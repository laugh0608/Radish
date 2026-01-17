import { RouterProvider } from 'react-router-dom';
import { UserProvider } from './contexts/UserContext';
import { ErrorBoundary } from './components/ErrorBoundary';
import { router } from './router';
import './App.css';

function App() {
  return (
    <ErrorBoundary>
      <UserProvider>
        <RouterProvider router={router} />
      </UserProvider>
    </ErrorBoundary>
  );
}

export default App;
