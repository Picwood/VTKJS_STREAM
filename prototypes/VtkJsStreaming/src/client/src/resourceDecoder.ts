import type { DataType, ResourceDescriptor, SupportedTypedArray } from './protocol';

const bytesPerElement: Readonly<Record<DataType, number>> = {
  int32: 4,
  uint32: 4,
  float32: 4,
  float64: 8,
};

export class ResourceValidationError extends Error {
  public constructor(public readonly code: string, message: string) {
    super(message);
    this.name = 'ResourceValidationError';
  }
}

export function expectedByteLength(descriptor: ResourceDescriptor): number {
  if (!Number.isSafeInteger(descriptor.elementCount) || descriptor.elementCount < 0
    || !Number.isSafeInteger(descriptor.components) || descriptor.components <= 0) {
    throw new ResourceValidationError('resource-descriptor-invalid', 'Resource counts must be safe non-negative integers.');
  }

  const result = descriptor.elementCount * descriptor.components * bytesPerElement[descriptor.dataType];
  if (!Number.isSafeInteger(result)) {
    throw new ResourceValidationError('resource-length-invalid', 'Resource length exceeds JavaScript safe integer range.');
  }
  return result;
}

export function decodeResource(descriptor: ResourceDescriptor, buffer: ArrayBuffer): SupportedTypedArray {
  if (descriptor.byteOrder !== 'little-endian') {
    throw new ResourceValidationError('resource-endianness-unsupported', 'Protocol V1 requires little-endian resources.');
  }
  if (!isLittleEndian()) {
    throw new ResourceValidationError('client-endianness-unsupported', 'This client currently requires a little-endian platform.');
  }

  const expected = expectedByteLength(descriptor);
  if (descriptor.byteLength !== expected || buffer.byteLength !== expected) {
    throw new ResourceValidationError(
      'resource-length-invalid',
      `Expected ${expected} bytes for ${descriptor.resourceId}, received ${buffer.byteLength}.`,
    );
  }

  switch (descriptor.dataType) {
    case 'int32': return new Int32Array(buffer);
    case 'uint32': return new Uint32Array(buffer);
    case 'float32': return new Float32Array(buffer);
    case 'float64': return new Float64Array(buffer);
  }
}

export async function verifySha256(descriptor: ResourceDescriptor, buffer: ArrayBuffer): Promise<void> {
  const digest = await globalThis.crypto.subtle.digest('SHA-256', buffer);
  const actual = Array.from(new Uint8Array(digest), (value) => value.toString(16).padStart(2, '0')).join('');
  if (actual !== descriptor.sha256) {
    throw new ResourceValidationError(
      'resource-hash-invalid',
      `SHA-256 mismatch for ${descriptor.resourceId}.`,
    );
  }
}

export function toVtkTriangleCells(connectivity: Uint32Array): Uint32Array {
  if (connectivity.length % 3 !== 0) {
    throw new ResourceValidationError('connectivity-invalid', 'Triangle connectivity length must be divisible by three.');
  }

  const triangleCount = connectivity.length / 3;
  const packed = new Uint32Array(triangleCount * 4);
  for (let triangle = 0; triangle < triangleCount; triangle += 1) {
    const source = triangle * 3;
    const target = triangle * 4;
    packed[target] = 3;
    packed[target + 1] = connectivity[source] ?? 0;
    packed[target + 2] = connectivity[source + 1] ?? 0;
    packed[target + 3] = connectivity[source + 2] ?? 0;
  }
  return packed;
}

export function toVtkLineCells(connectivity: Uint32Array): Uint32Array {
  if (connectivity.length % 2 !== 0) {
    throw new ResourceValidationError('connectivity-invalid', 'Line connectivity length must be divisible by two.');
  }
  const packed = new Uint32Array(connectivity.length / 2 * 3);
  for (let line = 0; line < connectivity.length / 2; line += 1) {
    packed[line * 3] = 2;
    packed[line * 3 + 1] = connectivity[line * 2] ?? 0;
    packed[line * 3 + 2] = connectivity[line * 2 + 1] ?? 0;
  }
  return packed;
}

function isLittleEndian(): boolean {
  const word = new Uint16Array([0x00ff]);
  return new Uint8Array(word.buffer)[0] === 0xff;
}
