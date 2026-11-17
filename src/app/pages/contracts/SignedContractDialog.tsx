// A contract signed outside CadenceE, on paper or with another signing service: its signed copy
// is uploaded here, and that completes the contract step as a countersignature does — the deal
// is won and the member admitted. The copy is kept with the deal; nothing is emailed.

import { useRef, useState, type DragEvent } from 'react';
import { CheckCircle2, FileCheck2, Loader2, UploadCloud, X } from 'lucide-react';
import { toast } from 'sonner';
import {
  SIGNED_COPY_ACCEPT, SIGNED_COPY_MAX_BYTES, uploadSignedContract, type Contract,
} from '@/app/api/contracts';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { DateInput } from '@/app/components/DateInput';
import { cn } from '@/app/components/ui/utils';

const TYPES = /\.(pdf|jpe?g|png)$/i;

/** Today as "yyyy-mm-dd", in the person's own time zone. */
function today(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

function sizeOf(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** Why this file cannot be the signed copy, or null when it can. */
export function signedCopyProblem(file: File): string | null {
  if (!TYPES.test(file.name)) return 'Choose the signed contract as a PDF, or a JPEG or PNG photo of it.';
  if (file.size === 0) return 'That file is empty.';
  if (file.size > SIGNED_COPY_MAX_BYTES) return `That file is ${sizeOf(file.size)}: it can be up to 10 MB.`;
  return null;
}

export function SignedContractDialog({ open, onOpenChange, dealId, leadId, contract, counterpartyName, onRecorded }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  dealId?: string;
  leadId?: string;
  /** A contract drafted or sent here that was signed another way: the copy closes it. */
  contract?: Contract | null;
  /** Who it is with, to name the signer by default. */
  counterpartyName: string;
  onRecorded: (contract: Contract) => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [signedBy, setSignedBy] = useState(counterpartyName);
  const [signedOn, setSignedOn] = useState(today);
  const [dragging, setDragging] = useState(false);
  const [saving, setSaving] = useState(false);
  const picker = useRef<HTMLInputElement>(null);

  const reset = () => {
    setFile(null);
    setProblem(null);
    setSignedBy(counterpartyName);
    setSignedOn(today());
  };

  const choose = (picked: File | undefined | null) => {
    if (!picked) return;
    const why = signedCopyProblem(picked);
    setProblem(why);
    setFile(why ? null : picked);
  };

  const drop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragging(false);
    choose(e.dataTransfer.files?.[0]);
  };

  const record = async () => {
    if (!file) return;
    if (signedOn > today()) {
      setProblem('The day it was signed cannot be in the future.');
      return;
    }
    setSaving(true);
    try {
      const recorded = await uploadSignedContract({
        file,
        dealId: contract ? undefined : dealId,
        leadId: contract || dealId ? undefined : leadId,
        contractId: contract?.id,
        signedBy,
        signedOn: signedOn || undefined,
      });
      toast.success('Contract recorded as signed', { description: dealId ? 'The deal is marked won.' : undefined });
      onRecorded(recorded);
      onOpenChange(false);
      reset();
    } catch (e) {
      setProblem(e instanceof Error ? e.message : 'The signed contract could not be uploaded.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!saving) { onOpenChange(next); if (!next) reset(); } }}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileCheck2 className="h-5 w-5 text-emerald-600" aria-hidden /> Upload the signed contract
          </DialogTitle>
          <DialogDescription>
            {contract
              ? 'Signed on paper or another way? Its signed copy closes this contract.'
              : 'Signed on paper or with another signing service? Upload it here.'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div
            onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
            onDragLeave={() => setDragging(false)}
            onDrop={drop}
            className={cn(
              'rounded-xl border-2 border-dashed p-4 text-center transition-colors',
              dragging ? 'border-emerald-400 bg-emerald-50' : 'border-slate-200 bg-slate-50/60',
            )}
          >
            {file ? (
              <div className="flex items-center gap-3 text-left">
                <FileCheck2 className="h-8 w-8 shrink-0 text-emerald-600" aria-hidden />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-slate-900">{file.name}</p>
                  <p className="text-xs text-slate-500">{sizeOf(file.size)}</p>
                </div>
                <Button type="button" variant="ghost" size="sm" onClick={() => setFile(null)} aria-label="Remove the file">
                  <X className="h-4 w-4" aria-hidden />
                </Button>
              </div>
            ) : (
              <>
                <UploadCloud className="mx-auto h-8 w-8 text-slate-400" aria-hidden />
                <p className="mt-2 text-sm text-slate-600">Drop the signed contract here, or</p>
                <Button type="button" variant="outline" size="sm" className="mt-2" onClick={() => picker.current?.click()}>
                  Choose a file
                </Button>
                <p className="mt-2 text-[11px] text-slate-400">PDF, or a JPEG or PNG photo · up to 10 MB</p>
              </>
            )}
            <input
              ref={picker}
              type="file"
              accept={SIGNED_COPY_ACCEPT}
              className="sr-only"
              aria-label="The signed contract"
              onChange={(e) => { choose(e.target.files?.[0]); e.target.value = ''; }}
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="signed-by">Signed by</Label>
              <Input id="signed-by" value={signedBy} onChange={(e) => setSignedBy(e.target.value)} maxLength={300} className="mt-1" />
            </div>
            <div>
              <Label htmlFor="signed-on">Signed on</Label>
              <div className="mt-1">
                <DateInput id="signed-on" value={signedOn} onChange={setSignedOn} />
              </div>
            </div>
          </div>

          <p className="flex items-start gap-2 rounded-lg bg-emerald-50 p-3 text-xs text-emerald-900 dark:bg-emerald-500/10 dark:text-emerald-100">
            <CheckCircle2 className="mt-px h-3.5 w-3.5 shrink-0 text-emerald-600" aria-hidden />
            <span>
              This completes the contract step, as signing here does{dealId || contract?.dealId ? ': the deal is marked won' : ''}.
              The copy is kept with the {dealId || contract?.dealId ? 'deal' : 'lead'}, and nothing is emailed.
            </span>
          </p>

          {problem && <p role="alert" className="text-sm text-rose-700">{problem}</p>}
        </div>

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button type="button" onClick={() => void record()} disabled={!file || saving} className="gap-1.5 bg-emerald-600 hover:bg-emerald-500">
            {saving ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <FileCheck2 className="h-4 w-4" aria-hidden />}
            Record as signed
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
