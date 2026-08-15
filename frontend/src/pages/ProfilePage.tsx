/**
 * Profile — PDR §3.8.
 *
 * A read-only card. This API exposes no profile mutations — no password change, no email change,
 * no avatar — so there are no inputs here. Building them would advertise capabilities that do
 * not exist.
 */
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth/useAuth';
import { describeRole } from '../auth/permissions';
import { Badge } from '../components/ui/Badge';
import { Button } from '../components/ui/Button';
import { FullPageSpinner } from '../components/ui/Spinner';

export function ProfilePage() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // The route guard has already established a session; this is belt-and-braces for the frame in
  // which context has not settled.
  if (!user) return <FullPageSpinner />;

  const role = describeRole(user.role);

  async function handleSignOut() {
    await signOut();
    queryClient.clear(); // §2.8 — product data is cached; skipping this leaks it into the next login
    navigate('/login', { replace: true });
  }

  return (
    <div className="mx-auto max-w-xl space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight">Profile</h1>

      <div className="card divide-y divide-slate-100">
        <dl className="space-y-4 p-6 text-sm">
          <div>
            {/* There is no `name` claim in this system — email is the only user label (§5.1). */}
            <dt className="text-ink-500">Email</dt>
            <dd className="mt-1 font-medium">{user.email}</dd>
          </div>

          <div>
            <dt className="text-ink-500">Role</dt>
            <dd className="mt-1">
              <Badge tone={user.role === 'Admin' ? 'admin' : 'brand'}>{role.label}</Badge>
            </dd>
          </div>

          <div>
            <dt className="text-ink-500">User id</dt>
            <dd className="mt-1 font-mono text-xs break-all text-ink-700 select-all">{user.id}</dd>
          </div>

          <div>
            <dt className="text-ink-500">Permissions</dt>
            <dd className="mt-2">
              <ul className="space-y-1">
                {role.capabilities.map((capability) => (
                  <li key={capability} className="text-ink-700">
                    <span aria-hidden="true">•</span> {capability}
                  </li>
                ))}
              </ul>
            </dd>
          </div>
        </dl>

        <div className="flex justify-end p-4">
          <Button variant="secondary" onClick={handleSignOut}>
            Sign out
          </Button>
        </div>
      </div>
    </div>
  );
}
