import type { RenderActor, RenderChunk, ResourceDescriptor, SceneManifest, SupportedTypedArray } from './protocol';

export interface ResourceClient {
  getResource(descriptor: ResourceDescriptor, signal?: AbortSignal): Promise<SupportedTypedArray>;
}

export interface LoadedGeometry {
  readonly actor: RenderActor;
  readonly chunk: RenderChunk;
  readonly positions: Float32Array;
  readonly connectivity: Uint32Array;
}

export interface LoadedAttributes {
  readonly actorId: string;
  readonly chunkId: string;
  readonly sourcePointIds: Int32Array;
  readonly sourceElementIds: Int32Array;
  readonly scalars: Float32Array;
}

export interface LoadProgress {
  readonly completedResources: number;
  readonly totalResources: number;
  readonly transferredBytes: number;
  readonly totalBytes: number;
}

export interface ChunkLoadCallbacks {
  readonly onGeometry: (geometry: LoadedGeometry) => void;
  readonly onAttributes: (attributes: LoadedAttributes) => void;
  readonly onProgress: (progress: LoadProgress) => void;
}

export class ChunkLoader {
  readonly #api: ResourceClient;
  readonly #limiter: AsyncLimiter;

  public constructor(api: ResourceClient, concurrency = 3) {
    this.#api = api;
    this.#limiter = new AsyncLimiter(concurrency);
  }

  public async load(manifest: SceneManifest, callbacks: ChunkLoadCallbacks, signal: AbortSignal): Promise<void> {
    const descriptors = new Map(manifest.resources.map((descriptor) => [descriptor.resourceId, descriptor]));
    const chunks = manifest.actors
      .flatMap((actor) => actor.chunks.map((chunk) => ({ actor, chunk })))
      .sort((left, right) => right.chunk.priority - left.chunk.priority || left.chunk.ordinal - right.chunk.ordinal);
    const totals = {
      completedResources: 0,
      totalResources: chunks.length * 5,
      transferredBytes: 0,
      totalBytes: chunks.reduce((sum, item) => sum + resourceIds(item.chunk).reduce(
        (chunkSum, id) => chunkSum + requireDescriptor(descriptors, id).byteLength,
        0,
      ), 0),
    };

    const fetchResource = async (resourceId: string) => {
      const descriptor = requireDescriptor(descriptors, resourceId);
      const result = await this.#limiter.run(() => this.#api.getResource(descriptor, signal), signal);
      totals.completedResources += 1;
      totals.transferredBytes += descriptor.byteLength;
      callbacks.onProgress({ ...totals });
      return result;
    };

    await Promise.all(chunks.map(async ({ actor, chunk }) => {
      const [positions, connectivity] = await Promise.all([
        fetchResource(chunk.resources.positions.resourceId),
        fetchResource(chunk.resources.triangleConnectivity.resourceId),
      ]);
      if (!(positions instanceof Float32Array) || !(connectivity instanceof Uint32Array)) {
        throw new TypeError(`Chunk ${chunk.chunkId} geometry resources have unexpected types.`);
      }
      callbacks.onGeometry({ actor, chunk, positions, connectivity });

      const [sourcePointIds, sourceElementIds, scalars] = await Promise.all([
        fetchResource(chunk.resources.sourcePointIds.resourceId),
        fetchResource(chunk.resources.sourceElementIds.resourceId),
        fetchResource(chunk.resources.pointScalars.resourceId),
      ]);
      if (!(sourcePointIds instanceof Int32Array)
        || !(sourceElementIds instanceof Int32Array)
        || !(scalars instanceof Float32Array)) {
        throw new TypeError(`Chunk ${chunk.chunkId} attribute resources have unexpected types.`);
      }
      callbacks.onAttributes({ actorId: actor.actorId, chunkId: chunk.chunkId, sourcePointIds, sourceElementIds, scalars });
    }));
  }
}

class AsyncLimiter {
  readonly #limit: number;
  #active = 0;
  readonly #waiters: Array<() => void> = [];

  public constructor(limit: number) {
    if (!Number.isInteger(limit) || limit < 1 || limit > 16) {
      throw new RangeError('Resource concurrency must be an integer from 1 through 16.');
    }
    this.#limit = limit;
  }

  public async run<T>(operation: () => Promise<T>, signal: AbortSignal): Promise<T> {
    await this.#acquire(signal);
    try {
      signal.throwIfAborted();
      return await operation();
    } finally {
      this.#active -= 1;
      this.#waiters.shift()?.();
    }
  }

  async #acquire(signal: AbortSignal): Promise<void> {
    signal.throwIfAborted();
    if (this.#active < this.#limit) {
      this.#active += 1;
      return;
    }
    await new Promise<void>((resolve, reject) => {
      const resume = () => {
        signal.removeEventListener('abort', abort);
        this.#active += 1;
        resolve();
      };
      const abort = () => {
        const index = this.#waiters.indexOf(resume);
        if (index >= 0) {
          this.#waiters.splice(index, 1);
        }
        reject(signal.reason);
      };
      this.#waiters.push(resume);
      signal.addEventListener('abort', abort, { once: true });
    });
  }
}

function resourceIds(chunk: RenderChunk): string[] {
  return [
    chunk.resources.positions.resourceId,
    chunk.resources.triangleConnectivity.resourceId,
    chunk.resources.sourcePointIds.resourceId,
    chunk.resources.sourceElementIds.resourceId,
    chunk.resources.pointScalars.resourceId,
  ];
}

function requireDescriptor(descriptors: ReadonlyMap<string, ResourceDescriptor>, id: string): ResourceDescriptor {
  const descriptor = descriptors.get(id);
  if (!descriptor) {
    throw new Error(`Manifest references missing resource ${id}.`);
  }
  return descriptor;
}
