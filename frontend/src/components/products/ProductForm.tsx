/**
 * The create/edit form — PDR §3.6.5. One component; the differences between create and edit are
 * the endpoint, the initial values, and the `If-Match` header, all owned by the caller.
 */
import { useState } from 'react';
import type { ProductFormValues } from '../../lib/schemas';
import type { ProductFormApi } from './useProductForm';
import {
  DESCRIPTION_COUNTER_FROM,
  DESCRIPTION_MAX,
  NAME_COUNTER_FROM,
  NAME_MAX,
  PRICE_MAX,
} from '../../lib/constants';
import { roundTo2dp } from '../../lib/format';
import { Button } from '../ui/Button';
import { CharCounter, Textarea, TextInput } from '../ui/Field';
import { Modal } from '../ui/Modal';

export function ProductForm({
  form,
  submitLabel,
  isPending,
  onSubmit,
  onCancel,
  /** Edit disables submit while the form is unmodified; create never does. */
  disableWhenPristine = false,
  children,
}: {
  form: ProductFormApi;
  submitLabel: string;
  isPending: boolean;
  onSubmit: (values: ProductFormValues) => void;
  onCancel: () => void;
  disableWhenPristine?: boolean;
  children?: React.ReactNode;
}) {
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = form;

  const [confirmDiscard, setConfirmDiscard] = useState(false);

  const name = watch('name');
  const description = watch('description');

  function requestCancel() {
    if (isDirty) setConfirmDiscard(true);
    else onCancel();
  }

  return (
    <>
      <form className="card space-y-5 p-6" onSubmit={handleSubmit(onSubmit)} noValidate>
        {children}

        <TextInput
          label="Name"
          required
          maxLength={NAME_MAX}
          autoFocus
          error={errors.name?.message}
          adornment={<CharCounter value={name} max={NAME_MAX} from={NAME_COUNTER_FROM} />}
          {...register('name')}
        />

        <Textarea
          label="Description"
          rows={6}
          maxLength={DESCRIPTION_MAX}
          hint="Optional. Leave blank for no description."
          error={errors.description?.message}
          adornment={
            <CharCounter
              value={description}
              max={DESCRIPTION_MAX}
              from={DESCRIPTION_COUNTER_FROM}
            />
          }
          {...register('description')}
        />

        <div className="max-w-48">
          <TextInput
            label="Price"
            required
            type="number"
            step="0.01"
            min="0"
            max={PRICE_MAX}
            inputMode="decimal"
            className="num"
            error={errors.price?.message}
            {...register('price', {
              // Prices are rounded to 2dp server-side with banker's rounding, so the displayed
              // value is aligned to what will actually be stored (§4.3).
              onBlur: (event: React.FocusEvent<HTMLInputElement>) => {
                const raw = event.target.value.trim();
                if (raw === '' || !Number.isFinite(Number(raw))) return;

                const rounded = roundTo2dp(Number(raw)).toFixed(2);
                if (rounded !== raw) setValue('price', rounded, { shouldValidate: true });
              },
            })}
          />
        </div>

        <div className="flex justify-end gap-2 border-t border-slate-100 pt-5">
          <Button variant="secondary" onClick={requestCancel} disabled={isPending}>
            Cancel
          </Button>
          <Button
            type="submit"
            isPending={isPending}
            disabled={disableWhenPristine && !isDirty}
          >
            {submitLabel}
          </Button>
        </div>
      </form>

      <Modal
        open={confirmDiscard}
        title="Discard your changes?"
        onClose={() => setConfirmDiscard(false)}
      >
        <p>Your edits to this product have not been saved.</p>
        <div className="mt-6 flex justify-end gap-2">
          <Button variant="secondary" data-autofocus onClick={() => setConfirmDiscard(false)}>
            Keep editing
          </Button>
          <Button variant="danger" onClick={onCancel}>
            Discard
          </Button>
        </div>
      </Modal>
    </>
  );
}
