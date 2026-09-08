import type { HostTransformationStateMessage } from './protocol';

const maximumMatrices = 256;
const maximumMagnitude = 1e150;

export function normalizeHostTransformations(value: unknown): HostTransformationStateMessage {
  const message = value as { type?: unknown; visible?: unknown; matrices?: unknown } | null;
  if (!message || message.type !== 'prepomax.transformations.updated-v1'
      || typeof message.visible !== 'boolean' || !Array.isArray(message.matrices)
      || message.matrices.length > maximumMatrices)
    throw new Error('The transformation state is malformed or exceeds its bounds.');
  const matrices = message.matrices.map((candidate) => {
    if (!Array.isArray(candidate) || candidate.length !== 16
        || candidate.some((entry) => typeof entry !== 'number' || !Number.isFinite(entry)
          || Math.abs(entry) > maximumMagnitude))
      throw new Error('A transformation matrix is malformed.');
    const matrix = [...candidate] as number[];
    if (Math.abs(matrix[12]!) > 1e-12 || Math.abs(matrix[13]!) > 1e-12
        || Math.abs(matrix[14]!) > 1e-12 || Math.abs(matrix[15]! - 1) > 1e-12)
      throw new Error('A transformation matrix is not affine.');
    const determinant = matrix[0]! * (matrix[5]! * matrix[10]! - matrix[6]! * matrix[9]!)
      - matrix[1]! * (matrix[4]! * matrix[10]! - matrix[6]! * matrix[8]!)
      + matrix[2]! * (matrix[4]! * matrix[9]! - matrix[5]! * matrix[8]!);
    if (!Number.isFinite(determinant) || Math.abs(determinant) < 1e-12)
      throw new Error('A transformation matrix is singular.');
    return matrix;
  });
  return { type: 'prepomax.transformations.updated-v1', visible: message.visible, matrices };
}

export function toRelativeTransformationMatrix(
  matrix: readonly number[], origin: readonly number[],
): Float32Array {
  if (matrix.length !== 16 || origin.length !== 3)
    throw new Error('A transformation matrix and floating origin are required.');
  const tx = matrix[0]! * origin[0]! + matrix[1]! * origin[1]! + matrix[2]! * origin[2]!
    + matrix[3]! - origin[0]!;
  const ty = matrix[4]! * origin[0]! + matrix[5]! * origin[1]! + matrix[6]! * origin[2]!
    + matrix[7]! - origin[1]!;
  const tz = matrix[8]! * origin[0]! + matrix[9]! * origin[1]! + matrix[10]! * origin[2]!
    + matrix[11]! - origin[2]!;
  return new Float32Array([
    matrix[0]!, matrix[4]!, matrix[8]!, 0,
    matrix[1]!, matrix[5]!, matrix[9]!, 0,
    matrix[2]!, matrix[6]!, matrix[10]!, 0,
    tx, ty, tz, 1,
  ]);
}
