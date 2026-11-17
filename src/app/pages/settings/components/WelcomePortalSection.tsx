import { useEffect, useMemo, useRef, useState, type ComponentType, type ReactNode } from 'react';
import { motion } from 'motion/react';
import { toast } from 'sonner';
import {
  AlertTriangle, ArrowRight, AtSign, CalendarClock, CheckCircle2, Code2, Copy, KeyRound, LayoutTemplate, Link2, Loader2, Lock, Mail, Monitor, PenLine,
  PlugZap, RotateCcw, Save, Send, Smartphone, Sparkles, Wand2, XCircle, Zap,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION, type Organization } from '@/app/api/organizations';
import {
  getOnboardingSettings, newOnboardingSecret, previewOnboarding, saveOnboardingSettings, sendOnboardingTestEmail, senderLabel, testOnboardingPortal,
  type OnboardingLanguage, type OnboardingPreview, type OnboardingPreviewRequest, type OnboardingSettings,
} from '@/app/api/onboarding';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { Textarea } from '@/app/components/ui/textarea';
import { cn } from '@/app/components/ui/utils';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { useOrgMembers } from '@/app/hooks/queries/records';
import { useModules } from '@/app/hooks/useModules';
import {
  EmailPreviewFrame, PlaceholderChips, SIMPLE_COLORS, buildSimpleHtml, designOf, insertAt, readSimple, simpleDefaults, withoutSimpleMark,
  type EmailDesign, type SimpleEmail,
} from '@/app/components/onboarding';
import { getCurrentUser } from '@/app/lib/auth';
import { EmailImageTools } from '@/app/components/onboarding/EmailImageTools';
import { HelpTip, IconTile, SettingsCard, StatusPill, type PillTone } from './SettingsCard';

/** "Anna", "Anna and Ben", "Anna, Ben and Carla", "Anna, Ben and 3 others". */
function namesList(names: string[]): string {
  if (names.length <= 1) return names[0] ?? '';
  if (names.length <= 3) return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
  return `${names.slice(0, 2).join(', ')} and ${names.length - 2} others`;
}

const SAMPLE = { name: 'Camille Laurent', email: 'camille@example.com', company: 'Maison Laurent', dealName: 'Gold membership' };

/** The email for the sample member, as typed so far: what the preview shows and a test sends. */
function sampleRequest(language: OnboardingLanguage, subject: string, html: string): OnboardingPreviewRequest {
  return {
    ...SAMPLE,
    welcomeMessage: language === 'fr' ? 'Nous sommes ravis de vous accueillir au club.' : "We're delighted to welcome you to the club.",
    startsOnUtc: new Date(Date.now() + 14 * 86_400_000).toISOString().slice(0, 10),
    language,
    emailSubject: subject || null,
    emailHtml: html || null,
  };
}

