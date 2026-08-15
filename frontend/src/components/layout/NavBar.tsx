/**
 * §5.5 — nav items are visibility-filtered by the same capability helper the buttons use.
 *
 * Anonymous visitors see a working catalogue plus a "Sign in" link. Anonymous browsing is a
 * supported mode, not a degraded one.
 */
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/useAuth';
import { usePermissions } from '../../auth/usePermissions';
import { Badge } from '../ui/Badge';
import { Button } from '../ui/Button';

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-md px-3 py-2 text-sm font-medium transition ${
    isActive ? 'bg-brand-50 text-brand-700' : 'text-ink-700 hover:bg-slate-100'
  }`;

export function NavBar() {
  const { user, signOut } = useAuth();
  const { canWrite, isAuthenticated } = usePermissions();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  async function handleSignOut() {
    await signOut();
    // Product data is cached, and skipping this leaks the previous session's data into the next
    // login on a shared machine (§2.8).
    queryClient.clear();
    navigate('/login', { replace: true });
  }

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
        <div className="flex items-center gap-1">
          <Link to="/products" className="mr-3 text-base font-semibold tracking-tight">
            Product Catalog
          </Link>
          <NavLink to="/products" className={linkClass}>
            Products
          </NavLink>
          {canWrite && (
            <NavLink to="/products/new" className={linkClass}>
              New product
            </NavLink>
          )}
          {isAuthenticated && (
            <NavLink to="/profile" className={linkClass}>
              Profile
            </NavLink>
          )}
        </div>

        <div className="flex items-center gap-3">
          {isAuthenticated ? (
            <>
              {/* There is no `name` claim in this system — email is the only user label (§5.1). */}
              <span className="hidden text-sm text-ink-500 sm:inline">{user?.email}</span>
              {user?.role === 'Admin' && <Badge tone="admin">Admin</Badge>}
              <Button variant="secondary" size="sm" onClick={handleSignOut}>
                Sign out
              </Button>
            </>
          ) : (
            <Link to="/login" className="btn btn-sm">
              Sign in
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
