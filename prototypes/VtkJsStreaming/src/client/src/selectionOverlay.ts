export const maximumExactNodeOverlayPoints = 1_000_000;

export function getNodeOverlayStride(selectedPointCount: number): number {
  if (!Number.isSafeInteger(selectedPointCount) || selectedPointCount < 0)
    throw new Error('The selected point count is invalid.');
  return Math.max(1, Math.ceil(selectedPointCount / maximumExactNodeOverlayPoints));
}
