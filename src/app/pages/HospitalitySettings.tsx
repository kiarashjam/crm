import { useEffect, useState } from 'react';
import {
  Settings,
  Plus,
  X,
  Save,
  RotateCcw,
  Crown,
  Sparkles,
  ScanLine,
  Loader2,
  Coins,
  Info,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  getHospitalitySettings,
  updateHospitalitySettings,
  resetHospitalitySettings,
  type HospitalitySettings as Settings_,
  type TierConfig,
} from '@/app/api/hospitalitySettings';
import { MEMBER_TIERS, type MemberTier } from '@/app/api/members';
import { VISIT_VENUES, type VisitVenue } from '@/app/api/visits';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { setClubCurrency } from '@/app/hooks/useClubMoney';
import { CLUB_CURRENCIES, normaliseCurrency } from '@/app/lib/clubMoney';

const TIER_COLOR: Record<MemberTier, string> = {
  Bronze: 'from-amber-400 to-amber-600',
  Silver: 'from-slate-400 to-slate-600',
  Gold: 'from-yellow-400 to-yellow-600',
  Platinum: 'from-indigo-400 to-indigo-600',
};

export default function HospitalitySettings() {
  const [settings, setSettings] = useState<Settings_ | null>(null);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [newBenefit, setNewBenefit] = useState<Record<MemberTier, string>>({
    Bronze: '',
    Silver: '',
    Gold: '',
    Platinum: '',
  });

  const load = async () => {
    try {
      setSettings(await getHospitalitySettings());
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  if (loading || !settings) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <div className="flex items-center justify-center py-20 text-slate-500">
          <Loader2 className="h-6 w-6 animate-spin" />
        </div>
      </div>
    );
  }

  const currency = normaliseCurrency(settings.currency);

  const updateTier = (tier: MemberTier, patch: Partial<TierConfig>) => {
    setSettings({
      ...settings,
      tiers: settings.tiers.map((t) => (t.tier === tier ? { ...t, ...patch } : t)),
    });
  };

  const addBenefit = (tier: MemberTier) => {
    const text = newBenefit[tier].trim();
    if (!text) return;
    updateTier(tier, {
      benefits: [...(settings.tiers.find((t) => t.tier === tier)?.benefits ?? []), text],
    });
    setNewBenefit({ ...newBenefit, [tier]: '' });
  };

  const removeBenefit = (tier: MemberTier, index: number) => {
    const tierCfg = settings.tiers.find((t) => t.tier === tier);
    if (!tierCfg) return;
    updateTier(tier, { benefits: tierCfg.benefits.filter((_, i) => i !== index) });
  };

  const updateVenuePoints = (venue: VisitVenue, value: number) => {
    setSettings({
      ...settings,
      visitPointsByVenue: { ...settings.visitPointsByVenue, [venue]: value },
    });
  };

  const save = async () => {
    setSaving(true);
    try {
      const saved = await updateHospitalitySettings(settings);
      setClubCurrency(saved.currency);
      toast.success('Settings saved');
    } finally {
      setSaving(false);
    }
  };

  const reset = async () => {
    if (!confirm('Reset all hospitality settings to defaults? This cannot be undone.')) return;
    setResetting(true);
    try {
      const fresh = await resetHospitalitySettings();
      setSettings(fresh);
      setClubCurrency(fresh.currency);
      toast.success('Settings reset');
    } finally {
      setResetting(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <PageHero
            icon={Settings}
            tone="indigo"
            title="Hospitality Settings"
            subtitle="The club currency, plus tiers, dues, loyalty rates and venue points saved for later."
            actions={
              <>
                <Button variant="outline" onClick={reset} disabled={resetting} className={heroSecondary}>
                  {resetting ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <>
                      <RotateCcw className="h-4 w-4" /> Reset to defaults
                    </>
                  )}
                </Button>
                <Button onClick={save} disabled={saving} className={heroPrimary('indigo')}>
                  {saving ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <>
                      <Save className="h-4 w-4" /> Save changes
                    </>
                  )}
                </Button>
              </>
            }
          />

          <div className="mb-6 max-w-2xl rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">
              <Coins className="h-4 w-4 text-emerald-600" /> Club currency
            </h3>
            <p className="mb-3 text-sm text-slate-500">
              Every amount on the club pages and on printed statements is shown in this currency. Changing it
              relabels amounts already entered; it does not convert them.
            </p>
            <Select
              value={currency}
              onValueChange={(v) => setSettings({ ...settings, currency: v })}
            >
              <SelectTrigger aria-label="Club currency" className="max-w-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CLUB_CURRENCIES.map((c) => (
                  <SelectItem key={c.code} value={c.code}>
                    {c.code} — {c.name}
                  </SelectItem>
                ))}
                {!CLUB_CURRENCIES.some((c) => c.code === currency) && (
                  <SelectItem value={currency}>{currency}</SelectItem>
                )}
              </SelectContent>
            </Select>
          </div>

          <div
            role="note"
            className="mb-4 max-w-4xl rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900"
          >
            <p className="inline-flex items-center gap-2 font-medium">
              <Info className="h-4 w-4 shrink-0 text-amber-700" aria-hidden /> Only the club currency is applied today.
            </p>
            <p className="mt-1">
              The tiers, dues, loyalty rates and venue points below are saved, but nothing in Cadence reads them yet.
              Closing a tab, checking a member in and an event check-in still award the built-in points, and each
              member&apos;s dues are the ones set on that member.
            </p>
          </div>

          <Tabs defaultValue="tiers">
            <TabsList className="mb-4 max-w-full justify-start overflow-x-auto">
              <TabsTrigger value="tiers">Membership tiers</TabsTrigger>
              <TabsTrigger value="loyalty">Loyalty rules</TabsTrigger>
              <TabsTrigger value="venues">Venue points</TabsTrigger>
            </TabsList>

            <TabsContent value="tiers">
              <p className="mb-3 text-sm text-slate-500">
                <NotApplied /> Dues and initiation fees are not charged from here, multipliers do not change the points
                anyone earns, and benefits are not shown to members.
              </p>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {MEMBER_TIERS.map((tier, index) => {
                  const cfg = settings.tiers.find((t) => t.tier === tier);
                  if (!cfg) return null;
                  return (
                    <StaggerItem
                      as="article"
                      key={tier}
                      index={index}
                      className={`bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden hover:border-indigo-200 ${liftOnHover}`}
                    >
                      <div className={`h-3 bg-gradient-to-r ${TIER_COLOR[tier]}`} />
                      <div className="p-5">
                        <div className="flex items-center gap-2 mb-4">
                          <Crown className="w-5 h-5 text-indigo-600" />
                          <h3 className="font-semibold text-slate-900">{tier}</h3>
                        </div>

                        <div className="grid grid-cols-2 gap-3 mb-4">
                          <div>
                            <Label className="text-xs">Monthly dues ({currency})</Label>
                            <Input aria-label={`Monthly dues (${currency})`}
                              type="number"
                              min="0"
                              value={cfg.monthlyDues}
                              onChange={(e) =>
                                updateTier(tier, { monthlyDues: Number(e.target.value) || 0 })
                              }
                            />
                          </div>
                          <div>
                            <Label className="text-xs">Initiation fee ({currency})</Label>
                            <Input aria-label={`Initiation fee (${currency})`}
                              type="number"
                              min="0"
                              value={cfg.initiationFee}
                              onChange={(e) =>
                                updateTier(tier, { initiationFee: Number(e.target.value) || 0 })
                              }
                            />
                          </div>
                          <div className="col-span-2">
                            <Label className="text-xs">Loyalty multiplier (×)</Label>
                            <Input aria-label="Loyalty multiplier (×)"
                              type="number"
                              min="0"
                              step="0.05"
                              value={cfg.loyaltyMultiplier}
                              onChange={(e) =>
                                updateTier(tier, { loyaltyMultiplier: Number(e.target.value) || 1 })
                              }
                            />
                          </div>
                        </div>

                        <Label className="text-xs">Benefits</Label>
                        <ul className="space-y-1 mt-1 mb-2">
                          {cfg.benefits.map((b, i) => (
                            <li
                              key={i}
                              className="flex items-center justify-between gap-2 text-sm bg-slate-50 rounded px-2 py-1.5"
                            >
                              <span className="text-slate-700">{b}</span>
                              <button
                                type="button"
                                onClick={() => removeBenefit(tier, i)}
                                className="-m-1 rounded p-1.5 text-slate-500 hover:text-rose-600"
                                aria-label="Remove benefit"
                              >
                                <X className="w-3.5 h-3.5" />
                              </button>
                            </li>
                          ))}
                        </ul>
                        <div className="flex gap-2">
                          <Input
                            placeholder="Add a benefit..."
                            value={newBenefit[tier]}
                            onChange={(e) =>
                              setNewBenefit({ ...newBenefit, [tier]: e.target.value })
                            }
                            onKeyDown={(e) => {
                              if (e.key === 'Enter') {
                                e.preventDefault();
                                addBenefit(tier);
                              }
                            }}
                          />
                          <Button size="sm" variant="outline" onClick={() => addBenefit(tier)} aria-label={`Add a ${tier} benefit`}>
                            <Plus className="w-4 h-4" />
                          </Button>
                        </div>
                      </div>
                    </StaggerItem>
                  );
                })}
              </div>
            </TabsContent>

            <TabsContent value="loyalty">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 max-w-2xl">
                <h3 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-1">
                  <Sparkles className="w-4 h-4 text-amber-500" /> Earning & redemption rates
                </h3>
                <p className="mb-4 text-sm text-slate-500">
                  <NotApplied /> A tab still earns 10 points per 1 {currency} and an event check-in 50 points, both
                  times the built-in tier multipliers; 100 points are still worth 1 {currency}; and no referral or
                  anniversary bonus is awarded automatically.
                </p>
                <div className="space-y-4">
                  <div>
                    <Label>Base points per 1 {currency} spent</Label>
                    <Input aria-label={`Base points per 1 ${currency} spent`}
                      type="number"
                      min="1"
                      value={settings.basePointsPerDollar}
                      onChange={(e) =>
                        setSettings({
                          ...settings,
                          basePointsPerDollar: Number(e.target.value) || 1,
                        })
                      }
                    />
                    <p className="text-xs text-slate-500 mt-1">
                      Multiplied by the tier's loyalty multiplier on every purchase.
                    </p>
                  </div>
                  <div>
                    <Label>Points required per 1 {currency} of redemption value</Label>
                    <Input aria-label={`Points required per 1 ${currency} of redemption value`}
                      type="number"
                      min="1"
                      value={settings.pointsPerDollarRedemption}
                      onChange={(e) =>
                        setSettings({
                          ...settings,
                          pointsPerDollarRedemption: Number(e.target.value) || 100,
                        })
                      }
                    />
                    <p className="text-xs text-slate-500 mt-1">
                      100 points = 1 {currency} by default — adjust the rate as needed.
                    </p>
                  </div>
                  <div>
                    <Label>Event check-in points (base)</Label>
                    <Input aria-label="Event check-in points (base)"
                      type="number"
                      min="0"
                      value={settings.eventCheckInPoints}
                      onChange={(e) =>
                        setSettings({
                          ...settings,
                          eventCheckInPoints: Number(e.target.value) || 0,
                        })
                      }
                    />
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <Label>Referral bonus (points)</Label>
                      <Input aria-label="Referral bonus (points)"
                        type="number"
                        min="0"
                        value={settings.referralBonus}
                        onChange={(e) =>
                          setSettings({
                            ...settings,
                            referralBonus: Number(e.target.value) || 0,
                          })
                        }
                      />
                    </div>
                    <div>
                      <Label>Anniversary bonus (points)</Label>
                      <Input aria-label="Anniversary bonus (points)"
                        type="number"
                        min="0"
                        value={settings.anniversaryBonus}
                        onChange={(e) =>
                          setSettings({
                            ...settings,
                            anniversaryBonus: Number(e.target.value) || 0,
                          })
                        }
                      />
                    </div>
                  </div>
                </div>
              </div>
            </TabsContent>

            <TabsContent value="venues">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 max-w-2xl">
                <h3 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-1">
                  <ScanLine className="w-4 h-4 text-cyan-500" /> Points awarded per check-in
                </h3>
                <p className="text-sm text-slate-500 mb-4">
                  <NotApplied /> Check-ins still award the built-in points for each venue, multiplied by the
                  member&apos;s tier.
                </p>
                <div className="grid grid-cols-2 gap-4">
                  {VISIT_VENUES.map((v) => (
                    <div key={v}>
                      <Label>{v}</Label>
                      <Input
                        type="number"
                        min="0"
                        value={settings.visitPointsByVenue[v]}
                        onChange={(e) =>
                          updateVenuePoints(v, Number(e.target.value) || 0)
                        }
                      />
                    </div>
                  ))}
                </div>
              </div>
            </TabsContent>
          </Tabs>

          <p className="text-xs text-slate-500 mt-8">
            Saving keeps every value here. Of them, only the club currency changes anything today.
          </p>
        </main>
      </PageTransition>
    </div>
  );
}

/** Said next to every setting that is saved but not yet read by anything. */
function NotApplied() {
  return (
    <span className="mr-1 inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 ring-1 ring-amber-200">
      Not applied yet
    </span>
  );
}
