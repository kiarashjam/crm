// A member's codes at the POS: what names them on a ticket. Their member number or the code on
// their card (typed in as the card shows it), or a new one Cadence makes with a check digit, shown
// with its QR code for a card or a phone to carry; and the POS's own customer, remembered when
// staff matched one of their tickets. A ticket that carries one is posted to the member.
//
// Giving and taking away codes takes club.members; everyone else reads them.

import { useState } from 'react';
import { Loader2, Plus, QrCode, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { addMemberCode, removeMemberCode, type MemberCode } from '@/app/api/pos';
import { useMemberCodes } from '@/app/hooks/queries';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

/** A code's QR code, drawn from the rows of modules the server worked out (dark is 1), with the quiet zone around it. */
function QrImage({ code }: { code: MemberCode }) {
  const rows = code.qrRows ?? [];
  if (rows.length === 0) return null;
  const size = rows.length + 8;
  return (
    <svg viewBox={`0 0 ${size} ${size}`} width={96} height={96} role="img" aria-label={`QR code for ${code.code}`} className="rounded bg-white">
      <rect width={size} height={size} fill="#fff" />
      {rows.flatMap((row, y) =>
        [...row].map((bit, x) => (bit === '1' ? <rect key={`${x}-${y}`} x={x + 4} y={y + 4} width={1} height={1} fill="#0f172a" /> : null)))}
    </svg>
  );
}

export function MemberCodesCard({ memberId }: { memberId: string }) {
  const query = useMemberCodes(memberId);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState<null | 'add' | 'make' | string>(null);
  const [shown, setShown] = useState<string | null>(null);
  const codes = query.data ?? [];

  const add = async (typed?: string) => {
    setBusy(typed ? 'add' : 'make');
    try {
      const saved = await addMemberCode(memberId, typed);
      toast.success(`Code ${saved.code} names this member at the POS`);
      setCode('');
      setShown(saved.id);
    } catch (err) {
      toast.error(message(err, 'Could not give the code'));
    } finally {
      setBusy(null);
    }
  };

  const remove = async (c: MemberCode) => {
    if (!window.confirm(`Take away the code ${c.code}? A ticket that carries it no longer finds this member.`)) return;
    setBusy(c.id);
    try {
      await removeMemberCode(memberId, c.id);
      toast.success(`Code ${c.code} taken away`);
    } catch (err) {
      toast.error(message(err, 'Could not take the code away'));
    } finally {
      setBusy(null);
    }
  };

  return (
    <section aria-labelledby={`member-codes-${memberId}`} className="overflow-hidden rounded-2xl border border-cyan-200/70 bg-white shadow-sm">
      <div className="flex items-center border-b border-cyan-100 px-4 py-2.5">
        <h3 id={`member-codes-${memberId}`} className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-cyan-700">
          <QrCode className="h-3.5 w-3.5" aria-hidden="true" /> Codes at the POS
        </h3>
      </div>
      <div className="space-y-3 p-4 text-sm">
        {query.isPending ? (
          <p className="flex items-center gap-2 text-slate-500" role="status"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
        ) : query.isError ? (
          <p className="text-slate-600">
            Their codes could not be loaded.{' '}
            <button type="button" onClick={() => void query.refetch()} className="font-medium text-cyan-700 underline">Try again</button>
          </p>
        ) : codes.length === 0 ? (
          <p className="text-slate-500">No code yet: a ticket from the POS can&apos;t name this member until they have one.</p>
        ) : (
          <ul className="space-y-2">
            {codes.map((c) => (
              <li key={c.id} className="rounded-lg border border-slate-100 p-2">
                <div className="flex items-center justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-mono font-medium text-slate-900">{c.code}</p>
                    <p className="text-xs text-slate-500">
                      {c.kind === 'pos-customer' ? (c.label ?? 'POS customer') : (c.label ?? 'Member code')}
                      {c.createdByName ? ` · by ${c.createdByName}` : ''}
                    </p>
                  </div>
                  <div className="flex items-center gap-1">
                    {c.qrRows && (
                      <Button type="button" size="sm" variant="ghost" aria-expanded={shown === c.id} onClick={() => setShown(shown === c.id ? null : c.id)}>
                        {shown === c.id ? 'Hide QR' : 'QR'}
                      </Button>
                    )}
                    <WriteOnly permission={PERMISSION.ClubMembers}>
                      <Button type="button" size="icon" variant="ghost" aria-label={`Take away the code ${c.code}`} disabled={busy === c.id}
                        onClick={() => void remove(c)}>
                        {busy === c.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4 text-slate-500" />}
                      </Button>
                    </WriteOnly>
                  </div>
                </div>
                {shown === c.id && <div className="mt-2"><QrImage code={c} /></div>}
              </li>
            ))}
          </ul>
        )}
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <div className="flex flex-wrap items-center gap-2">
            <Input aria-label="Member code" className="h-8 w-40" value={code} maxLength={40} placeholder="Their member number"
              onChange={(e) => setCode(e.target.value)} />
            <Button type="button" size="sm" variant="outline" disabled={!code.trim() || busy !== null} onClick={() => void add(code.trim())}>
              {busy === 'add' ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Plus className="mr-1 h-3.5 w-3.5" />} Add
            </Button>
            <Button type="button" size="sm" variant="ghost" disabled={busy !== null} onClick={() => void add()}>
              {busy === 'make' ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : null} Make a new code
            </Button>
          </div>
        </WriteOnly>
      </div>
    </section>
  );
}
