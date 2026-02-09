import { useEffect } from 'react';
import { RouterProvider } from 'react-router-dom';
import { UserProvider } from './contexts/UserContext';
import { LoadingProvider } from './contexts/LoadingContext';
import { ErrorBoundary } from './components/ErrorBoundary';
import { GlobalLoading } from './components/GlobalLoading';
import { setupApiInterceptors } from './services/apiInterceptor';
import { router } from './router';
import './App.css';

function App() {
  // 初始化 API 拦截器
  useEffect(() => {
    setupApiInterceptors();
  }, []);

  return (
    <ErrorBoundary>
      <LoadingProvider>
        <UserProvider>
          <RouterProvider router={router} />
          <GlobalLoading />
        </UserProvider>
      </LoadingProvider>
    </ErrorBoundary>
  );
}

export default App;
