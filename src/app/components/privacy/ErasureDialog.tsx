// Erasing a person: first what it would do, read from the server without changing anything
// (what goes, what is anonymised and stays in the books, what is cancelled, what is kept and
// why), then the erasure itself, which takes the word ERASE typed out. It cannot be undone.
// The reference staff give is theirs (a letter or ticket number) and is kept in the log, so
// the server refuses one with an email address in it.

import { useState } from 'react';
import { AlertTriangle, CheckCircle2, Loader2 } from 'lucide-react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { LoadFailed } from '@/app/components/LoadFailed';
import { erasePerson, type ErasureLine, type ErasureResult, type PersonSelection } from '@/app/api/privacy';
import { useErasurePreview } from '@/app/hooks/queries';
import { cn } from '@/app/components/ui/utils';

const ACTION: Record<ErasureLine['action'], { label: string; tone: string }> = {
  delete: { label: 'Deleted', tone: 'bg-rose-100 text-rose-800' },
  anonymise: { label: 'Anonymised', tone: 'bg-amber-100 text-amber-900' },
  cancel: { label: 'Cancelled', tone: 'bg-sky-100 text-sky-800' },
  keep: { label: 'Kept', tone: 'bg-slate-100 text-slate-700' },
};

export const CONFIRM_WORD = 'ERASE';

function Lines({ lines }: { lines: ErasureLine[] }) {
  if (lines.length === 0) return <p className="text-sm text-slate-600">Nothing else is held about them.</p>;
  return (
    <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
      {lines.map((line, i) => (
        <li key={`${i}-${line.area}-${line.action}`} className="flex items-start gap-3 px-3 py-2 text-sm">
          <span className={cn('mt-0.5 shrink-0 rounded-md px-1.5 py-0.5 text-xs font-semibold', ACTION[line.action]?.tone ?? ACTION.keep.tone)}>
            {ACTION[line.action]?.label ?? line.action}
          </span>
          <span className="min-w-0 flex-1 text-slate-700">
            <span className="font-medium text-slate-900">{line.area}</span>
            <span className="text-slate-500"> · {line.count}</span>
            {line.note && <span className="block text-xs text-slate-500">{line.note}</span>}
          </span>
        </li>
      ))}
    </ul>
  );
}

export function ErasureDialog({
  selection, onClose, onErased,
}: {
  /** What to erase; null while the dialog is closed. */
  selection: PersonSelection | null;
  onClose: () => void;
  onErased: (result: ErasureResult) => void;
}) {
  const plan = useErasurePreview(selection);
  const [confirm, setConfirm] = useState('');
  const [reference, setReference] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ErasureResult | null>(null);

  const close = () => {
    if (busy) return;
    setConfirm('');
    setReference('');
    setError(null);
    setResult(null);
    onClose();
  };

  const erase = async () => {
    if (!selection) return;
    setBusy(true);
    setError(null);
    try {
      const done = await erasePerson(selection, confirm, reference);
      setResult(done);
      onErased(done);
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : 'The erasure did not go through. Nothing was changed.');
    } finally {
      setBusy(false);
    }
  };

  const ready = !!plan.data && confirm.trim() === CONFIRM_WORD && !busy;

  return (
    <Dialog open={selection !== null} onOpenChange={(open) => { if (!open) close(); }}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        {result ? (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5 text-emerald-600" aria-hidden="true" />
                Erased
              </DialogTitle>
              <DialogDescription>
                What the books keep now reads &ldquo;{result.pseudonym}&rdquo;. This is erasure {result.number} in the log, which
                names nobody.
              </DialogDescription>
            </DialogHeader>
            <Lines lines={result.lines} />
            <DialogFooter>
              <Button type="button" onClick={close}>Done</Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>Erase this person</DialogTitle>
              <DialogDescription>
                This is what an erasure would do. Nothing has changed yet, and once done it cannot be undone.
              </DialogDescription>
            </DialogHeader>

            {plan.isPending ? (
              <p className="flex items-center gap-2 text-sm text-slate-600" role="status">
                <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" /> Working out what an erasure would do…
              </p>
            ) : plan.isError ? (
              <LoadFailed what="What an erasure would do" error={plan.error} onRetry={() => void plan.refetch()} retrying={plan.isFetching} />
            ) : (
              <div className="space-y-4">
                {plan.data.warnings.length > 0 && (
                  <ul className="space-y-1 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                    {plan.data.warnings.map((w) => (
                      <li key={w} className="flex gap-2">
                        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                        <span>{w}</span>
                      </li>
                    ))}
                  </ul>
                )}
                <Lines lines={plan.data.lines} />
                {plan.data.addressesSuppressed.length > 0 && (
                  <p className="text-sm text-slate-600">
                    {plan.data.addressesSuppressed.join(', ')} {plan.data.addressesSuppressed.length === 1 ? 'goes' : 'go'} on the
                    suppression list as a one-way fingerprint: nothing is emailed there again, and an import or a new member
                    with {plan.data.addressesSuppressed.length === 1 ? 'it' : 'one of them'} is stopped and said so.
                  </p>
                )}
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1">
                    <Label htmlFor="erase-reference">Your reference (optional)</Label>
                    <Input
                      id="erase-reference"
                      value={reference}
                      maxLength={200}
                      onChange={(e) => setReference(e.target.value)}
                      placeholder="e.g. letter of 3 May, ticket 1042"
                    />
                    <p className="text-xs text-slate-500">Kept in the log. Never their name or email.</p>
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="erase-confirm">Type {CONFIRM_WORD} to confirm</Label>
                    <Input
                      id="erase-confirm"
                      value={confirm}
                      autoComplete="off"
                      onChange={(e) => setConfirm(e.target.value)}
                    />
                  </div>
                </div>
              </div>
            )}
            {error && <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</p>}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={close} disabled={busy}>Cancel</Button>
              <Button type="button" variant="destructive" onClick={() => void erase()} disabled={!ready}>
                {busy && <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" />}
                Erase for good
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
