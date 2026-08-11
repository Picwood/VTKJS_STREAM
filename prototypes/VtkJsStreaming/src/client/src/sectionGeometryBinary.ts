export interface SectionGeometryRequest {
  readonly point: readonly [number, number, number];
  readonly normal: readonly [number, number, number];
  readonly lightenColors: boolean;
  readonly sectionColor: readonly [number, number, number, number];
  readonly actorOffsets: readonly { readonly actorName: string; readonly offset: readonly [number, number, number] }[];
}

export interface SectionGeometryActor {
  readonly actorId: string;
  readonly parentActorId: string;
  readonly name: string;
  readonly rgba: readonly [number, number, number, number];
  readonly lineWidthPx: number;
  readonly positions: Float32Array;
  readonly triangleConnectivity: Uint32Array;
  readonly lineConnectivity: Uint32Array;
  readonly pointScalars: Float32Array;
  readonly sourceElementIds: Int32Array;
  readonly bounds: readonly [number, number, number, number, number, number];
}

export interface SectionGeometryResult {
  readonly actors: readonly SectionGeometryActor[];
  readonly skippedCells: number;
}

const magic = new TextEncoder().encode('PMXSEC1\0');

export function decodeSectionGeometry(buffer: ArrayBuffer): SectionGeometryResult {
  const bytes = new Uint8Array(buffer);
  let offset = 0;
  const requireBytes = (count: number) => {
    if (!Number.isSafeInteger(count) || count < 0 || offset > bytes.length - count)
      throw new Error('The section geometry response is truncated or has invalid counts.');
  };
  requireBytes(magic.length);
  if (!magic.every((value, index) => bytes[index] === value)) throw new Error('The section geometry response has invalid magic.');
  offset += magic.length;
  const view = new DataView(buffer);
  const readInt32 = () => { requireBytes(4); const value = view.getInt32(offset, true); offset += 4; return value; };
  const readFloat32 = () => { requireBytes(4); const value = view.getFloat32(offset, true); offset += 4; return value; };
  const readFloat64 = () => { requireBytes(8); const value = view.getFloat64(offset, true); offset += 8; return value; };
  const readString = () => {
    const length = readInt32();
    if (length < 1 || length > 4096) throw new Error('The section geometry response contains an invalid string.');
    requireBytes(length);
    const value = new TextDecoder('utf-8', { fatal: true }).decode(bytes.subarray(offset, offset + length));
    offset += length;
    return value;
  };
  const readArray = <T extends Float32Array | Uint32Array | Int32Array>(count: number,
    constructor: { new(buffer: ArrayBuffer): T }): T => {
    const byteLength = count * 4;
    requireBytes(byteLength);
    const value = new constructor(buffer.slice(offset, offset + byteLength));
    offset += byteLength;
    return value;
  };

  const actorCount = readInt32();
  const skippedCells = readInt32();
  if (actorCount < 0 || actorCount > 512 || skippedCells < 0) throw new Error('The section geometry response exceeds its bounds.');
  const actors: SectionGeometryActor[] = [];
  let totalPoints = 0;
  let totalTriangles = 0;
  for (let actorIndex = 0; actorIndex < actorCount; actorIndex += 1) {
    const actorId = readString();
    const parentActorId = readString();
    const name = readString();
    const rgba = [readFloat32(), readFloat32(), readFloat32(), readFloat32()] as const;
    const lineWidthPx = readFloat32();
    const pointCount = readInt32();
    const triangleCount = readInt32();
    const lineCount = readInt32();
    totalPoints += pointCount;
    totalTriangles += triangleCount;
    if (pointCount < 0 || triangleCount < 0 || lineCount < 0 || totalPoints > 500_000 || totalTriangles > 500_000
      || rgba.some((value) => !Number.isFinite(value) || value < 0 || value > 1) || !Number.isFinite(lineWidthPx))
      throw new Error('The section geometry response contains invalid actor metadata.');
    const bounds = [readFloat64(), readFloat64(), readFloat64(), readFloat64(), readFloat64(), readFloat64()] as const;
    const positions = readArray(pointCount * 3, Float32Array);
    const triangleConnectivity = readArray(triangleCount * 3, Uint32Array);
    const lineConnectivity = readArray(lineCount * 2, Uint32Array);
    const pointScalars = readArray(pointCount, Float32Array);
    const sourceElementIds = readArray(triangleCount, Int32Array);
    if (positions.some((value) => !Number.isFinite(value)) || pointScalars.some((value) => value === Infinity || value === -Infinity)
      || triangleConnectivity.some((value) => value >= pointCount) || lineConnectivity.some((value) => value >= pointCount)
      || bounds.some((value) => !Number.isFinite(value)))
      throw new Error('The section geometry response contains invalid arrays.');
    actors.push({ actorId, parentActorId, name, rgba, lineWidthPx, positions, triangleConnectivity,
      lineConnectivity, pointScalars, sourceElementIds, bounds });
  }
  if (offset !== buffer.byteLength) throw new Error('The section geometry response contains trailing bytes.');
  return { actors, skippedCells };
}
