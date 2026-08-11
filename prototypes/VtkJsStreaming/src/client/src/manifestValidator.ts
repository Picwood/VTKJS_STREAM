import type { ResourceDescriptor, SceneManifest } from './protocol';
import { expectedByteLength, ResourceValidationError } from './resourceDecoder';

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

export function validateManifest(value: unknown): SceneManifest {
  if (!isRecord(value)) {
    throw invalid('Manifest must be an object.');
  }
  const protocol = value['protocol'];
  if (!isRecord(protocol) || protocol['major'] !== 1 || protocol['minor'] !== 0) {
    throw new ResourceValidationError('protocol-version-unsupported', 'Only manifest protocol 1.0 is supported.');
  }
  requireUuid(value['sceneId'], 'sceneId');
  requireNumberTuple(value['worldOrigin'], 3, 'worldOrigin');
  requireNumberTuple(value['bounds'], 6, 'bounds');
  if (!Array.isArray(value['actors']) || !Array.isArray(value['resources']) || !isRecord(value['field'])) {
    throw invalid('Manifest actors, resources, or field metadata are missing.');
  }

  const resourceIds = new Set<string>();
  for (const candidate of value['resources']) {
    validateDescriptor(candidate, resourceIds);
  }
  for (const actor of value['actors']) {
    if (!isRecord(actor) || !Array.isArray(actor['chunks'])) {
      throw invalid('Actor or actor chunks are invalid.');
    }
    requireUuid(actor['actorId'], 'actorId');
    for (const chunk of actor['chunks']) {
      validateChunk(chunk, resourceIds);
    }
  }

  return value as unknown as SceneManifest;
}

function validateDescriptor(value: unknown, ids: Set<string>): asserts value is ResourceDescriptor {
  if (!isRecord(value)) {
    throw invalid('Resource descriptor must be an object.');
  }
  const id = requireUuid(value['resourceId'], 'resourceId');
  if (ids.has(id)) {
    throw invalid(`Duplicate resource ID ${id}.`);
  }
  ids.add(id);
  if (!['int32', 'uint32', 'float32', 'float64'].includes(String(value['dataType']))
    || typeof value['components'] !== 'number'
    || typeof value['elementCount'] !== 'number'
    || typeof value['byteLength'] !== 'number'
    || value['byteOrder'] !== 'little-endian'
    || value['contentEncoding'] !== 'identity'
    || typeof value['sha256'] !== 'string'
    || !/^[0-9a-f]{64}$/.test(value['sha256'])) {
    throw invalid(`Resource descriptor ${id} is malformed.`);
  }
  const descriptor = value as unknown as ResourceDescriptor;
  if (expectedByteLength(descriptor) !== descriptor.byteLength) {
    throw new ResourceValidationError('resource-length-invalid', `Resource descriptor ${id} has an inconsistent byte length.`);
  }
}

function validateChunk(value: unknown, resourceIds: ReadonlySet<string>): void {
  if (!isRecord(value) || !isRecord(value['resources'])) {
    throw invalid('Chunk must contain resource references.');
  }
  requireUuid(value['chunkId'], 'chunkId');
  const resources = value['resources'];
  for (const semantic of ['positions', 'triangleConnectivity', 'sourcePointIds', 'sourceElementIds', 'pointScalars']) {
    const reference = resources[semantic];
    if (!isRecord(reference) || typeof reference['resourceId'] !== 'string' || !resourceIds.has(reference['resourceId'])) {
      throw invalid(`Chunk has an invalid ${semantic} reference.`);
    }
  }
}

function requireUuid(value: unknown, name: string): string {
  if (typeof value !== 'string' || !uuidPattern.test(value)) {
    throw invalid(`${name} must be a lowercase UUID.`);
  }
  return value;
}

function requireNumberTuple(value: unknown, length: number, name: string): void {
  if (!Array.isArray(value) || value.length !== length || value.some((item) => typeof item !== 'number' || !Number.isFinite(item))) {
    throw invalid(`${name} must contain ${length} finite numbers.`);
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function invalid(message: string): ResourceValidationError {
  return new ResourceValidationError('manifest-invalid', message);
}
