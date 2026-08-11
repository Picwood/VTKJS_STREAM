import type { HostSelectionStateMessage } from './protocol';

const decoder = new TextDecoder('utf-8', { fatal: true });
const maximumBytes = 64 * 1024 * 1024;
const maximumIds = 8_000_000;

export function decodeHostSelection(buffer: ArrayBuffer): HostSelectionStateMessage {
  if (buffer.byteLength < 40 || buffer.byteLength > maximumBytes)
    throw new Error('The shared host selection has an invalid length.');
  const bytes = new Uint8Array(buffer);
  if (decoder.decode(bytes.subarray(0, 8)) !== 'PMXSEL1\0')
    throw new Error('The shared host selection has an invalid signature.');
  const view = new DataView(buffer);
  let offset = 8;
  const readUint32 = (): number => {
    if (offset + 4 > buffer.byteLength) throw new Error('The shared host selection is truncated.');
    const value = view.getUint32(offset, true); offset += 4; return value;
  };
  const readInt32 = (): number => {
    if (offset + 4 > buffer.byteLength) throw new Error('The shared host selection is truncated.');
    const value = view.getInt32(offset, true); offset += 4; return value;
  };
  const version = readUint32();
  const itemCode = readUint32();
  const idCount = readUint32();
  const actorCount = readUint32();
  const faceCount = readUint32();
  const faceNodeCount = readUint32();
  const edgeCount = readUint32();
  const edgeNodeCount = readUint32();
  if (version !== 1 || idCount > maximumIds || actorCount > 4096 || faceCount > 250_000
    || faceNodeCount > 2_000_000 || edgeCount > 250_000 || edgeNodeCount > 750_000)
    throw new Error('The shared host selection exceeds its safety bounds.');
  const item = decodeItem(itemCode);
  const ids = Array.from({ length: idCount }, readInt32);
  const actorNames: string[] = [];
  for (let index = 0; index < actorCount; index += 1) {
    const length = readUint32();
    if (length === 0 || length > 4096 || offset + length > buffer.byteLength)
      throw new Error('The shared host selection contains an invalid actor name.');
    actorNames.push(decoder.decode(bytes.subarray(offset, offset + length)));
    offset += length;
  }
  const faceNodeIds = readCells(faceCount, faceNodeCount, 3, 32, readUint32, readInt32);
  const edgeNodeIds = readCells(edgeCount, edgeNodeCount, 2, 3, readUint32, readInt32);
  if (offset !== buffer.byteLength) throw new Error('The shared host selection has trailing bytes.');
  return { type: 'prepomax.selection.updated-v1', item, ids, actorNames, faceNodeIds, edgeNodeIds };
}

function readCells(count: number, expectedReferences: number, minimum: number, maximum: number,
                   readLength: () => number, readId: () => number): number[][] {
  const cells: number[][] = [];
  let references = 0;
  for (let index = 0; index < count; index += 1) {
    const length = readLength();
    if (length < minimum || length > maximum) throw new Error('The shared host selection has invalid cell arity.');
    references += length;
    if (references > expectedReferences) throw new Error('The shared host selection has excess cell references.');
    cells.push(Array.from({ length }, readId));
  }
  if (references !== expectedReferences) throw new Error('The shared host selection cell count does not match.');
  return cells;
}

function decodeItem(value: number): HostSelectionStateMessage['item'] {
  const items: HostSelectionStateMessage['item'][] = [
    'none', 'node', 'element', 'surface', 'geometry-edge', 'geometry', 'part', 'unsupported',
  ];
  const item = items[value];
  if (!item || item === 'unsupported') throw new Error('The shared host selection has an invalid item.');
  return item;
}
