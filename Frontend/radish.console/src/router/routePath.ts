export function matchesRoutePattern(routePath: string, pathname: string): boolean {
  const routeSegments = routePath.split('/').filter(Boolean);
  const pathSegments = pathname.split('/').filter(Boolean);
  if (routeSegments.length !== pathSegments.length) {
    return false;
  }

  return routeSegments.every((segment, index) => (
    segment.startsWith(':') || segment === pathSegments[index]
  ));
}
