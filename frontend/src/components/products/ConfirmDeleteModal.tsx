import { Button } from '../ui/Button';
import { Modal } from '../ui/Modal';
import type { Product } from '../../api/types';

/** §3.7 — the delete confirmation, shared by the list and the detail page. */
export function ConfirmDeleteModal({
  product,
  isPending,
  onCancel,
  onConfirm,
}: {
  product: Product | null;
  isPending: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <Modal open={product !== null} title="Delete product?" onClose={onCancel}>
      <p>
        <strong className="font-semibold text-ink-900">{product?.name}</strong> will be removed
        from the catalogue. This can&rsquo;t be undone from here.
      </p>

      <div className="mt-6 flex justify-end gap-2">
        {/* Cancel is autofocused, so a reflexive Enter dismisses rather than deletes. */}
        <Button variant="secondary" onClick={onCancel} data-autofocus disabled={isPending}>
          Cancel
        </Button>
        <Button variant="danger" onClick={onConfirm} isPending={isPending}>
          Delete
        </Button>
      </div>
    </Modal>
  );
}
