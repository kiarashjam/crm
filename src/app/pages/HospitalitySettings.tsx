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
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
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

const TIER_COLOR: Record<MemberTier, string> = {
  Bronze: 'from-amber-400 to-amber-600',
  Silver: 'from-slate-400 to-slate-600',
  Gold: 'from-yellow-400 to-yellow-600',
  Platinum: 'from-indigo-400 to-indigo-600',
};

export default function HospitalitySettings() {
  const [settings, setSettings] = useState<Settings_ | null>(null);
  const [loading, setLoading] = useState(true);
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
        <div className="flex items-center justify-center py-20 text-slate-400">
          <Loader2 className="h-6 w-6 animate-spin" />
        </div>
      </div>
    );
  }

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
      await updateHospitalitySettings(settings);
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
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-indigo-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-fuchsia-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-indigo-500 to-fuchsia-500 flex items-center justify-center shadow-2xl shadow-indigo-500/30">
                    <Settings className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">
                      Hospitality Settings
                    </h1>
                    <p className="text-slate-400 mt-1">
                      Configure tiers, dues, loyalty multipliers, and venue point values.
                    </p>
                  </div>
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    onClick={reset}
                    disabled={resetting}
                    className="bg-white/10 text-white border-white/20 hover:bg-white/20 hover:text-white"
                  >
                    {resetting ? (
                      <Loader2 className="w-4 h-4 animate-spin" />
                    ) : (
                      <>
                        <RotateCcw className="w-4 h-4 mr-1.5" /> Reset to defaults
                      </>
                    )}
                  </Button>
                  <Button
                    onClick={save}
                    disabled={saving}
                    className="bg-white text-slate-900 hover:bg-slate-100"
                  >
                    {saving ? (
                      <Loader2 className="w-4 h-4 animate-spin" />
                    ) : (
                      <>
                        <Save className="w-4 h-4 mr-1.5" /> Save changes
                      </>
                    )}
                  </Button>
                </div>
              </div>
            </div>
          </div>

          <Tabs defaultValue="tiers">
            <TabsList className="mb-4">
              <TabsTrigger value="tiers">Membership tiers</TabsTrigger>
              <TabsTrigger value="loyalty">Loyalty rules</TabsTrigger>
              <TabsTrigger value="venues">Venue points</TabsTrigger>
            </TabsList>

            <TabsContent value="tiers">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {MEMBER_TIERS.map((tier) => {
                  const cfg = settings.tiers.find((t) => t.tier === tier);
                  if (!cfg) return null;
                  return (
                    <article
                      key={tier}
                      className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden"
                    >
                      <div className={`h-3 bg-gradient-to-r ${TIER_COLOR[tier]}`} />
                      <div className="p-5">
                        <div className="flex items-center gap-2 mb-4">
                          <Crown className="w-5 h-5 text-indigo-600" />
                          <h3 className="font-semibold text-slate-900">{tier}</h3>
                        </div>

                        <div className="grid grid-cols-2 gap-3 mb-4">
                          <div>
                            <Label className="text-xs">Monthly dues ($)</Label>
                            <Input
                              type="number"
                              min="0"
                              value={cfg.monthlyDues}
                              onChange={(e) =>
                                updateTier(tier, { monthlyDues: Number(e.target.value) || 0 })
                              }
                            />
                          </div>
                          <div>
                            <Label className="text-xs">Initiation fee ($)</Label>
                            <Input
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
                            <Input
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
                                className="rounded p-0.5 text-slate-400 hover:text-rose-600"
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
                          <Button size="sm" variant="outline" onClick={() => addBenefit(tier)}>
                            <Plus className="w-4 h-4" />
                          </Button>
                        </div>
                      </div>
                    </article>
                  );
                })}
              </div>
            </TabsContent>

            <TabsContent value="loyalty">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 max-w-2xl">
                <h3 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-4">
                  <Sparkles className="w-4 h-4 text-amber-500" /> Earning & redemption rates
                </h3>
                <div className="space-y-4">
                  <div>
                    <Label>Base points per $1 spent</Label>
                    <Input
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
                    <Label>Points required per $1 of redemption value</Label>
                    <Input
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
                      100 points = $1 by default — adjust the rate as needed.
                    </p>
                  </div>
                  <div>
                    <Label>Event check-in points (base)</Label>
                    <Input
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
                      <Input
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
                      <Input
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
                <h3 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-4">
                  <ScanLine className="w-4 h-4 text-cyan-500" /> Points awarded per check-in
                </h3>
                <p className="text-sm text-slate-500 mb-4">
                  These are base points — actual points earned are multiplied by the visiting
                  member's tier multiplier.
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

          <p className="text-xs text-slate-400 mt-8">
            Changes apply to all new transactions once saved. Existing ledger entries and charges are
            not retroactively recalculated.
          </p>
        </main>
      </PageTransition>
    </div>
  );
}
