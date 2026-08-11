export interface TriangleSourceMapping {
  readonly sourceElementId: number;
  readonly candidateSourceNodeId: number;
}

export function resolveTriangleSource(
  connectivity: Uint32Array,
  positions: Float32Array,
  sourcePointIds: Int32Array,
  sourceElementIds: Int32Array,
  primitiveIndex: number,
  relativeHit: readonly [number, number, number],
): TriangleSourceMapping {
  if (!Number.isInteger(primitiveIndex) || primitiveIndex < 0 || primitiveIndex >= sourceElementIds.length) {
    throw new RangeError('The selected triangle index is outside the source-element map.');
  }
  const connectivityOffset = primitiveIndex * 3;
  if (connectivityOffset + 2 >= connectivity.length) {
    throw new RangeError('The selected triangle is outside the connectivity array.');
  }

  let candidateSourceNodeId: number | null = null;
  let nearestSquaredDistance = Number.POSITIVE_INFINITY;
  for (let vertex = 0; vertex < 3; vertex += 1) {
    const localPointIndex = connectivity[connectivityOffset + vertex];
    if (localPointIndex === undefined || localPointIndex >= sourcePointIds.length || localPointIndex * 3 + 2 >= positions.length) {
      throw new RangeError('The selected triangle references an invalid local point.');
    }
    const dx = (positions[localPointIndex * 3] ?? Number.NaN) - relativeHit[0];
    const dy = (positions[localPointIndex * 3 + 1] ?? Number.NaN) - relativeHit[1];
    const dz = (positions[localPointIndex * 3 + 2] ?? Number.NaN) - relativeHit[2];
    const squaredDistance = dx * dx + dy * dy + dz * dz;
    if (squaredDistance < nearestSquaredDistance) {
      nearestSquaredDistance = squaredDistance;
      candidateSourceNodeId = sourcePointIds[localPointIndex] ?? null;
    }
  }
  if (candidateSourceNodeId === null) {
    throw new Error('The selected triangle has no source-node candidate.');
  }
  return { sourceElementId: sourceElementIds[primitiveIndex]!, candidateSourceNodeId };
}
