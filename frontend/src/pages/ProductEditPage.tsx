/** Edit — PDR §3.6.2–§3.6.4, `PUT /api/v1/products/{id}` with `If-Match`. */
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useParams } from 'react-router-dom';
import { applyServerErrors, describeError, getProblem } from '../api/problem';
import type { Product } from '../api/types';
import { useProduct, useUpdateProduct } from '../hooks/useProducts';
import { useToast } from '../components/ui/useToast';
import { toWriteRequest, type ProductFormValues } from '../lib/schemas';
import { Alert } from '../components/ui/Alert';
import { Button } from '../components/ui/Button';
import { Modal } from '../components/ui/Modal';
import { Skeleton } from '../components/ui/Skeleton';
import { ErrorState } from '../components/ErrorState';
import { ProductForm } from '../components/products/ProductForm';
import { toFormValues, useProductForm } from '../components/products/useProductForm';
import { ConflictModal } from '../components/products/ConflictModal';
import { NotFoundPage } from './NotFoundPage';
import { parseProductId } from '../lib/productId';

export function ProductEditPage() {
  const { id: rawId } = useParams();
  const id = parseProductId(rawId);

  const { data, error, isPending, refetch } = useProduct(id ?? 0, id !== null);

  if (id === null) return <NotFoundPage />;

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
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-10 w-40" />
      </div>
    );
  }

  if (error || !data) return <ErrorState error={error} onRetry={() => void refetch()} />;

  // Keyed on the product id so navigating between edit pages re-seeds the form rather than
  // carrying one product's values into another.
  return <EditForm key={id} id={id} product={data.product} refetchDetail={refetch} />;
}

function EditForm({
  id,
  product,
  refetchDetail,
}: {
  id: number;
  product: Product;
  refetchDetail: () => Promise<{ data?: { product: Product } | undefined }>;
}) {
  const navigate = useNavigate();
  const toast = useToast();
  const form = useProductForm(toFormValues(product));
  const updateMutation = useUpdateProduct(id);

  const [formError, setFormError] = useState<{ message: string; traceId?: string } | null>(null);
  const [conflictWith, setConflictWith] = useState<Product | null>(null);
  const [goneModal, setGoneModal] = useState(false);

  function submit(values: ProductFormValues) {
    setFormError(null);

    // A full replacement, not a patch: all three fields go every time, or an omitted description
    // becomes null (§3.6.2). The ETag is resolved inside the mutation (§3.6.3).
    updateMutation.mutate(toWriteRequest(values), {
      onSuccess: () => {
        setConflictWith(null);
        toast.success('Changes saved.');
        navigate(`/products/${id}`, { replace: true });
      },
      onError: async (error) => {
        if (applyServerErrors(getProblem(error), form.setError)) return;

        const info = describeError(error);
        if (info.silent) return;

        switch (info.code) {
          case 'concurrency_conflict':
          case 'precondition_required': {
            // Refetching replaces the stale ETag in cache and gives us the server's current
            // values to compare against. The form stays mounted, so the user's edits survive.
            const fresh = await refetchDetail();
            setConflictWith(fresh.data?.product ?? null);
            return;
          }

          case 'resource_not_found':
            setGoneModal(true);
            return;

          case 'insufficient_role':
            toast.error(info.message, info.traceId);
            return;

          default:
            setFormError({ message: info.message, traceId: info.traceId });
        }
      },
    });
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <Link to={`/products/${id}`} className="text-sm font-medium text-brand-700 hover:underline">
        ← Back to product
      </Link>

      <h1 className="flex flex-wrap items-baseline gap-2 text-2xl font-semibold tracking-tight">
        Edit product
        <span className="font-mono text-sm font-normal text-ink-500">#{id}</span>
      </h1>

      <ProductForm
        form={form}
        submitLabel="Save changes"
        isPending={updateMutation.isPending}
        onSubmit={submit}
        onCancel={() => navigate(`/products/${id}`)}
        disableWhenPristine
      >
        {formError && (
          <Alert tone="error" traceId={formError.traceId}>
            {formError.message}
          </Alert>
        )}
      </ProductForm>

      <ConflictModal
        open={conflictWith !== null}
        theirs={conflictWith}
        mine={form.getValues()}
        isPending={updateMutation.isPending}
        onClose={() => setConflictWith(null)}
        // Resubmits against the ETag the refetch above just put in cache.
        onKeepMine={() => submit(form.getValues())}
        onDiscardMine={() => {
          if (conflictWith) form.reset(toFormValues(conflictWith));
          setConflictWith(null);
        }}
      />

      <Modal
        open={goneModal}
        title="This product no longer exists"
        onClose={() => navigate('/products', { replace: true })}
      >
        <p>It was deleted while you were editing. Your changes cannot be saved.</p>
        <div className="mt-6 flex justify-end">
          <Button data-autofocus onClick={() => navigate('/products', { replace: true })}>
            Back to products
          </Button>
        </div>
      </Modal>
    </div>
  );
}
