export const maximumExactNodeOverlayPoints = 1_000_000;
export const maximumSampledNodeOverlayPoints = 500_000;

export function getNodeOverlayStride(selectedPointCount: number): number {
  if (!Number.isSafeInteger(selectedPointCount) || selectedPointCount < 0)
    throw new Error('The selected point count is invalid.');
  if (selectedPointCount <= maximumExactNodeOverlayPoints) return 1;
  return Math.ceil(selectedPointCount / maximumSampledNodeOverlayPoints);
}
