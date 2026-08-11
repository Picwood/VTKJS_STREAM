import { describe, expect, it } from 'vitest';
import { ChunkLoader, type ResourceClient } from './chunkLoader';
import type { DataType, RenderActor, RenderChunk, ResourceDescriptor, SceneManifest, SupportedTypedArray } from './protocol';

describe('ChunkLoader', () => {
  it('bounds requests and publishes geometry before attributes for each chunk', async () => {
    const manifest = createManifest(2);
    let active = 0;
    let maximumActive = 0;
    const client: ResourceClient = {
      async getResource(descriptor) {
        active += 1;
        maximumActive = Math.max(maximumActive, active);
        await delay(2);
        active -= 1;
        return arrayFor(descriptor);
      },
    };
    const events: string[] = [];
    const progress: number[] = [];
    await new ChunkLoader(client, 2).load(manifest, {
      onGeometry: ({ chunk }) => events.push(`geometry:${chunk.chunkId}`),
      onAttributes: ({ chunkId }) => events.push(`attributes:${chunkId}`),
      onProgress: (value) => progress.push(value.completedResources),
    }, new AbortController().signal);

    expect(maximumActive).toBe(2);
    expect(progress.at(-1)).toBe(10);
    for (const actor of manifest.actors) {
      const chunkId = actor.chunks[0]?.chunkId ?? '';
      expect(events.indexOf(`geometry:${chunkId}`)).toBeLessThan(events.indexOf(`attributes:${chunkId}`));
    }
  });

  it('rejects promptly when queued work is cancelled', async () => {
    const controller = new AbortController();
    const client: ResourceClient = {
      async getResource(_descriptor, signal) {
        await new Promise<void>((resolve, reject) => {
          const timer = setTimeout(resolve, 100);
          signal?.addEventListener('abort', () => {
            clearTimeout(timer);
            reject(signal.reason);
          }, { once: true });
        });
        return new Float32Array();
      },
    };
    const loading = new ChunkLoader(client, 1).load(createManifest(2), {
      onGeometry: () => undefined,
      onAttributes: () => undefined,
      onProgress: () => undefined,
    }, controller.signal);
    controller.abort(new DOMException('cancelled', 'AbortError'));
    await expect(loading).rejects.toMatchObject({ name: 'AbortError' });
  });
});

function createManifest(chunkCount: number): SceneManifest {
  const descriptors: ResourceDescriptor[] = [];
  const actors: RenderActor[] = [];
  for (let index = 0; index < chunkCount; index += 1) {
    const chunkOrdinal = index + 1;
    const refs = {
      positions: addDescriptor(descriptors, chunkOrdinal, 1, 'positions', 'float32', 3, 3),
      triangleConnectivity: addDescriptor(descriptors, chunkOrdinal, 2, 'triangleConnectivity', 'uint32', 3, 1),
      sourcePointIds: addDescriptor(descriptors, chunkOrdinal, 3, 'sourcePointIds', 'int32', 1, 3),
      sourceElementIds: addDescriptor(descriptors, chunkOrdinal, 4, 'sourceElementIds', 'int32', 1, 1),
      pointScalars: addDescriptor(descriptors, chunkOrdinal, 5, 'pointScalars', 'float32', 1, 3),
    };
    const chunk: RenderChunk = {
      chunkId: uuid(30, chunkOrdinal),
      ordinal: index,
      priority: 100 - index,
      bounds: [0, 1, 0, 1, 0, 0],
      pointCount: 3,
      triangleCount: 1,
      resources: refs,
    };
    actors.push({
      actorId: uuid(20, chunkOrdinal),
      name: `Actor ${chunkOrdinal}`,
      visible: true,
      pickable: true,
      material: { frontRgba: [0.7, 0.7, 0.7, 1], backRgba: [1, 0, 0, 1] },
      chunks: [chunk],
    });
  }
  return {
    protocol: { major: 1, minor: 0 },
    sceneId: uuid(10, 1),
    sceneRevision: 1,
    name: 'loader-test',
    worldOrigin: [0, 0, 0],
    bounds: [0, 1, 0, 1, 0, 0],
    actors,
    resources: descriptors,
    field: {
      name: 'test', component: 'value', units: '', association: 'point',
      validRange: [-1, 1], displayedRange: [-1, 1], invalidValuePolicy: 'nan-color',
    },
  };
}

function addDescriptor(
  output: ResourceDescriptor[],
  chunk: number,
  slot: number,
  semantic: string,
  dataType: DataType,
  components: number,
  elementCount: number,
) {
  const resourceId = uuid(40 + chunk, slot);
  output.push({
    resourceId,
    semantic,
    dataType,
    components,
    elementCount,
    byteLength: components * elementCount * 4,
    byteOrder: 'little-endian',
    contentEncoding: 'identity',
    sha256: '0'.repeat(64),
    version: 1,
    immutable: true,
    uri: `/resources/${resourceId}`,
  });
  return { resourceId };
}

function arrayFor(descriptor: ResourceDescriptor): SupportedTypedArray {
  const length = descriptor.components * descriptor.elementCount;
  switch (descriptor.dataType) {
    case 'int32': return new Int32Array(length);
    case 'uint32': return new Uint32Array(length);
    case 'float32': return new Float32Array(length);
    case 'float64': return new Float64Array(length);
  }
}

function uuid(prefix: number, suffix: number): string {
  return `${prefix.toString().padStart(8, '0')}-0000-0000-0000-${suffix.toString().padStart(12, '0')}`;
}

async function delay(milliseconds: number): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, milliseconds));
}
