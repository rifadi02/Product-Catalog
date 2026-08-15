import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { can, type Capability } from './permissions';
import { useAuth } from './useAuth';
import { FullPageSpinner } from '../components/ui/Spinner';
import { ForbiddenPage } from '../pages/ForbiddenPage';

/**
 * §5.6 — route guard.
 *
 * The `isBooting` check is load-bearing: without it, reloading the page on `/products/new`
 * redirects to `/login` before the refresh-and-rehydrate sequence (§2.7) has had a chance to
 * restore the session.
 *
 * Hiding a route is cosmetic, not a security control. Anyone can call the API directly, so every
 * mutation path still handles 403 (§5.4).
 */
export function RequireCapability({
  capability,
  children,
}: {
  capability: Capability;
  children: ReactNode;
}) {
  const { user, isBooting } = useAuth();
  const location = useLocation();

  if (isBooting) return <FullPageSpinner label="Restoring your session" />;

  if (!can(capability, user)) {
    return user ? (
      <ForbiddenPage /> // signed in, wrong role
    ) : (
      <Navigate to="/login" state={{ from: location }} replace />
    );
  }

  return <>{children}</>;
}

/**
 * The `Authenticated` policy (§5.2) — role-agnostic. Kept distinct from `capability="write"`
 * even though both currently mean "signed in": if the backend ever narrows CanWriteProducts to
 * Admin, `/profile` must not narrow with it.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, isBooting } = useAuth();
  const location = useLocation();

  if (isBooting) return <FullPageSpinner label="Restoring your session" />;
  if (!user) return <Navigate to="/login" state={{ from: location }} replace />;

  return <>{children}</>;
}

/** `/login` and `/register` bounce to the catalogue when a session already exists (§3.0). */
export function RedirectIfAuthenticated({ children }: { children: ReactNode }) {
  const { user, isBooting } = useAuth();

  if (isBooting) return <FullPageSpinner label="Restoring your session" />;
  if (user) return <Navigate to="/products" replace />;

  return <>{children}</>;
}
