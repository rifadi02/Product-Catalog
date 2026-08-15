/** The permission matrix — PDR §5.2, §5.3. */
import { describe, expect, it } from 'vitest';
import { can, describeRole } from './permissions';

const ANONYMOUS = null;
const USER = { role: 'User' } as const;
const ADMIN = { role: 'Admin' } as const;

describe('can()', () => {
  it('lets anyone read — the catalogue is public', () => {
    // CanReadProducts always passes, even anonymous.
    expect(can('read', ANONYMOUS)).toBe(true);
    expect(can('read', USER)).toBe(true);
    expect(can('read', ADMIN)).toBe(true);
  });

  it('requires a session to write', () => {
    expect(can('write', ANONYMOUS)).toBe(false);
    expect(can('write', USER)).toBe(true);
    expect(can('write', ADMIN)).toBe(true);
  });

  it('restricts delete to Admin', () => {
    // The 401-vs-403 distinction: anonymous gets 401, an authenticated User gets 403 (§5.2).
    expect(can('delete', ANONYMOUS)).toBe(false);
    expect(can('delete', USER)).toBe(false);
    expect(can('delete', ADMIN)).toBe(true);
  });

  describe('forward compatibility with _links (§7)', () => {
    it('prefers server-supplied links over role inference', () => {
      const resource = {
        _links: {
          self: { href: '/api/v1/products/42', method: 'GET' },
          update: { href: '/api/v1/products/42', method: 'PUT' },
        },
      };

      // An Admin would infer delete: true, but the server said otherwise, and it wins.
      expect(can('delete', ADMIN, resource)).toBe(false);
      expect(can('write', ADMIN, resource)).toBe(true);
      expect(can('read', ADMIN, resource)).toBe(true);
    });

    it('grants from links even without a user', () => {
      const resource = {
        _links: { delete: { href: '/api/v1/products/42', method: 'DELETE' } },
      };
      expect(can('delete', ANONYMOUS, resource)).toBe(true);
    });

    it('falls back to inference when a resource carries no links', () => {
      expect(can('delete', ADMIN, {})).toBe(true);
    });
  });
});

describe('describeRole', () => {
  it('describes what an Admin may do', () => {
    expect(describeRole('Admin')).toEqual({
      label: 'Administrator',
      capabilities: expect.arrayContaining(['Delete products']),
    });
  });

  it('states plainly that a standard user cannot delete', () => {
    const role = describeRole('User');
    expect(role.label).toBe('Standard user');
    expect(role.capabilities).toContain('Cannot delete products');
    expect(role.capabilities).not.toContain('Delete products');
  });
});
