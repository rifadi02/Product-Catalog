/** Create — PDR §3.6.1, `POST /api/v1/products`. Policy: CanWriteProducts. */
import { Link, useNavigate } from 'react-router-dom';
import { applyServerErrors, describeError, getProblem } from '../api/problem';
import { useCreateProduct } from '../hooks/useProducts';
import { useToast } from '../components/ui/useToast';
import { toWriteRequest, type ProductFormValues } from '../lib/schemas';
import { Alert } from '../components/ui/Alert';
import { ProductForm } from '../components/products/ProductForm';
import { EMPTY_PRODUCT_FORM, useProductForm } from '../components/products/useProductForm';
import { useState } from 'react';

export function ProductCreatePage() {
  const navigate = useNavigate();
  const toast = useToast();
  const form = useProductForm(EMPTY_PRODUCT_FORM);
  const createMutation = useCreateProduct();
  const [formError, setFormError] = useState<{ message: string; traceId?: string } | null>(null);

  function onSubmit(values: ProductFormValues) {
    setFormError(null);

    createMutation.mutate(toWriteRequest(values), {
      onSuccess: (product) => {
        toast.success('Product created.');
        // The id comes from the body rather than the Location header — simpler, and always present.
        navigate(`/products/${product.id}`, { replace: true });
      },
      onError: (error) => {
        if (applyServerErrors(getProblem(error), form.setError)) return;

        const info = describeError(error);
        if (info.silent) return;

        // Should be unreachable behind the route guard, but the hidden button is cosmetic and the
        // server is the gate (§5.4).
        if (info.code === 'insufficient_role') {
          toast.error(info.message, info.traceId);
          return;
        }

        setFormError({ message: info.message, traceId: info.traceId });
      },
    });
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <Link to="/products" className="text-sm font-medium text-brand-700 hover:underline">
        ← Back to products
      </Link>

      <h1 className="text-2xl font-semibold tracking-tight">New product</h1>

      <ProductForm
        form={form}
        submitLabel="Create product"
        isPending={createMutation.isPending}
        onSubmit={onSubmit}
        onCancel={() => navigate('/products')}
      >
        {formError && (
          <Alert tone="error" traceId={formError.traceId}>
            {formError.message}
          </Alert>
        )}
      </ProductForm>
    </div>
  );
}
