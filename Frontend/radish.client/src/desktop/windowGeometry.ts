export interface WindowGeometryPosition {
  x: number;
  y: number;
}

export interface WindowGeometrySize {
  width: number;
  height: number;
}

export interface WindowGeometry {
  position: WindowGeometryPosition;
  size: WindowGeometrySize;
}

export interface ViewportBounds {
  width: number;
  height: number;
}

interface PersistedWindowGeometry extends WindowGeometry {
  updatedAt: number;
}

type PersistedWindowGeometryMap = Record<string, PersistedWindowGeometry>;
type PersistableParamValue =
  | string
  | number
  | boolean
  | null
  | PersistableParamValue[]
  | { [key: string]: PersistableParamValue };

const WINDOW_GEOMETRY_STORAGE_KEY = 'radish.desktop.window-geometries.v1';
const WINDOW_MAX_WIDTH_RATIO = 0.8;
const WINDOW_MAX_HEIGHT_RATIO = 0.85;

export const WINDOW_MIN_WIDTH = 400;
export const WINDOW_MIN_HEIGHT = 300;

const DEFAULT_VIEWPORT_BOUNDS: ViewportBounds = {
  width: 1440,
  height: 900
};

const clampNumber = (value: number, min: number, max: number) => {
  if (!Number.isFinite(value)) {
    return min;
  }

  if (max < min) {
    return min;
  }

  return Math.min(Math.max(value, min), max);
};

const getNumericValue = (value: unknown, fallback: number) => {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
};

const normalizePersistableParamValue = (value: unknown): PersistableParamValue | undefined => {
  if (value === undefined) {
    return undefined;
  }

  if (value === null) {
    return null;
  }

  if (typeof value === 'string' || typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : undefined;
  }

  if (Array.isArray(value)) {
    const normalizedItems = value
      .map((item) => normalizePersistableParamValue(item))
      .filter((item): item is PersistableParamValue => item !== undefined);

    return normalizedItems;
  }

  if (typeof value === 'object') {
    const normalizedEntries = Object.entries(value as Record<string, unknown>)
      .filter(([key]) => key.trim() && !key.startsWith('__'))
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, entryValue]) => [key, normalizePersistableParamValue(entryValue)] as const)
      .filter(([, entryValue]) => entryValue !== undefined);

    if (normalizedEntries.length === 0) {
      return undefined;
    }

    return Object.fromEntries(normalizedEntries) as { [key: string]: PersistableParamValue };
  }

  return undefined;
};

export const buildWindowPersistenceKey = (
  appId: string,
  appParams?: Record<string, unknown>
): string => {
  const normalizedParams = normalizePersistableParamValue(appParams);
  if (!normalizedParams || (typeof normalizedParams === 'object' && !Array.isArray(normalizedParams) && Object.keys(normalizedParams).length === 0)) {
    return appId;
  }

  return `${appId}:${JSON.stringify(normalizedParams)}`;
};

export const getViewportBounds = (): ViewportBounds => {
  if (typeof window === 'undefined') {
    return DEFAULT_VIEWPORT_BOUNDS;
  }

  return {
    width: Math.max(0, window.innerWidth),
    height: Math.max(0, window.innerHeight)
  };
};

const normalizeWindowSize = (
  size: WindowGeometrySize,
  bounds: ViewportBounds
): WindowGeometrySize => {
  const minWidth = Math.min(WINDOW_MIN_WIDTH, bounds.width || WINDOW_MIN_WIDTH);
  const minHeight = Math.min(WINDOW_MIN_HEIGHT, bounds.height || WINDOW_MIN_HEIGHT);
  const maxWidth = Math.max(minWidth, Math.floor(bounds.width * WINDOW_MAX_WIDTH_RATIO));
  const maxHeight = Math.max(minHeight, Math.floor(bounds.height * WINDOW_MAX_HEIGHT_RATIO));

  return {
    width: clampNumber(Math.round(getNumericValue(size.width, minWidth)), minWidth, maxWidth),
    height: clampNumber(Math.round(getNumericValue(size.height, minHeight)), minHeight, maxHeight)
  };
};

export const getCenteredWindowPosition = (
  size: WindowGeometrySize,
  bounds: ViewportBounds = getViewportBounds()
): WindowGeometryPosition => {
  const maxX = Math.max(0, bounds.width - size.width);
  const maxY = Math.max(0, bounds.height - size.height);

  return {
    x: Math.round(maxX / 2),
    y: Math.round(maxY / 2)
  };
};

export const clampWindowPosition = (
  position: WindowGeometryPosition,
  size: WindowGeometrySize,
  bounds: ViewportBounds = getViewportBounds()
): WindowGeometryPosition => {
  const maxX = Math.max(0, bounds.width - size.width);
  const maxY = Math.max(0, bounds.height - size.height);

  return {
    x: clampNumber(Math.round(getNumericValue(position.x, 0)), 0, maxX),
    y: clampNumber(Math.round(getNumericValue(position.y, 0)), 0, maxY)
  };
};

export const clampWindowGeometry = (
  geometry: WindowGeometry,
  bounds: ViewportBounds = getViewportBounds()
): WindowGeometry => {
  const size = normalizeWindowSize(geometry.size, bounds);
  const position = clampWindowPosition(geometry.position, size, bounds);

  return {
    position,
    size
  };
};

export const resolveInitialWindowGeometry = (
  defaultSize: WindowGeometrySize,
  persistedGeometry?: Partial<WindowGeometry> | null,
  bounds: ViewportBounds = getViewportBounds()
): WindowGeometry => {
  const nextSize = normalizeWindowSize(
    {
      width: getNumericValue(persistedGeometry?.size?.width, defaultSize.width),
      height: getNumericValue(persistedGeometry?.size?.height, defaultSize.height)
    },
    bounds
  );

  if (!persistedGeometry?.position) {
    return {
      position: getCenteredWindowPosition(nextSize, bounds),
      size: nextSize
    };
  }

  return {
    position: clampWindowPosition(
      {
        x: getNumericValue(persistedGeometry.position.x, 0),
        y: getNumericValue(persistedGeometry.position.y, 0)
      },
      nextSize,
      bounds
    ),
    size: nextSize
  };
};

const readPersistedWindowGeometryMap = (): PersistedWindowGeometryMap => {
  if (typeof window === 'undefined') {
    return {};
  }

  try {
    const raw = window.localStorage.getItem(WINDOW_GEOMETRY_STORAGE_KEY);
    if (!raw) {
      return {};
    }

    const parsed = JSON.parse(raw) as PersistedWindowGeometryMap;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
};

const writePersistedWindowGeometryMap = (geometries: PersistedWindowGeometryMap) => {
  if (typeof window === 'undefined') {
    return;
  }

  window.localStorage.setItem(WINDOW_GEOMETRY_STORAGE_KEY, JSON.stringify(geometries));
};

export const loadPersistedWindowGeometry = (appId: string): WindowGeometry | null => {
  if (!appId) {
    return null;
  }

  const storedGeometry = readPersistedWindowGeometryMap()[appId];
  if (!storedGeometry) {
    return null;
  }

  return {
    position: storedGeometry.position,
    size: storedGeometry.size
  };
};

export const savePersistedWindowGeometry = (
  appId: string,
  geometry: WindowGeometry
) => {
  if (!appId || typeof window === 'undefined') {
    return;
  }

  const geometries = readPersistedWindowGeometryMap();
  geometries[appId] = {
    ...geometry,
    updatedAt: Date.now()
  };
  writePersistedWindowGeometryMap(geometries);
};
