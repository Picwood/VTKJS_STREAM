export type SectionSelectionPoint = readonly [number, number, number];

export function clipPolygonToSection(
  polygon: readonly SectionSelectionPoint[],
  planePoint: SectionSelectionPoint,
  planeNormal: SectionSelectionPoint,
): SectionSelectionPoint[] {
  if (polygon.length === 0) return [];
  const evaluate = (point: SectionSelectionPoint): number =>
    planeNormal[0] * (point[0] - planePoint[0]) +
    planeNormal[1] * (point[1] - planePoint[1]) +
    planeNormal[2] * (point[2] - planePoint[2]);
  const clipped: SectionSelectionPoint[] = [];
  let previous = polygon[polygon.length - 1]!;
  let previousValue = evaluate(previous);
  for (const current of polygon) {
    const currentValue = evaluate(current);
    const previousInside = previousValue >= 0;
    const currentInside = currentValue >= 0;
    if (previousInside !== currentInside) {
      const fraction = previousValue / (previousValue - currentValue);
      clipped.push([
        previous[0] + fraction * (current[0] - previous[0]),
        previous[1] + fraction * (current[1] - previous[1]),
        previous[2] + fraction * (current[2] - previous[2]),
      ]);
    }
    if (currentInside) clipped.push(current);
    previous = current;
    previousValue = currentValue;
  }
  return clipped;
}
