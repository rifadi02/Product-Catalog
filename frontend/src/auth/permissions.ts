/**
 * The permission model — PDR §5.
 *
 * There is no permissions endpoint and no hypermedia in this API, so the single source of truth
 * is the `role` claim in the access token. `can()` is nonetheless written to prefer `_links`
 * when they appear (§7), making that migration a one-function change rather than a refactor.
 */
import type { UserRole } from '../api/types';

export type Capability = 'read' | 'write' | 'delete';

export interface HasLinks {
  _links?: Record<string, { href: string; method: string }>;
}

const RELS: Record<Capability, string> = {
  read: 'self',
  write: 'update',
  delete: 'delete',
};

export function can(
  capability: Capability,
  user: { role: UserRole } | null,
  resource?: HasLinks,
): boolean {
  // Forward-compatible: when the backend starts emitting _links, they win outright — the server
  // has said what this caller may do with this resource, which beats inference.
  if (resource?._links) return RELS[capability] in resource._links;

  switch (capability) {
    case 'read':
      return true; // the catalogue is public — CanReadProducts always passes
    case 'write':
      return user !== null; // CanWriteProducts — User or Admin
    case 'delete':
      return user?.role === 'Admin'; // CanDeleteProducts — Admin only
  }
}

/** The §5.2 matrix, rendered as prose for the profile page. */
export function describeRole(role: UserRole): { label: string; capabilities: string[] } {
  const shared = ['Browse and search the catalogue', 'Create products', 'Edit any product'];

  return role === 'Admin'
    ? { label: 'Administrator', capabilities: [...shared, 'Delete products'] }
    : { label: 'Standard user', capabilities: [...shared, 'Cannot delete products'] };
}
