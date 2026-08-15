/**
 * §3.6.4 — the 409 conflict resolver. A concurrency conflict is a designed-for state, not an
 * edge case: the user's edits stay in the form, both versions are shown field by field, and they
 * choose which one wins.
 */
import { formatPrice } from '../../lib/format';
import type { Product } from '../../api/types';
import type { ProductFormValues } from '../../lib/schemas';
import { Button } from '../ui/Button';
import { Modal } from '../ui/Modal';

interface Row {
  label: string;
  theirs: string;
  mine: string;
}

function buildRows(theirs: Product, mine: ProductFormValues): Row[] {
  const minePrice = Number(mine.price);

  return [
    { label: 'Name', theirs: theirs.name, mine: mine.name.trim() },
    {
      label: 'Description',
      theirs: theirs.description ?? '—',
      mine: mine.description.trim() || '—',
    },
    {
      label: 'Price',
      theirs: formatPrice(theirs.price),
      mine: Number.isFinite(minePrice) ? formatPrice(minePrice) : mine.price,
    },
  ];
}

export function ConflictModal({
  open,
  theirs,
  mine,
  isPending,
  onKeepMine,
  onDiscardMine,
  onClose,
}: {
  open: boolean;
  theirs: Product | null;
  mine: ProductFormValues;
  isPending: boolean;
  onKeepMine: () => void;
  onDiscardMine: () => void;
  onClose: () => void;
}) {
  if (!theirs) return null;

  const rows = buildRows(theirs, mine);

  return (
    <Modal open={open} title="This product changed while you were editing" onClose={onClose} size="lg">
      <p>
        Someone else saved changes to this product after you opened it. Compare the two versions
        and choose which one to keep.
      </p>

      <div className="mt-4 overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="text-xs uppercase tracking-wide text-ink-500">
            <tr>
              <th scope="col" className="py-2 pr-4 font-medium">
                Field
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                On the server
              </th>
              <th scope="col" className="py-2 font-medium">
                Your version
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => {
              const differs = row.theirs !== row.mine;
              return (
                <tr key={row.label} className="border-t border-slate-100 align-top">
                  <th scope="row" className="py-2 pr-4 font-medium text-ink-700">
                    {row.label}
                  </th>
                  <td className={`py-2 pr-4 break-words ${differs ? 'bg-amber-50' : ''}`}>
                    {row.theirs}
                  </td>
                  <td className={`py-2 break-words ${differs ? 'bg-brand-50 font-medium' : ''}`}>
                    {row.mine}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="mt-6 flex flex-wrap justify-end gap-2">
        {/* Discard is the non-destructive default here: it restores the server's truth. */}
        <Button variant="secondary" data-autofocus onClick={onDiscardMine} disabled={isPending}>
          Discard mine
        </Button>
        <Button onClick={onKeepMine} isPending={isPending}>
          Keep my changes
        </Button>
      </div>
    </Modal>
  );
}
