import { Link } from 'react-router-dom';

/**
 * §5.2, §5.6 — reached when a signed-in user lacks the role for a route. The 401-vs-403
 * distinction matters: anonymous visitors are sent to `/login` instead, because they might
 * succeed after signing in. This user will not.
 */
export function ForbiddenPage() {
  return (
    <div className="card mx-auto max-w-lg p-10 text-center">
      <h1 className="text-2xl font-semibold tracking-tight">You don&rsquo;t have access</h1>
      <p className="mt-2 text-sm text-ink-500">
        Your account doesn&rsquo;t have permission to view this page. If you think it should, ask
        an administrator.
      </p>
      <Link to="/products" className="btn mt-6">
        Back to products
      </Link>
    </div>
  );
}