const looksLikeEmail = (text: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(text.trim());

const DESIGNS: { id: EmailDesign; label: string; icon: ComponentType<{ className?: string }>; hint: string }[] = [
  { id: 'portal', label: 'Portal design', icon: LayoutTemplate, hint: 'The portal’s own email, with your welcome message in it.' },
  { id: 'simple', label: 'Simple', icon: PenLine, hint: 'Write the words; the email is designed for you.' },
  { id: 'html', label: 'HTML', icon: Code2, hint: 'Your own HTML, for a design of your own.' },
];

const SECTION = { conversion: 'welcome-conversion', connection: 'welcome-connection', email: 'welcome-email' } as const;

const LANGUAGES: { id: OnboardingLanguage; label: string }[] = [
  { id: 'fr', label: 'Français' },
  { id: 'en', label: 'English' },
];

/** What a test of the portal found: a headline, the detail, and what to know about the sender. */
type TestResult = { ok: boolean; title: string; detail: string; note?: string; warning?: string | null };

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
  const clubOn = useModules().club;
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
  const [bookingPage, setBookingPage] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [language, setLanguage] = useState<OnboardingLanguage>('fr');
  const [addToMembers, setAddToMembers] = useState(true);
  const [senderName, setSenderName] = useState('');
  const [senderEmail, setSenderEmail] = useState('');
  const [replyTo, setReplyTo] = useState('');
  const [subject, setSubject] = useState('');
  const [html, setHtml] = useState('');
  const [saving, setSaving] = useState(false);
  const [secret, setSecret] = useState<string | null>(null);
  const [making, setMaking] = useState(false);
  const [testing, setTesting] = useState(false);
  const [test, setTest] = useState<TestResult | null>(null);
  const [device, setDevice] = useState<'desktop' | 'phone'>('desktop');
  const [design, setDesign] = useState<EmailDesign>('portal');
  // The simple email last left for the HTML editor, to come back to while that HTML is unchanged.
  const leftSimple = useRef<{ email: SimpleEmail; html: string } | null>(null);
  // The box a field button adds to: the one last typed in.
  const lastField = useRef<string | null>(null);
  const [testTo, setTestTo] = useState(() => getCurrentUser()?.email ?? '');
  const [sendingTest, setSendingTest] = useState(false);
  const [testEmail, setTestEmail] = useState<{ ok: boolean; text: string } | null>(null);
  const [preview, setPreview] = useState<OnboardingPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const previewSeq = useRef(0);

  const take = (s: OnboardingSettings) => {
    setSettings(s);
    setUrl(s.webhookUrl ?? '');
    setBookingPage(s.bookingPageUrl ?? '');
    setEnabled(s.enabled);
    setLanguage(s.defaultLanguage);
    setAddToMembers(s.addToClubMembers);
    setSenderName(s.senderName ?? '');
    setSenderEmail(s.senderEmail ?? '');
    setReplyTo(s.replyTo ?? '');
    setSubject(s.emailSubject ?? '');
    setHtml(s.emailHtml ?? '');
    setDesign(designOf(s.emailHtml));
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
      previewOnboarding(sampleRequest(language, subject, html))
        .then((p) => { if (seq === previewSeq.current) { setPreview(p); setPreviewError(null); } })
        .catch((e: unknown) => { if (seq === previewSeq.current) setPreviewError(e instanceof Error ? e.message : 'No preview'); });
    }, 450);
    return () => window.clearTimeout(t);
  }, [html, subject, language, loading]);

  const dirty = useMemo(() => Boolean(settings) && (
    url !== (settings?.webhookUrl ?? '') || enabled !== settings?.enabled || language !== settings?.defaultLanguage
    || addToMembers !== settings?.addToClubMembers || subject !== (settings?.emailSubject ?? '') || html !== (settings?.emailHtml ?? '')
    || senderName !== (settings?.senderName ?? '') || senderEmail !== (settings?.senderEmail ?? '') || replyTo !== (settings?.replyTo ?? '')
    || bookingPage !== (settings?.bookingPageUrl ?? '')
  ), [settings, url, enabled, language, addToMembers, subject, html, senderName, senderEmail, replyTo, bookingPage]);

  // Leaving with edits not saved: the browser asks first, rather than dropping them.
  useEffect(() => {
    if (!dirty) return;
    const stay = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = ''; };
    window.addEventListener('beforeunload', stay);
    return () => window.removeEventListener('beforeunload', stay);
  }, [dirty]);

  // Who it is from, as typed: what the preview shows before anything is saved.
  const fromShown = senderLabel(senderName.trim() || settings?.defaultSenderName, senderEmail.trim() || null);
  const replyShown = replyTo.trim() || settings?.defaultReplyTo || '';

  const save = async () => {
    setSaving(true);
    try {
      take(await saveOnboardingSettings({
        webhookUrl: url.trim() || null, enabled, defaultLanguage: language, addToClubMembers: addToMembers,
        emailSubject: subject.trim() || null, emailHtml: html.trim() || null,
        senderName: senderName.trim() || null, senderEmail: senderEmail.trim() || null, replyTo: replyTo.trim() || null,
        // Sent only when changed: left out, the server keeps it as it is (and empty takes it away).
        ...(bookingPage !== (settings?.bookingPageUrl ?? '') ? { bookingPageUrl: bookingPage.trim() } : {}),
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
      if (!r.ok) {
        setTest({ ok: false, title: 'The portal did not accept the test', detail: r.error ?? `It answered ${r.statusCode}.` });
        return;
      }
      // A from address the portal's mail service may not use: it sends from its own, and replies come to you.
      const domain = (e?: string | null) => e?.split('@')[1]?.toLowerCase();
      const own = senderEmail.trim() || settings?.senderEmail;
      const note = own && r.fromEmail && domain(own) !== domain(r.fromEmail)
        ? `${own} is not on its domain (${domain(r.fromEmail)}), so it sends from ${r.fromEmail} under your name, with replies to ${replyShown || own}.`
        : undefined;
      setTest({
        ok: true,
        title: 'Connected: the portal is ready',
        detail: [`Answered ${r.statusCode}`, 'signature checked', r.fromEmail ? `sends from ${senderLabel(r.fromName, r.fromEmail)}` : null].filter(Boolean).join(' · '),
        note,
        warning: r.callbackProblem,
      });
    } catch (e) {
      setTest({ ok: false, title: 'The test could not be sent', detail: e instanceof Error ? e.message : 'Try again in a moment.' });
    } finally {
      setTesting(false);
    }
  };

  const simple = design === 'simple' ? readSimple(html) ?? simpleDefaults(language) : null;
  const editSimple = (change: Partial<SimpleEmail>) => setHtml(buildSimpleHtml({ ...(readSimple(html) ?? simpleDefaults(language)), ...change }));

  const chooseDesign = (next: EmailDesign) => {
    if (next === design) return;
    if (next === 'portal') {
      if (html.trim() && !window.confirm('Use the portal’s design? The email written here is cleared.')) return;
      setHtml('');
    } else if (next === 'simple') {
      const back = leftSimple.current && leftSimple.current.html === html ? leftSimple.current.email : null;
      if (!back && design === 'html' && html.trim() && !window.confirm('Write it in the simple editor? Your HTML is replaced by a simple email.')) return;
      setHtml(buildSimpleHtml(back ?? readSimple(html) ?? simpleDefaults(language)));
    } else {
      const from = design === 'simple' ? html : buildSimpleHtml(simpleDefaults(language));
      const own = withoutSimpleMark(from);
      leftSimple.current = design === 'simple' && simple ? { email: simple, html: own } : null;
      setHtml(own);
    }
    setDesign(next);
  };

  // Where each box's words are, so a field button can add to the one last typed in.
  const boxes: Record<string, { value: string; set: (value: string) => void }> = {
    'welcome-subject': { value: subject, set: setSubject },
    'welcome-html': { value: html, set: setHtml },
    'simple-heading': { value: simple?.heading ?? '', set: (v) => editSimple({ heading: v }) },
    'simple-message': { value: simple?.message ?? '', set: (v) => editSimple({ message: v }) },
    'simple-button': { value: simple?.button ?? '', set: (v) => editSimple({ button: v }) },
    'simple-signoff': { value: simple?.signoff ?? '', set: (v) => editSimple({ signoff: v }) },
  };
  const addField = (token: string) => {
    const shown = (id: string | null): id is string => Boolean(id && boxes[id] && document.getElementById(id));
    const id = shown(lastField.current) ? lastField.current : design === 'html' ? 'welcome-html' : design === 'simple' ? 'simple-message' : 'welcome-subject';
    const box = boxes[id];
    if (box) box.set(insertAt(id, box.value, token));
  };
  const typingIn = (id: string) => () => { lastField.current = id; };

  const sendTestEmail = async () => {
    setSendingTest(true);
    setTestEmail(null);
    try {
      const r = await sendOnboardingTestEmail({ ...sampleRequest(language, subject, html), to: testTo.trim() });
      setTestEmail({ ok: true, text: r.status === 'sent' ? `Sent to ${r.to}. Have a look in that inbox.` : `Queued for ${r.to}: the mail server was busy, it goes in a minute.` });
    } catch (e) {
      setTestEmail({ ok: false, text: e instanceof Error ? e.message : 'The test could not be sent.' });
    } finally {
      setSendingTest(false);
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
      <SettingsCard icon={PlugZap} tone="violet" title="Welcome handoff" description="Connect the real API to hand new customers to your portal from here." />
    );
  }
  if (loading) {
    return <div className="flex items-center justify-center gap-2 p-12 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /> Loading…</div>;
  }

  const connection: { tone: PillTone; text: string } = settings?.configured
    ? { tone: 'good', text: 'Connected' }
    : !settings?.webhookUrl
      ? { tone: 'warn', text: 'Address needed' }
      : { tone: 'warn', text: 'Secret needed' };
  const languageName = LANGUAGES.find((l) => l.id === language)?.label ?? language;

  return (
    <div className="space-y-5">
      {!canEdit && (
        <div role="status" className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
          <Lock className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
          <div>
            <p className="font-medium">You can see these settings, but not change them.</p>
            <p className="mt-0.5 text-amber-800 dark:text-amber-200/80">
              It takes the <strong>Manage settings</strong> permission
              {managers.length > 0 ? `, which ${namesList(managers)} ${managers.length === 1 ? 'has' : 'have'}` : ''}.
              Ask them, or for that permission in{' '}
              <Link to="/settings?tab=organization" className="font-medium underline underline-offset-2">Settings › Team</Link>.
            </p>
          </div>
        </div>
      )}

      <FlowOverview
        steps={[
          {
            icon: Sparkles, title: 'Convert', text: 'Lead becomes a deal', target: SECTION.conversion,
            status: enabled ? { tone: 'good', text: 'Welcome on' } : { tone: 'idle', text: 'Welcome off' },
          },
          { icon: Send, title: 'Hand over', text: 'Signed to your portal', target: SECTION.connection, status: connection },
          {
            icon: Mail, title: 'Welcome', text: 'Email to set a password', target: SECTION.email,
            status: { tone: 'info', text: `${design === 'portal' ? 'Portal design' : design === 'simple' ? 'Simple email' : 'Your HTML'} · ${language.toUpperCase()}` },
          },
        ]}
      />

      <SettingsCard
        id={SECTION.connection}
        icon={PlugZap}
        tone="violet"
        title="Connection"
        description="Where new members are handed over."
        aside={<StatusPill tone={connection.tone}>{connection.text}</StatusPill>}
      >
        <div className="space-y-4">
          <div>
            <div className="flex items-center gap-0.5">
              <Label htmlFor="portal-url">Handoff address</Label>
              <HelpTip label="About the handoff address">
                An https address on the internet. For Pavillon 46, its API&apos;s{' '}
                <code className="rounded bg-slate-100 px-1 text-xs">/api/integrations/cadence/onboarding</code>.
              </HelpTip>
            </div>
            <div className="relative mt-1">
              <Link2 className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
              <Input
                id="portal-url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://api.your-portal.com/api/integrations/cadence/onboarding"
                className="pl-9 font-mono text-sm"
                disabled={!canEdit}
              />
            </div>
          </div>

          <div id="welcome-booking">
            <div className="flex items-center gap-0.5">
              <Label htmlFor="booking-page">Meeting booking page</Label>
              <HelpTip label="About the meeting booking page">
                Where a lead books a meeting from Leads › Invite to a meeting: each lead&apos;s link opens it with their own invitation,
                signed with the secret below. For Pavillon 46, its{' '}
                <code className="rounded bg-slate-100 px-1 text-xs">/rendez-vous</code> page.
              </HelpTip>
            </div>
            <div className="relative mt-1">
              <CalendarClock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
              <Input
                id="booking-page"
                type="url"
                value={bookingPage}
                onChange={(e) => setBookingPage(e.target.value)}
                placeholder="https://your-club.ch/rendez-vous"
                maxLength={500}
                className="pl-9 font-mono text-sm"
                disabled={!canEdit}
              />
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-slate-50/70 p-3">
            <IconTile icon={KeyRound} tone="violet" size="sm" />
            <div className="min-w-0 flex-1">
              <p className="flex items-center gap-0.5 text-sm font-medium text-slate-800">
                Signing secret
                <HelpTip label="About the signing secret">
                  Every handoff is signed with it (X-Cadence-Signature-256, with the time), so the portal knows it came from you
                  and is not a replay. The portal needs the same secret: in Pavillon 46, its CadenceE onboarding secret setting.
                </HelpTip>
              </p>
              <p className="font-mono text-xs text-slate-500">
                {settings?.hasSecret ? <>••••••••{settings.secretHint}</> : <span className="font-sans">Not made yet</span>}
              </p>
            </div>
            {canEdit && (
              <Button type="button" variant="outline" size="sm" onClick={() => void makeSecret()} disabled={making}>
                {making ? <Loader2 className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
                {settings?.hasSecret ? 'Make a new one' : 'Make the secret'}
              </Button>
            )}
          </div>

          {secret && (
            <motion.div initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }} className="rounded-xl border border-amber-300 bg-amber-50 p-3 dark:border-amber-500/30 dark:bg-amber-500/10">
              <p className="text-xs font-semibold text-amber-800 dark:text-amber-200">Shown only once: copy it into the portal now.</p>
              <div className="mt-2 flex gap-2">
                <Input readOnly value={secret} className="font-mono text-xs" aria-label="New signing secret" />
                <Button type="button" variant="outline" onClick={() => void copy(secret)}><Copy className="h-4 w-4" /> Copy</Button>
              </div>
            </motion.div>
          )}
        </div>

        {canEdit && (
          <div className="mt-4 space-y-3 border-t border-slate-200 pt-4 dark:border-white/10">
            <div className="flex flex-wrap items-center gap-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void runTest()} disabled={testing || dirty || !settings?.configured}>
                {testing ? <Loader2 className="h-4 w-4 animate-spin" /> : <PlugZap className="h-4 w-4" />} Test the connection
              </Button>
              <span className="text-xs text-slate-500">
                {!settings?.configured ? 'Add the address and the secret, then save, to test it.' : dirty ? 'Save first, then test.' : 'Checks the portal and the sender it uses.'}
              </span>
            </div>
            {test && (
              <motion.div
                role="status"
                initial={{ opacity: 0, y: -4 }}
                animate={{ opacity: 1, y: 0 }}
                className={cn(
                  'flex items-start gap-3 rounded-xl border p-3 text-sm',
                  test.ok
                    ? 'border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-100'
                    : 'border-rose-200 bg-rose-50 text-rose-900 dark:border-rose-500/30 dark:bg-rose-500/10 dark:text-rose-100',
                )}
              >
                {test.ok
                  ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-hidden />
                  : <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-rose-600" aria-hidden />}
                <div className="min-w-0">
                  <p className="font-medium">{test.title}</p>
                  <p className="break-words text-xs opacity-80">{test.detail}</p>
                  {test.note && <p className="mt-1 break-words text-xs text-amber-800 dark:text-amber-200">{test.note}</p>}
                </div>
              </motion.div>
            )}
            {test?.warning && (
              <p role="alert" className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
                <span><strong>The portal could not report back.</strong> {test.warning}</span>
              </p>
            )}
          </div>
        )}
      </SettingsCard>

      <div className="grid gap-5 lg:grid-cols-2">
        <SettingsCard icon={AtSign} tone="sky" title="Sent from" description="How the welcome shows in their inbox.">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <Label htmlFor="sender-name">Name</Label>
              <Input
                id="sender-name"
                value={senderName}
                onChange={(e) => setSenderName(e.target.value)}
                placeholder={settings?.defaultSenderName ?? 'Your organization'}
                maxLength={100}
                disabled={!canEdit}
                className="mt-1"
              />
            </div>
            <div>
              <div className="flex items-center gap-0.5">
                <Label htmlFor="sender-email">From address</Label>
                <HelpTip label="About the from address">
                  The portal sends through its own mail service, which can send only for its own domain (for Pavillon 46, its own).
                  On any other, it sends from its own address under your name, and replies still come to you. Send a test to see which it uses.
                </HelpTip>
              </div>
              <Input
                id="sender-email"
                type="email"
                value={senderEmail}
                onChange={(e) => setSenderEmail(e.target.value)}
                placeholder="The portal's own"
                maxLength={256}
                disabled={!canEdit}
                className="mt-1"
              />
            </div>
            <div>
              <Label htmlFor="reply-to" className="flex h-7 items-center">Replies go to</Label>
              <Input
                id="reply-to"
                type="email"
                value={replyTo}
                onChange={(e) => setReplyTo(e.target.value)}
                placeholder={settings?.defaultReplyTo ?? 'members@your-club.ch'}
                maxLength={256}
                disabled={!canEdit}
                className="mt-1"
              />
            </div>
          </div>
        </SettingsCard>

        <SettingsCard id={SECTION.conversion} icon={Zap} tone="emerald" title="On conversion" description="When a lead becomes a deal.">
          <div className="divide-y divide-slate-200 dark:divide-white/10">
            <SettingRow title="Offer the welcome" hint="On by default for every new deal.">
              <Switch checked={enabled} onCheckedChange={setEnabled} disabled={!canEdit} aria-label="Offer the welcome by default" />
            </SettingRow>
            {/* With the club turned off there are no members to add them to (the server adds nobody). */}
            {clubOn && (
              <SettingRow title="Add to club members" hint="Here too, with their start date.">
                <Switch checked={addToMembers} onCheckedChange={setAddToMembers} disabled={!canEdit} aria-label="Add to club members" />
              </SettingRow>
            )}
            <SettingRow title="Language" hint={`The welcome is written in ${languageName}.`}>
              <div className="inline-flex rounded-lg bg-slate-100 p-0.5 dark:bg-white/5" role="group" aria-label="Language">
                {LANGUAGES.map((l) => (
                  <button
                    key={l.id}
                    type="button"
                    disabled={!canEdit}
                    aria-pressed={language === l.id}
                    onClick={() => setLanguage(l.id)}
                    className={cn(
                      'rounded-md px-3 py-1.5 text-sm font-medium transition',
                      language === l.id ? 'bg-white text-slate-900 shadow-sm dark:bg-slate-700 dark:text-white' : 'text-slate-500 hover:text-slate-800',
                    )}
                  >
                    {l.label}
                  </button>
                ))}
              </div>
            </SettingRow>
          </div>
        </SettingsCard>
      </div>

      <SettingsCard
        id={SECTION.email}
        icon={Mail}
        tone="orange"
        title="Welcome email"
        description={DESIGNS.find((d) => d.id === design)?.hint}
      >
        <div className="grid gap-6 xl:grid-cols-2">
          <div className="min-w-0 space-y-4">
            <div className="grid grid-cols-3 gap-1 rounded-xl bg-slate-100 p-1 dark:bg-white/5" role="radiogroup" aria-label="How the email is made">
              {DESIGNS.map((d) => (
                <button
                  key={d.id}
                  type="button"
                  role="radio"
                  aria-checked={design === d.id}
                  disabled={!canEdit}
                  onClick={() => chooseDesign(d.id)}
                  className={cn(
                    'inline-flex items-center justify-center gap-1.5 rounded-lg px-2 py-2 text-sm font-medium transition',
                    design === d.id ? 'bg-white text-slate-900 shadow-sm dark:bg-slate-700 dark:text-white' : 'text-slate-500 hover:text-slate-800',
                  )}
                >
                  <d.icon className="h-4 w-4 shrink-0" aria-hidden /> <span className="truncate">{d.label}</span>
                </button>
              ))}
            </div>

            <div>
              <Label htmlFor="welcome-subject">Subject</Label>
              <Input
                id="welcome-subject"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                onFocus={typingIn('welcome-subject')}
                placeholder={language === 'fr' ? 'Bienvenue au club, {{firstName}}' : 'Welcome to the club, {{firstName}}'}
                disabled={!canEdit}
                maxLength={300}
                className="mt-1"
              />
            </div>

            {design === 'portal' && (
              <div className="flex items-start gap-3 rounded-xl border border-dashed border-slate-300 p-4 text-sm text-slate-600">
                <LayoutTemplate className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-hidden />
                <p>
                  The portal sends its own design, with the welcome message you write when you convert a lead. To write the whole
                  email yourself, choose <strong>Simple</strong>.
                </p>
              </div>
            )}

            {design === 'simple' && simple && (
              <div className="space-y-3">
                <div>
                  <Label htmlFor="simple-heading">Heading</Label>
                  <Input
                    id="simple-heading"
                    value={simple.heading}
                    onChange={(e) => editSimple({ heading: e.target.value })}
                    onFocus={typingIn('simple-heading')}
                    disabled={!canEdit}
                    maxLength={200}
                    className="mt-1 text-base"
                  />
                </div>
                <div>
                  <div className="flex items-baseline justify-between gap-2">
                    <Label htmlFor="simple-message">Message</Label>
                    <span className="text-[11px] text-slate-400">Blank line: new paragraph · **bold**</span>
                  </div>
                  <Textarea
                    id="simple-message"
                    value={simple.message}
                    onChange={(e) => editSimple({ message: e.target.value })}
                    onFocus={typingIn('simple-message')}
                    disabled={!canEdit}
                    rows={7}
                    className="mt-1 min-h-[9rem] leading-relaxed"
                  />
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <Label htmlFor="simple-button">Button</Label>
                    <Input
                      id="simple-button"
                      value={simple.button}
                      onChange={(e) => editSimple({ button: e.target.value })}
                      onFocus={typingIn('simple-button')}
                      disabled={!canEdit}
                      maxLength={80}
                      className="mt-1"
                    />
                    <p className="mt-1 text-[11px] text-slate-400">Opens the page to set their password.</p>
                  </div>
                  <div>
                    <p className="text-sm font-medium text-slate-700" id="simple-color-label">Colour</p>
                    <div className="mt-1.5 flex flex-wrap items-center gap-1.5" role="radiogroup" aria-labelledby="simple-color-label">
                      {SIMPLE_COLORS.map((c) => (
                        <button
                          key={c}
                          type="button"
                          role="radio"
                          aria-checked={simple.color.toLowerCase() === c}
                          aria-label={c}
                          disabled={!canEdit}
                          onClick={() => editSimple({ color: c })}
                          className={cn(
                            'h-7 w-7 rounded-full ring-offset-2 transition hover:scale-110 dark:ring-offset-slate-900',
                            simple.color.toLowerCase() === c ? 'ring-2 ring-slate-900 dark:ring-white' : 'ring-1 ring-black/10',
                          )}
                          style={{ background: c }}
                        />
                      ))}
                      <label className="relative h-7 w-7 cursor-pointer overflow-hidden rounded-full ring-1 ring-black/10" title="Another colour">
                        <span className="sr-only">Another colour</span>
                        <span aria-hidden className="absolute inset-0 bg-[conic-gradient(#f43f5e,#f59e0b,#22c55e,#3b82f6,#a855f7,#f43f5e)]" />
                        <input
                          type="color"
                          value={simple.color}
                          disabled={!canEdit}
                          onChange={(e) => editSimple({ color: e.target.value })}
                          className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
                        />
                      </label>
                    </div>
                  </div>
                </div>
                <div>
                  <Label htmlFor="simple-signoff">Sign-off</Label>
                  <Textarea
                    id="simple-signoff"
                    value={simple.signoff}
                    onChange={(e) => editSimple({ signoff: e.target.value })}
                    onFocus={typingIn('simple-signoff')}
                    disabled={!canEdit}
                    rows={2}
                    className="mt-1"
                  />
                </div>
              </div>
            )}

            {design === 'html' && (
              <>
                <div className="overflow-hidden rounded-xl border border-slate-800 bg-slate-950 shadow-sm">
                  <div className="flex items-center gap-2 border-b border-white/10 px-3 py-2">
                    <span className="flex gap-1.5" aria-hidden>
                      <span className="h-2.5 w-2.5 rounded-full bg-rose-400/80" />
                      <span className="h-2.5 w-2.5 rounded-full bg-amber-400/80" />
                      <span className="h-2.5 w-2.5 rounded-full bg-emerald-400/80" />
                    </span>
                    <Label htmlFor="welcome-html" className="ml-1 font-mono text-xs font-medium text-slate-300">HTML</Label>
                    <span className="ml-auto text-[11px] text-slate-400">
                      {html.trim() ? `${html.split('\n').length} lines` : 'Empty: the portal’s design'}
                    </span>
                  </div>
                  <Textarea
                    id="welcome-html"
                    value={html}
                    onChange={(e) => setHtml(e.target.value)}
                    onFocus={typingIn('welcome-html')}
                    placeholder="<div>…your email's HTML…</div>"
                    rows={14}
                    spellCheck={false}
                    disabled={!canEdit}
                    className="max-h-[28rem] min-h-[11rem] overflow-y-auto rounded-none border-0 bg-transparent font-mono text-xs leading-relaxed text-emerald-200 shadow-none placeholder:text-slate-500 hover:border-0 focus-visible:ring-0 dark:bg-transparent"
                  />
                </div>

                {/<style[\s>]/i.test(html.replace(/<!--[\s\S]*?-->/g, '')) && (
                  <p className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-2.5 text-xs text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
                    <AlertTriangle className="mt-px h-3.5 w-3.5 shrink-0 text-amber-600" aria-hidden />
                    <span>
                      This email has a <code className="rounded bg-amber-100 px-1 dark:bg-amber-500/20">&lt;style&gt;</code> block. The portal removes it before sending,
                      so put the styles you need inline, on each element.
                    </span>
                  </p>
                )}

                <EmailImageTools html={html} onChange={setHtml} textareaId="welcome-html" disabled={!canEdit} />
              </>
            )}

            {canEdit && settings && (
              <div className="rounded-xl border border-slate-200 p-3">
                <div className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                  <span className="font-medium text-slate-700">Add a field</span>
                  <span className="text-slate-500">to the box you last typed in, filled in for each person</span>
                  <HelpTip label="About fields" className="-my-1 ml-auto">
                    A field is written like <code className="rounded bg-slate-100 px-1 text-xs">{'{{firstName}}'}</code> and is replaced by
                    each person&apos;s own. Orange ones are filled by CadenceE; violet ones by the portal when it sends (the links, the login).
                    Give any a fallback for someone without it: <code className="rounded bg-slate-100 px-1 text-xs">{'{{firstName|there}}'}</code>.
                  </HelpTip>
                </div>
                <div className="space-y-1.5">
                  <PlaceholderChips fields={settings.cadenceFields} tone="orange" onPick={addField} />
                  <PlaceholderChips fields={settings.receiverFields} tone="violet" onPick={addField} />
                </div>
              </div>
            )}
          </div>

          <div className="min-w-0 space-y-3 xl:sticky xl:top-4 xl:self-start">
            <div className="flex items-center gap-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Preview</p>
              <span className="truncate text-xs text-slate-400">for a sample member</span>
              <div className="ml-auto inline-flex rounded-lg bg-slate-100 p-0.5 dark:bg-white/5" role="group" aria-label="Preview size">
                {([['desktop', Monitor, 'Desktop'], ['phone', Smartphone, 'Phone']] as const).map(([d, Icon, label]) => (
                  <button
                    key={d}
                    type="button"
                    aria-pressed={device === d}
                    onClick={() => setDevice(d)}
                    className={cn(
                      'inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-xs font-medium transition',
                      device === d ? 'bg-white text-slate-900 shadow-sm dark:bg-slate-700 dark:text-white' : 'text-slate-500 hover:text-slate-800',
                    )}
                  >
                    <Icon className="h-3.5 w-3.5" aria-hidden /> {label}
                  </button>
                ))}
              </div>
            </div>
            {previewError ? (
              <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">{previewError}</p>
            ) : preview ? (
              <>
                {preview.unknown.length > 0 && (
                  <p className="rounded-lg border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">Nothing fills {preview.unknown.join(', ')}.</p>
                )}
                <EmailPreviewFrame subject={preview.subject} html={preview.html} from={fromShown || undefined} to={SAMPLE.email} replyTo={replyShown || undefined} device={device} />
              </>
            ) : (
              <div className="flex h-60 items-center justify-center rounded-xl border border-dashed border-slate-200 text-slate-400"><Loader2 className="h-5 w-5 animate-spin" /></div>
            )}

            <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3">
              <Label htmlFor="welcome-test-to" className="flex items-center gap-1.5">
                <Send className="h-3.5 w-3.5 text-orange-500" aria-hidden /> Send a test email
              </Label>
              <form
                className="mt-1.5 flex gap-2"
                onSubmit={(e) => { e.preventDefault(); void sendTestEmail(); }}
              >
                <Input
                  id="welcome-test-to"
                  type="email"
                  value={testTo}
                  onChange={(e) => { setTestTo(e.target.value); setTestEmail(null); }}
                  placeholder="name@example.com"
                  className="min-w-0 bg-white"
                />
                <Button type="submit" disabled={sendingTest || !looksLikeEmail(testTo) || Boolean(preview?.unknown.length)} className="shrink-0">
                  {sendingTest ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} Send test email
                </Button>
              </form>
              <p className="mt-1.5 text-xs text-slate-500">This email as shown here, for the sample member, marked as a test. Nothing is saved or handed to the portal.</p>
              {testEmail && (
                <p
                  role={testEmail.ok ? 'status' : 'alert'}
                  className={cn(
                    'mt-2 flex items-start gap-1.5 text-sm',
                    testEmail.ok ? 'text-emerald-700' : 'text-rose-700',
                  )}
                >
                  {testEmail.ok ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden /> : <XCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />}
                  {testEmail.text}
                </p>
              )}
            </div>
          </div>
        </div>
      </SettingsCard>

      {canEdit && (
        // Clear of the help button that floats over the page's bottom right corner.
        <div className="sticky bottom-4 z-20 mr-16">
          <div
            className={cn(
              'flex items-center gap-3 rounded-2xl border px-4 py-2.5 shadow-lg backdrop-blur transition-colors',
              dirty ? 'border-amber-200 bg-white/95 dark:border-amber-500/30 dark:bg-slate-900/95' : 'border-slate-200 bg-white/90 dark:bg-slate-900/90',
            )}
          >
            {dirty ? (
              <span className="inline-flex items-center gap-2 whitespace-nowrap text-sm font-medium text-amber-700" role="status">
                <span className="relative flex h-2 w-2" aria-hidden>
                  <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-amber-400 opacity-60 motion-reduce:animate-none" />
                  <span className="relative inline-flex h-2 w-2 rounded-full bg-amber-500" />
                </span>
                Unsaved changes
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-sm text-slate-500">
                <CheckCircle2 className="h-4 w-4 text-emerald-500" aria-hidden /> <span className="max-sm:sr-only">All changes saved</span>
              </span>
            )}
            <div className="ml-auto flex items-center gap-2">
              {dirty && (
                <Button type="button" variant="ghost" size="sm" onClick={() => settings && take(settings)} disabled={saving}>
                  <RotateCcw className="h-4 w-4" /> Discard
                </Button>
              )}
              <Button type="button" size="sm" onClick={() => void save()} disabled={saving || !dirty} className="bg-gradient-to-r from-orange-500 to-pink-500 text-white">
                {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} Save
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/** A setting on one line: what it is, a few words about it, and its control on the right. */
function SettingRow({ title, hint, children }: { title: string; hint: string; children: ReactNode }) {
  return (
    <div className="flex items-center gap-4 py-3 first:pt-0 last:pb-0">
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-slate-800">{title}</p>
        <p className="text-xs text-slate-500">{hint}</p>
      </div>
      {children}
    </div>
  );
}

type FlowStep = {
  icon: ComponentType<{ className?: string }>;
  title: string;
  text: string;
  target: string;
  status: { tone: PillTone; text: string };
};

/** The three steps from a converted lead to a welcomed member, each with where it stands; a step takes you to its settings. */
function FlowOverview({ steps }: { steps: FlowStep[] }) {
  const go = (id: string) => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  return (
    <nav
      aria-label="How a new member is welcomed"
      className="rounded-2xl border border-orange-100 bg-gradient-to-br from-orange-50 via-white to-pink-50 p-2 dark:border-white/10 dark:from-orange-500/10 dark:via-transparent dark:to-pink-500/10"
    >
      <ol className="grid gap-1 sm:grid-cols-3">
        {steps.map((step, i) => (
          <li key={step.title} className="relative">
            <button
              type="button"
              onClick={() => go(step.target)}
              className="group flex w-full items-center gap-3 rounded-xl p-3 text-left transition hover:bg-white/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:hover:bg-white/5"
            >
              <span className="relative flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-md shadow-orange-500/25">
                <step.icon className="h-[18px] w-[18px]" aria-hidden />
                <span className="absolute -right-1 -top-1 flex h-4 w-4 items-center justify-center rounded-full bg-white text-[10px] font-bold text-orange-600 ring-1 ring-orange-200 dark:bg-slate-900">
                  {i + 1}
                </span>
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-semibold text-slate-900">{step.title}</span>
                <span className="block truncate text-xs text-slate-500">{step.text}</span>
                <StatusPill tone={step.status.tone} className="mt-1.5">{step.status.text}</StatusPill>
              </span>
            </button>
            {i < steps.length - 1 && (
              <ArrowRight
                className="pointer-events-none absolute -right-2.5 top-1/2 z-10 hidden h-4 w-4 -translate-y-1/2 text-orange-300 sm:block"
                aria-hidden
              />
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
