import { Link } from 'react-router-dom';

export function NotFoundPage({
  title = 'Page not found',
  message = 'The page you were looking for does not exist.',
}: {
  title?: string;
  message?: string;
}) {
  return (
    <div className="card mx-auto max-w-lg p-10 text-center">
      <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
      <p className="mt-2 text-sm text-ink-500">{message}</p>
      <Link to="/products" className="btn mt-6">
        Back to products
      </Link>
    </div>
  );
}
