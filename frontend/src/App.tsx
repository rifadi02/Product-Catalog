import { Outlet, RouterProvider, createBrowserRouter } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './auth/AuthContext';
import { SessionEndedListener } from './auth/SessionEndedListener';
import { ToastProvider } from './components/ui/ToastProvider';
import { createQueryClient } from './api/queryClient';
import { routes } from './routes';

/**
 * Everything that must live inside the router but outside every page: the session-ended listener
 * (it navigates) and the toast region (§2.6, §1.2).
 */
function RootShell() {
  return (
    <>
      <SessionEndedListener />
      <ToastProvider>
        <Outlet />
      </ToastProvider>
    </>
  );
}

const router = createBrowserRouter([{ element: <RootShell />, children: routes }]);
const queryClient = createQueryClient();

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      {/* The boot sequence (§2.7) starts here and needs no router context. */}
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>
  );
}
