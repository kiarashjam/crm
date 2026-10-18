import { useEffect, useRef, useState } from 'react';
import { CloudUpload, Loader2 } from 'lucide-react';
import { Dialog, DialogContent, DialogTitle, DialogDescription } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { servesDemoData } from '@/app/api/apiClient';
import { announceDataChanged } from '@/app/api/dataChanged';
import { isOrgAdmin } from '@/app/api/organizations';
import {
  findLocalUploads, hasLocalUploads, serverAcceptsUploads, uploadLocalData, uploadSteps,
  type LocalUploadPlan, type LocalUploadResult,
} from '@/app/api/localUpload';
import { useOrgOptional } from '@/app/contexts/OrgContext';

// One-time upload of custom fields and attachments saved in this browser before
// they moved to the server. Once the server answers, the app reads only the server,
// so without this anything entered earlier would simply stop showing. It asks first,
// says exactly what will move, and keeps whatever can't go yet for a later run.
const SNOOZE_KEY = 'crm.localUpload.snoozed.v1';

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

function snooze() {
  try { sessionStorage.setItem(SNOOZE_KEY, '1'); } catch { /* ignore */ }
}

export default function LocalDataUploadDialog() {
  const org = useOrgOptional();
  const currentOrg = org?.currentOrg ?? null;
  const [plan, setPlan] = useState<LocalUploadPlan | null>(null);
  const [stage, setStage] = useState<'ask' | 'uploading' | 'done'>('ask');
  const [done, setDone] = useState(0);
  const [result, setResult] = useState<LocalUploadResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const probed = useRef<string | null>(null);
  const canDefineFields = isOrgAdmin(currentOrg);

  useEffect(() => {
    // Wait for the workspace itself, not just its id: its role decides what can be uploaded.
    if (servesDemoData() || !currentOrg || org?.isReadOnly) return;
    if (probed.current === currentOrg.id) return;
    probed.current = currentOrg.id;
    try { if (sessionStorage.getItem(SNOOZE_KEY)) return; } catch { /* proceed */ }

    const found = findLocalUploads();
    if (!hasLocalUploads(found, isOrgAdmin(currentOrg))) return;
    let cancelled = false;
    // Only offer once the server takes both; before that, the browser is still where they live.
    serverAcceptsUploads().then((ready) => {
      if (cancelled || !ready) return;
      setPlan(found);
      setStage('ask');
    });
    return () => { cancelled = true; };
  }, [currentOrg, org?.isReadOnly]);

  if (!plan) return null;

  const close = () => {
    if (stage === 'uploading') return;
    if (stage === 'ask') snooze();
    if (stage === 'done') announceDataChanged();
    setPlan(null);
  };

  const upload = async () => {
    setStage('uploading');
    setDone(0);
    try {
      const outcome = await uploadLocalData(plan, { canDefineFields, onProgress: setDone });
      setResult(outcome);
      // Anything still waiting is offered again next session, not straight away.
      if (outcome.elsewhere > 0 || outcome.failed > 0 || outcome.needsManager.length > 0) snooze();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Couldn't upload. Nothing was changed.");
      snooze();
    }
    setStage('done');
  };

  const orgName = currentOrg?.name ?? 'your workspace';
  const fieldCount = canDefineFields ? plan.fields.length : 0;
  const parts = [
    fieldCount > 0 && plural(fieldCount, 'custom field'),
    plan.records.length > 0 && `values on ${plural(plan.records.length, 'record')}`,
    plan.files.length > 0 && plural(plan.files.length, 'file'),
  ].filter(Boolean) as string[];

  return (
    <Dialog open onOpenChange={(open) => { if (!open) close(); }}>
      <DialogContent className="max-w-md">
        <DialogTitle className="flex items-center gap-2">
          <CloudUpload className="h-5 w-5 text-indigo-600" /> Upload what&apos;s saved in this browser
        </DialogTitle>

        {stage === 'ask' && (
          <>
            <DialogDescription className="mt-1">
              Custom fields and attachments used to be saved only in this browser. They&apos;re shared with your team now.
              Upload yours to <strong>{orgName}</strong> so everyone sees them?
            </DialogDescription>
            <ul className="mt-1 space-y-1 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700">
              {parts.map((p) => <li key={p}>{p.charAt(0).toUpperCase() + p.slice(1)}</li>)}
            </ul>
            {!canDefineFields && plan.fields.length > 0 && (
              <p className="text-sm text-slate-500">
                Only an owner or manager can add fields. If {orgName} doesn&apos;t have one of these fields yet, its values
                stay in this browser until one of them adds it.
              </p>
            )}
            {plan.unrecoverable.length > 0 && (
              <p className="text-sm text-slate-500">
                {plural(plan.unrecoverable.length, 'file')} {plan.unrecoverable.length === 1 ? 'was' : 'were'} too large
                for the browser to keep, so only {plan.unrecoverable.length === 1 ? 'its name was' : 'their names were'} saved:{' '}
                {plan.unrecoverable.map((a) => a.fileName).join(', ')}. {plan.unrecoverable.length === 1 ? 'It' : 'They'} can&apos;t be uploaded.
              </p>
            )}
            <div className="mt-2 flex items-center justify-end gap-2">
              <Button variant="outline" onClick={close}>Not now</Button>
              <Button onClick={upload} className="gap-1.5"><CloudUpload className="h-4 w-4" /> Upload</Button>
            </div>
          </>
        )}

        {stage === 'uploading' && (
          <p className="flex items-center gap-2 py-4 text-sm text-slate-600" role="status">
            <Loader2 className="h-4 w-4 animate-spin" /> Uploading {done} of {uploadSteps(plan)}…
          </p>
        )}

        {stage === 'done' && (
          <>
            <div className="mt-1 space-y-2 text-sm text-slate-700" role="status">
              {error ? <p>{error}</p> : result && <Summary result={result} />}
            </div>
            <div className="mt-2 flex justify-end">
              <Button onClick={close}>Close</Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** "a", "a and b", "a, b and c". */
function joinWords(items: string[]): string {
  return items.length <= 1 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

function Summary({ result }: { result: LocalUploadResult }) {
  const uploaded = [
    result.recordsUploaded > 0 && `values on ${plural(result.recordsUploaded, 'record')}`,
    result.filesUploaded > 0 && plural(result.filesUploaded, 'file'),
  ].filter(Boolean) as string[];
  const sentences = [
    result.fieldsAdded > 0 && `Added ${plural(result.fieldsAdded, 'custom field')}.`,
    uploaded.length > 0 && `Uploaded ${joinWords(uploaded)}.`,
  ].filter(Boolean) as string[];
  return (
    <>
      <p>{sentences.length > 0 ? sentences.join(' ') : 'Nothing could be uploaded to this workspace.'}</p>
      {result.elsewhere > 0 && (
        <p>{plural(result.elsewhere, 'item')} {result.elsewhere === 1 ? 'is' : 'are'} on records in another workspace. Switch to it and you&apos;ll be offered {result.elsewhere === 1 ? 'it' : 'them'} again.</p>
      )}
      {result.needsManager.length > 0 && (
        <p>Waiting for an owner or manager to add: {result.needsManager.join(', ')}.</p>
      )}
      {result.failed > 0 && (
        <p>{plural(result.failed, 'item')} couldn&apos;t be uploaded and {result.failed === 1 ? 'is' : 'are'} still in this browser. You&apos;ll be asked again next time.</p>
      )}
      {result.unrecoverable.length > 0 && (
        <p className="text-slate-500">Not recoverable, too large for the browser to have kept: {result.unrecoverable.join(', ')}.</p>
      )}
    </>
  );
}
