export interface TriangleSourceMapping {
  readonly sourceElementId: number;
  readonly candidateSourceNodeId: number;
}

/** Recover the hit from the GPU-selected triangle, without its quantized depth buffer. */
export function intersectPickedTriangle(
  connectivity: Uint32Array, positions: Float32Array, primitiveIndex: number,
  near: readonly [number, number, number], far: readonly [number, number, number],
): [number, number, number] | null {
  if (!Number.isInteger(primitiveIndex) || primitiveIndex < 0 || primitiveIndex * 3 + 2 >= connectivity.length)
    throw new RangeError('The selected triangle is outside the connectivity array.');
  const vertices = [0, 1, 2].map((i) => {
    const index = connectivity[primitiveIndex * 3 + i]! * 3;
    if (index + 2 >= positions.length) throw new RangeError('The selected triangle references an invalid point.');
    return [positions[index]!, positions[index + 1]!, positions[index + 2]!] as const;
  });
  const [a, b, c] = vertices as [typeof vertices[number], typeof vertices[number], typeof vertices[number]];
  const sub = (x: readonly number[], y: readonly number[]) => x.map((v, i) => v - y[i]!);
  const cross = (x: readonly number[], y: readonly number[]) => [x[1]! * y[2]! - x[2]! * y[1]!,
    x[2]! * y[0]! - x[0]! * y[2]!, x[0]! * y[1]! - x[1]! * y[0]!];
  const dot = (x: readonly number[], y: readonly number[]) => x.reduce((sum, v, i) => sum + v * y[i]!, 0);
  const edge1 = sub(b, a), edge2 = sub(c, a), direction = sub(far, near);
  const normal = cross(edge1, edge2);
  const denominator = dot(normal, direction);
  const magnitude = Math.hypot(...normal) * Math.hypot(...direction);
  if (!Number.isFinite(magnitude) || magnitude === 0 || Math.abs(denominator) <= magnitude * 1e-14) return null;
  const t = dot(normal, sub(a, near)) / denominator;
  if (!Number.isFinite(t) || t < -1e-10 || t > 1 + 1e-10) return null;
  const hit = near.map((value, i) => value + t * direction[i]!);
  const delta = sub(hit, a), normalSquared = dot(normal, normal);
  const u = dot(cross(delta, edge2), normal) / normalSquared;
  const v = dot(cross(edge1, delta), normal) / normalSquared;
  // Rasterization samples pixel centers. At a shared edge, clamp subpixel spill
  // to the triangle whose ID the GPU selected, retaining exact planar geometry.
  const weights = [Math.max(0, 1 - u - v), Math.max(0, u), Math.max(0, v)];
  const sum = weights.reduce((total, value) => total + value, 0);
  return [0, 1, 2].map((i) => a[i]! + weights[1]! / sum * edge1[i]! + weights[2]! / sum * edge2[i]!) as [number, number, number];
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
