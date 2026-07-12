export function getInitialChartDimension(height: number | string | undefined) {
  return {
    width: 1,
    height: typeof height === 'number' && Number.isFinite(height) && height > 0 ? height : 220
  };
}
