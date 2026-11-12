import { useEffect, useMemo, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { toast } from 'sonner';
import { CheckCircle2, Copy, KeyRound, Loader2, Lock, MessageSquareHeart, PlugZap, Save, ShieldCheck, Wand2, XCircle } from 'lucide-react';
import { Link } from 'react-router-dom';
import { servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION, type Organization } from '@/app/api/organizations';
import {
  getOnboardingSettings, newOnboardingSecret, previewOnboarding, saveOnboardingSettings, testOnboardingPortal,
  type OnboardingLanguage, type OnboardingPreview, type OnboardingSettings,
} from '@/app/api/onboarding';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { Textarea } from '@/app/components/ui/textarea';
import { cn } from '@/app/components/ui/utils';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { useOrgMembers } from '@/app/hooks/queries/records';
import { EmailPreviewFrame, PlaceholderChips, insertAt } from '@/app/components/onboarding';

const STARTER = `<div style="font-family:Georgia,serif;max-width:560px;margin:0 auto;padding:32px;background:#ffffff;color:#0f172a">
  <h1 style="font-weight:normal;margin:0 0 16px">Bienvenue, {{firstName}}</h1>
  <p style="font:15px/1.6 system-ui,sans-serif;color:#334155">{{welcomeMessage}}</p>
  <p style="font:15px/1.6 system-ui,sans-serif;color:#334155">Votre adhésion commence le <strong>{{startDate}}</strong>.</p>
  <p style="font:15px/1.6 system-ui,sans-serif;color:#334155">Votre identifiant : <strong>{{loginEmail}}</strong></p>
  <p><a href="{{setPasswordUrl}}" style="display:inline-block;padding:12px 22px;border-radius:999px;background:#0f172a;color:#fff;text-decoration:none;font:600 14px system-ui,sans-serif">Choisir mon mot de passe</a></p>
  <p style="font:13px system-ui,sans-serif;color:#94a3b8">{{workspace}} · <a href="{{portalUrl}}" style="color:#94a3b8">Espace membre</a></p>
</div>`;

/** "Anna", "Anna and Ben", "Anna, Ben and Carla", "Anna, Ben and 3 others". */
function namesList(names: string[]): string {
  if (names.length <= 1) return names[0] ?? '';
  if (names.length <= 3) return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
  return `${names.slice(0, 2).join(', ')} and ${names.length - 2} others`;
}

const SAMPLE = { name: 'Camille Laurent', email: 'camille@example.com', company: 'Maison Laurent', dealName: 'Gold membership' };

/**
 * Welcome & portal: where new customers are handed over when their lead becomes a deal,
 * and the welcome email the portal sends them. Any portal that takes the signed handoff
 * works; Pavillon 46's member area is the first.
 */
export function WelcomePortalSection({ currentOrg }: { currentOrg: Organization | null }) {
  const [settings, setSettings] = useState<OnboardingSettings | null>(null);
  const [loading, setLoading] = useState(!servesDemoData());
  usePageLoading(loading);
  const canEdit = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ManageSettings);
  // Who can change it, for someone who cannot: the fields alone, greyed out, said nothing.
  const members = useOrgMembers().data;
  const managers = useMemo(
    () => (members ?? [])
      .filter((m) => orgCan({ role: m.role, permissions: m.permissions }, PERMISSION.ManageSettings))
      .map((m) => m.name || m.email)
      .filter(Boolean),
    [members],
  );

  const [url, setUrl] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [language, setLanguage] = useState<OnboardingLanguage>('fr');
  const [addToMembers, setAddToMembers] = useState(true);
  const [subject, setSubject] = useState('');
  const [html, setHtml] = useState('');
  const [saving, setSaving] = useState(false);
  const [secret, setSecret] = useState<string | null>(null);
  const [making, setMaking] = useState(false);
  const [testing, setTesting] = useState(false);
  const [test, setTest] = useState<{ ok: boolean; text: string } | null>(null);
  const [preview, setPreview] = useState<OnboardingPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const previewSeq = useRef(0);

  const take = (s: OnboardingSettings) => {
    setSettings(s);
    setUrl(s.webhookUrl ?? '');
    setEnabled(s.enabled);
    setLanguage(s.defaultLanguage);
    setAddToMembers(s.addToClubMembers);
    setSubject(s.emailSubject ?? '');
    setHtml(s.emailHtml ?? '');
  };

  useEffect(() => {
    if (servesDemoData()) return;
    getOnboardingSettings()
      .then(take)
      .catch((e: unknown) => toast.error(e instanceof Error ? e.message : 'Could not read the settings'))
      .finally(() => setLoading(false));
  }, [currentOrg?.id]);

  // The preview follows the typing, a moment after it stops.
  useEffect(() => {
    if (servesDemoData() || loading) return;
    const seq = ++previewSeq.current;
    const t = window.setTimeout(() => {
      previewOnboarding({
        ...SAMPLE,
        welcomeMessage: language === 'fr' ? 'Nous sommes ravis de vous accueillir au club.' : "We're delighted to welcome you to the club.",
        startsOnUtc: new Date(Date.now() + 14 * 86_400_000).toISOString().slice(0, 10),
        language,
        emailSubject: subject || null,
        emailHtml: html || null,
      })
        .then((p) => { if (seq === previewSeq.current) { setPreview(p); setPreviewError(null); } })
        .catch((e: unknown) => { if (seq === previewSeq.current) setPreviewError(e instanceof Error ? e.message : 'No preview'); });
    }, 450);
    return () => window.clearTimeout(t);
  }, [html, subject, language, loading]);

  const dirty = useMemo(() => Boolean(settings) && (
    url !== (settings?.webhookUrl ?? '') || enabled !== settings?.enabled || language !== settings?.defaultLanguage
    || addToMembers !== settings?.addToClubMembers || subject !== (settings?.emailSubject ?? '') || html !== (settings?.emailHtml ?? '')
  ), [settings, url, enabled, language, addToMembers, subject, html]);

  const save = async () => {
    setSaving(true);
    try {
      take(await saveOnboardingSettings({
        webhookUrl: url.trim() || null, enabled, defaultLanguage: language, addToClubMembers: addToMembers,
        emailSubject: subject.trim() || null, emailHtml: html.trim() || null,
      }));
      toast.success('Welcome & portal saved');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not save');
    } finally {
      setSaving(false);
    }
  };

  const makeSecret = async () => {
    if (settings?.hasSecret && !window.confirm('Make a new secret? The portal must be given the new one, and handoffs already made stop reporting back.')) return;
    setMaking(true);
    try {
      setSecret(await newOnboardingSecret());
      setSettings(await getOnboardingSettings());
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not make a secret');
    } finally {
      setMaking(false);
    }
  };

  const runTest = async () => {
    setTesting(true);
    setTest(null);
    try {
      const r = await testOnboardingPortal();
      setTest(r.ok
        ? { ok: true, text: `The portal answered ${r.statusCode}: it checked the signature and is ready.` }
        : { ok: false, text: r.error ?? `The portal answered ${r.statusCode}.` });
    } catch (e) {
      setTest({ ok: false, text: e instanceof Error ? e.message : 'The test could not be sent.' });
    } finally {
      setTesting(false);
    }
  };

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success('Copied');
    } catch {
      toast.error('Could not copy');
    }
  };

  if (servesDemoData()) {
    return (
      <div className="space-y-3 p-6">
        <Heading />
        <p className="text-sm text-slate-600">Connect the real API to hand new customers to your portal from here.</p>
      </div>
    );
  }
  if (loading) {
    return <div className="flex items-center justify-center gap-2 p-12 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /> Loading…</div>;
  }

  return (
    <div className="space-y-8 p-6">
      <div className="flex flex-wrap items-start gap-3">
        <Heading />
        <span className={cn(
          'ml-auto inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold',
          settings?.configured ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-500',
        )}>
          {settings?.configured ? <CheckCircle2 className="h-3.5 w-3.5" /> : <PlugZap className="h-3.5 w-3.5" />}
          {settings?.configured ? 'Connected' : 'Not connected'}
        </span>
      </div>

      {!canEdit && (
        <div role="status" className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
          <Lock className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
          <div>
            <p className="font-medium">You can see these settings, but not change them.</p>
            <p className="mt-0.5 text-amber-800">
              Changing the portal, its secret or the welcome email takes the <strong>Manage settings</strong> permission
              {managers.length > 0 ? `, which ${namesList(managers)} ${managers.length === 1 ? 'has' : 'have'}` : ''}.
              They can change it here, or give you that permission in{' '}
              <Link to="/settings?tab=organization" className="font-medium underline underline-offset-2">Settings › Team</Link>.
            </p>
          </div>
        </div>
      )}

      <ol className="grid gap-3 sm:grid-cols-3">
        {[
          { n: 1, title: 'Convert', text: 'A lead becomes a deal. You write a welcome and say when they start, or that it is still to be decided.' },
          { n: 2, title: 'Hand over', text: 'CadenceE posts a signed handoff to your portal, which opens their account.' },
          { n: 3, title: 'Welcome', text: 'The portal sends your email with a link to set their password, and reports each step back.' },
        ].map((s, i) => (
          <motion.li
            key={s.n}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.08 }}
            className="rounded-xl border border-slate-200 bg-gradient-to-br from-white to-orange-50/40 p-3"
          >
            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-gradient-to-br from-orange-500 to-pink-500 text-xs font-bold text-white">{s.n}</span>
            <p className="mt-2 text-sm font-semibold text-slate-800">{s.title}</p>
            <p className="mt-0.5 text-xs text-slate-500">{s.text}</p>
          </motion.li>
        ))}
      </ol>

      <section className="space-y-4">
        <h3 className="font-semibold text-slate-900">Your portal</h3>
        <div>
          <Label htmlFor="portal-url">Handoff address</Label>
          <Input
            id="portal-url"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://api.your-portal.com/api/integrations/cadence/onboarding"
            className="mt-1.5 font-mono text-sm"
            disabled={!canEdit}
          />
          <p className="mt-1 text-xs text-slate-500">An https address on the internet. For Pavillon 46, its API's <code className="rounded bg-slate-100 px-1">/api/integrations/cadence/onboarding</code>.</p>
        </div>

        <div className="rounded-xl border border-slate-200 p-4">
          <div className="flex flex-wrap items-center gap-3">
            <KeyRound className="h-4 w-4 text-violet-500" />
            <p className="flex-1 text-sm text-slate-700">
              Signing secret {settings?.hasSecret ? <span className="font-mono text-slate-500">••••{settings.secretHint}</span> : <span className="text-slate-400">not made yet</span>}
            </p>
            {canEdit && (
              <Button type="button" variant="outline" size="sm" onClick={() => void makeSecret()} disabled={making}>
                {making ? <Loader2 className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
                {settings?.hasSecret ? 'Make a new one' : 'Make the secret'}
              </Button>
            )}
          </div>
          {secret && (
            <motion.div initial={{ opacity: 0, scale: 0.98 }} animate={{ opacity: 1, scale: 1 }} className="mt-3 rounded-lg border border-amber-300 bg-amber-50 p-3">
              <p className="text-xs font-medium text-amber-800">Shown this once. Give it to the portal (in Pavillon 46, its CadenceE onboarding secret setting).</p>
              <div className="mt-2 flex gap-2">
                <Input readOnly value={secret} className="font-mono text-xs" aria-label="New signing secret" />
                <Button type="button" variant="outline" onClick={() => void copy(secret)}><Copy className="h-4 w-4" /> Copy</Button>
              </div>
            </motion.div>
          )}
          <p className="mt-2 flex items-start gap-1.5 text-xs text-slate-500">
            <ShieldCheck className="mt-px h-3.5 w-3.5 shrink-0 text-emerald-500" />
            Every handoff is signed with it (X-Cadence-Signature-256, with the time), so the portal knows it came from you and is not a replay.
          </p>
        </div>

        {canEdit && settings?.configured && (
          <div className="flex flex-wrap items-center gap-3">
            <Button type="button" variant="outline" size="sm" onClick={() => void runTest()} disabled={testing || dirty}>
              {testing ? <Loader2 className="h-4 w-4 animate-spin" /> : <PlugZap className="h-4 w-4" />} Send a test
            </Button>
            {dirty && <span className="text-xs text-slate-500">Save first, then test.</span>}
            {test && (
              <span className={cn('inline-flex items-center gap-1.5 text-sm', test.ok ? 'text-emerald-700' : 'text-rose-700')}>
                {test.ok ? <CheckCircle2 className="h-4 w-4" /> : <XCircle className="h-4 w-4" />} {test.text}
              </span>
            )}
          </div>
        )}
      </section>

      <section className="space-y-3">
        <h3 className="font-semibold text-slate-900">When a lead is converted</h3>
        <label className="flex items-center gap-3 text-sm text-slate-700">
          <Switch checked={enabled} onCheckedChange={setEnabled} disabled={!canEdit} aria-label="Offer the welcome by default" />
          Offer the welcome on every conversion into a deal
        </label>
        <label className="flex items-center gap-3 text-sm text-slate-700">
          <Switch checked={addToMembers} onCheckedChange={setAddToMembers} disabled={!canEdit} aria-label="Add to club members" />
          Also add them to the club's members here, with their start date
        </label>
        <div className="flex items-center gap-3 text-sm text-slate-700">
          Language
          <div className="inline-flex rounded-lg border border-slate-200 p-0.5">
            {(['fr', 'en'] as const).map((l) => (
              <button
                key={l}
                type="button"
                disabled={!canEdit}
                onClick={() => setLanguage(l)}
                className={cn('rounded-md px-3 py-1 text-sm font-medium transition', language === l ? 'bg-slate-900 text-white' : 'text-slate-500 hover:text-slate-800')}
              >
                {l === 'fr' ? 'Français' : 'English'}
              </button>
            ))}
          </div>
        </div>
      </section>

      <section className="space-y-3">
        <div className="flex flex-wrap items-end gap-2">
          <div>
            <h3 className="font-semibold text-slate-900">Welcome email</h3>
            <p className="text-sm text-slate-500">Paste your own HTML, or leave it empty for the portal's design with your message in it.</p>
          </div>
          {canEdit && !html && (
            <Button type="button" variant="outline" size="sm" className="ml-auto" onClick={() => setHtml(STARTER)}>
              <Wand2 className="h-4 w-4" /> Start from an example
            </Button>
          )}
        </div>
        <div className="grid gap-4 xl:grid-cols-2">
          <div className="space-y-2">
            <Input
              id="welcome-subject"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="Subject, e.g. Bienvenue au club, {{firstName}}"
              disabled={!canEdit}
              maxLength={300}
              aria-label="Welcome email subject"
            />
            <Textarea
              id="welcome-html"
              value={html}
              onChange={(e) => setHtml(e.target.value)}
              placeholder="<div>…your email's HTML…</div>"
              rows={16}
              spellCheck={false}
              disabled={!canEdit}
              className="bg-slate-950 font-mono text-xs text-emerald-200 placeholder:text-slate-500"
              aria-label="Welcome email HTML"
            />
            {canEdit && settings && (
              <>
                <PlaceholderChips fields={settings.cadenceFields} tone="orange" onPick={(t) => setHtml(insertAt('welcome-html', html, t))} />
                <PlaceholderChips fields={settings.receiverFields} tone="violet" onPick={(t) => setHtml(insertAt('welcome-html', html, t))} />
                <p className="text-[11px] text-slate-500">
                  <span className="text-orange-600">Orange</span>: filled by CadenceE, as text. <span className="text-violet-600">Violet</span>: filled by the portal when it sends. Give any a fallback: {'{{firstName|there}}'}.
                  The portal removes scripts and anything else unsafe before sending.
                </p>
              </>
            )}
          </div>
          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-400">Preview, for a sample member</p>
            {previewError ? (
              <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">{previewError}</p>
            ) : preview ? (
              <>
                {preview.unknown.length > 0 && (
                  <p className="rounded-lg border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">Nothing fills {preview.unknown.join(', ')}.</p>
                )}
                <EmailPreviewFrame subject={preview.subject} html={preview.html} to={SAMPLE.email} />
              </>
            ) : (
              <div className="flex h-60 items-center justify-center rounded-xl border border-dashed border-slate-200 text-slate-400"><Loader2 className="h-5 w-5 animate-spin" /></div>
            )}
          </div>
        </div>
      </section>

      {canEdit && (
        <div className="sticky bottom-0 -mx-6 flex justify-end border-t border-slate-100 bg-white/90 px-6 py-3 backdrop-blur">
          <Button type="button" onClick={() => void save()} disabled={saving || !dirty} className="bg-gradient-to-r from-orange-500 to-pink-500 text-white">
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} Save
          </Button>
        </div>
      )}
    </div>
  );
}

function Heading() {
  return (
    <div>
      <h2 className="flex items-center gap-2 text-xl font-semibold text-slate-900">
        <MessageSquareHeart className="h-5 w-5 text-orange-600" />
        Welcome &amp; portal
      </h2>
      <p className="mt-1 text-sm text-slate-600">When a lead becomes a deal, hand them to your member portal with a welcome email.</p>
    </div>
  );
}
