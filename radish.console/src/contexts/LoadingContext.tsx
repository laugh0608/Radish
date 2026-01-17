import { createContext, useContext, useState, type ReactNode } from 'react';

interface LoadingContextValue {
  loading: boolean;
  setLoading: (loading: boolean) => void;
  loadingText: string;
  setLoadingText: (text: string) => void;
}

const LoadingContext = createContext<LoadingContextValue | undefined>(undefined);

export interface LoadingProviderProps {
  children: ReactNode;
}

/**
 * 全局加载状态提供者
 */
export function LoadingProvider({ children }: LoadingProviderProps) {
  const [loading, setLoading] = useState(false);
  const [loadingText, setLoadingText] = useState('加载中...');

  return (
    <LoadingContext.Provider value={{ loading, setLoading, loadingText, setLoadingText }}>
      {children}
    </LoadingContext.Provider>
  );
}

/**
 * 使用全局加载状态 Hook
 */
export function useLoading() {
  const context = useContext(LoadingContext);
  if (context === undefined) {
    throw new Error('useLoading must be used within a LoadingProvider');
  }
  return context;
}
