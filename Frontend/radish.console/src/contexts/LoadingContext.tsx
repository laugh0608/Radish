import { useState, type ReactNode } from 'react';
import { LoadingContext } from './loadingContextValue';

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
