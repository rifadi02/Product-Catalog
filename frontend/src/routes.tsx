/**
 * Route map — PDR §3.0.
 *
 * Reads are anonymous, so `/products` is the landing page and the catalogue browses without a
 * login. Only writes are guarded. That is a deliberate product decision in the backend, not an
 * oversight.
 */
import { Navigate, type RouteObject } from 'react-router-dom';
import { AppLayout } from './components/layout/AppLayout';
import { RedirectIfAuthenticated, RequireAuth, RequireCapability } from './auth/RequireCapability';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { ProductListPage } from './pages/ProductListPage';
import { ProductDetailPage } from './pages/ProductDetailPage';
import { ProductCreatePage } from './pages/ProductCreatePage';
import { ProductEditPage } from './pages/ProductEditPage';
import { ProfilePage } from './pages/ProfilePage';
import { NotFoundPage } from './pages/NotFoundPage';

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/products" replace /> },

      {
        path: 'login',
        element: (
          <RedirectIfAuthenticated>
            <LoginPage />
          </RedirectIfAuthenticated>
        ),
      },
      {
        path: 'register',
        element: (
          <RedirectIfAuthenticated>
            <RegisterPage />
          </RedirectIfAuthenticated>
        ),
      },

      { path: 'products', element: <ProductListPage /> },

      // Declared before `products/:id` so `new` is never parsed as an id.
      {
        path: 'products/new',
        element: (
          <RequireCapability capability="write">
            <ProductCreatePage />
          </RequireCapability>
        ),
      },
      { path: 'products/:id', element: <ProductDetailPage /> },
      {
        path: 'products/:id/edit',
        element: (
          <RequireCapability capability="write">
            <ProductEditPage />
          </RequireCapability>
        ),
      },

      {
        path: 'profile',
        element: (
          <RequireAuth>
            <ProfilePage />
          </RequireAuth>
        ),
      },

      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
