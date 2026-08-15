/** Product detail — PDR §3.5. Public: no `Authorization` header required. */
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { describeError, getProblem } from '../api/problem';
import { useDeleteProduct, useProduct } from '../hooks/useProducts';
import { usePermissions } from '../auth/usePermissions';
import { useToast } from '../components/ui/useToast';
import { formatDateTime, formatPrice } from '../lib/format';
import { Button } from '../components/ui/Button';
import { Skeleton } from '../components/ui/Skeleton';
import { ErrorState } from '../components/ErrorState';
import { ConfirmDeleteModal } from '../components/products/ConfirmDeleteModal';
import { NotFoundPage } from './NotFoundPage';
import { parseProductId } from '../lib/productId';

export function ProductDetailPage() {
  const { id: rawId } = useParams();
  const id = parseProductId(rawId);
  const navigate = useNavigate();
  const toast = useToast();

  const { canWrite, canDelete } = usePermissions();
  const { data, error, isPending, refetch } = useProduct(id ?? 0, id !== null);
  const deleteMutation = useDeleteProduct();
  const [confirming, setConfirming] = useState(false);

  if (id === null) return <NotFoundPage />;

  // Soft-deleted products 404 identically to ones that never existed (§3.5).
  if (getProblem(error)?.code === 'resource_not_found') {
    return (
      <NotFoundPage
        title="Product not found"
        message="This product does not exist, or it has been removed from the catalogue."
      />
    );
  }

  if (isPending) {
    return (
      <div className="card space-y-4 p-6">
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-6 w-24" />
        <Skeleton className="h-24 w-full" />
      </div>
    );
  }

  if (error || !data) {
    return <ErrorState error={error} onRetry={() => void refetch()} />;
  }

  const { product } = data;

  function handleDelete() {
    deleteMutation.mutate(product.id, {
      onSuccess: ({ alreadyGone }) => {
        setConfirming(false);
        if (alreadyGone) toast.info(`“${product.name}” had already been removed.`);
        else toast.success(`“${product.name}” was deleted.`);
        navigate('/products', { replace: true });
      },
      onError: (deleteError) => {
        setConfirming(false);
        const info = describeError(deleteError);
        if (!info.silent) toast.error(info.message, info.traceId);
      },
    });
  }

  return (
    <div className="space-y-4">
      <Link to="/products" className="text-sm font-medium text-brand-700 hover:underline">
        ← Back to products
      </Link>

      <article className="card p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="flex flex-wrap items-baseline gap-2 text-2xl font-semibold tracking-tight">
              {product.name}
              <span className="font-mono text-sm font-normal text-ink-500">#{product.id}</span>
            </h1>
            <p className="mt-2 text-3xl font-semibold tabular-nums">
              {formatPrice(product.price)}
            </p>
          </div>

          <div className="flex gap-2">
            {canWrite && (
              <Link to={`/products/${product.id}/edit`} className="btn btn-secondary">
                Edit
              </Link>
            )}
            {canDelete && (
              <Button variant="danger" onClick={() => setConfirming(true)}>
                Delete
              </Button>
            )}
          </div>
        </div>

        <div className="mt-6 border-t border-slate-100 pt-6">
          <h2 className="text-sm font-medium text-ink-700">Description</h2>
          {product.description ? (
            // Up to 2000 characters, with line breaks preserved and long words broken so the
            // layout survives (§3.5).
            <p className="mt-2 max-w-prose text-sm break-words whitespace-pre-wrap text-ink-700">
              {product.description}
            </p>
          ) : (
            <p className="mt-2 text-sm text-ink-500 italic">No description.</p>
          )}
        </div>

        <dl className="mt-6 grid gap-4 border-t border-slate-100 pt-6 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-ink-500">Created</dt>
            <dd className="mt-1">{formatDateTime(product.createdAt)}</dd>
          </div>
          {/* The row is hidden entirely when null, rather than showing an em-dash (§3.5). */}
          {product.updatedAt && (
            <div>
              <dt className="text-ink-500">Last updated</dt>
              <dd className="mt-1">{formatDateTime(product.updatedAt)}</dd>
            </div>
          )}
        </dl>
      </article>

      <ConfirmDeleteModal
        product={confirming ? product : null}
        isPending={deleteMutation.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={handleDelete}
      />
    </div>
  );
}
