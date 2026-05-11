type LocationLike = Pick<Location, 'hostname' | 'port'>;

const LOCAL_WEB_DEV_HOSTS = new Set(['localhost', '127.0.0.1', '::1']);
const CLIENT_DEV_PORT = '3000';

function isLocalClientDevLocation(location: LocationLike | null | undefined): boolean {
  if (!location) {
    return false;
  }

  return LOCAL_WEB_DEV_HOSTS.has(location.hostname) && location.port === CLIENT_DEV_PORT;
}

function getCurrentLocation(): LocationLike | null {
  if (typeof window === 'undefined') {
    return null;
  }

  return window.location;
}

export function resolveConsoleExternalUrl(location: LocationLike | null = getCurrentLocation()): string {
  return isLocalClientDevLocation(location) ? 'http://localhost:3100' : '/console/';
}

export function resolveScalarExternalUrl(location: LocationLike | null = getCurrentLocation()): string {
  return isLocalClientDevLocation(location) ? 'http://localhost:5100/scalar?auto=1' : '/scalar?auto=1';
}
