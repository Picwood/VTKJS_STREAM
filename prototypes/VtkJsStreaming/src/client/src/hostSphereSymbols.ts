import type { HostSphereSymbol, HostSphereSymbolStateMessage } from './protocol';

export const maximumSphereSymbolActors = 4096;
export const maximumSphereSymbolPoints = 100_000;

export function normalizeHostSphereSymbols(value: unknown): readonly HostSphereSymbol[] {
  const message = value as Partial<HostSphereSymbolStateMessage> | null;
  if (!message || message.type !== 'prepomax.sphere-symbols.updated-v1'
    || !Array.isArray(message.symbols) || message.symbols.length > maximumSphereSymbolActors)
    throw new Error('The embedded host sent an invalid sphere-symbol update.');
  const names = new Set<string>();
  let pointCount = 0;
  for (const symbol of message.symbols as readonly Partial<HostSphereSymbol>[]) {
    if (!symbol || typeof symbol.name !== 'string' || symbol.name.length === 0 || symbol.name.length > 1024
      || names.has(symbol.name) || !Array.isArray(symbol.points) || symbol.points.length === 0
      || !Array.isArray(symbol.rgba) || symbol.rgba.length !== 4
      || symbol.rgba.some((component) => typeof component !== 'number' || !Number.isFinite(component)
        || component < 0 || component > 1)
      || typeof symbol.symbolSize !== 'number' || !Number.isFinite(symbol.symbolSize)
      || symbol.symbolSize <= 0 || symbol.symbolSize > 1000
      || symbol.points.some((point) => !Array.isArray(point) || point.length !== 3
        || point.some((coordinate) => typeof coordinate !== 'number' || !Number.isFinite(coordinate)
          || Math.abs(coordinate) > 1e150)))
      throw new Error('The embedded host sent an invalid sphere-symbol update.');
    names.add(symbol.name);
    pointCount += symbol.points.length;
  }
  if (pointCount > maximumSphereSymbolPoints)
    throw new Error('The embedded host sphere symbols exceed their point safety bound.');
  return (message.symbols as readonly HostSphereSymbol[]).map((symbol) => ({
    ...symbol,
    points: symbol.points.map((point) => [...point] as [number, number, number]),
    rgba: [...symbol.rgba] as [number, number, number, number],
  }));
}
