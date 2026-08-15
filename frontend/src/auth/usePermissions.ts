/**
 * The capability hook — PDR §5.3. Components ask what they may show; they never read a token
 * or compare a role string themselves.
 */
import { useMemo } from 'react';
import { can, type HasLinks } from './permissions';
import { useAuth } from './useAuth';

export interface Permissions {
  canRead: boolean;
  canWrite: boolean;
  canDelete: boolean;
  isAuthenticated: boolean;
}

export function usePermissions(resource?: HasLinks): Permissions {
  const { user } = useAuth();

  return useMemo(
    () => ({
      canRead: can('read', user, resource),
      canWrite: can('write', user, resource),
      canDelete: can('delete', user, resource),
      isAuthenticated: user !== null,
    }),
    [user, resource],
  );
}
