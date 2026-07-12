import { createContext } from 'react';

export interface LoadingContextValue {
  loading: boolean;
  setLoading: (loading: boolean) => void;
  loadingText: string;
  setLoadingText: (text: string) => void;
}

export const LoadingContext = createContext<LoadingContextValue | undefined>(undefined);
