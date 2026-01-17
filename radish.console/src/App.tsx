import { RouterProvider } from 'react-router-dom';
import { UserProvider } from './contexts/UserContext';
import { router } from './router';
import './App.css';

function App() {
  return (
    <UserProvider>
      <RouterProvider router={router} />
    </UserProvider>
  );
}

export default App;
