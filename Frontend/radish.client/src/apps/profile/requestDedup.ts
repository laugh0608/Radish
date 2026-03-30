const inFlightRequests = new Map<string, Promise<unknown>>();

export function reuseInFlightRequest<T>(key: string, requestFactory: () => Promise<T>): Promise<T> {
  const existingRequest = inFlightRequests.get(key);
  if (existingRequest) {
    return existingRequest as Promise<T>;
  }

  const requestPromise = requestFactory().finally(() => {
    const currentRequest = inFlightRequests.get(key);
    if (currentRequest === requestPromise) {
      inFlightRequests.delete(key);
    }
  });

  inFlightRequests.set(key, requestPromise as Promise<unknown>);
  return requestPromise;
}
