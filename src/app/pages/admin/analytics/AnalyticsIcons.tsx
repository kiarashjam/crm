// The small pictures the analytics put beside a name: where a visit came from, and the
// device, browser and system it used. A known source gets its colour and mark; anything
// else a neutral globe. Every icon is decorative: the name always stands beside it.

import type { ComponentType } from 'react';
import {
  Apple, Bot, Chrome, Compass, Facebook, Flame, Globe, Instagram, Laptop, Link2, Linkedin, Mail, Megaphone, Monitor,
  Smartphone, Sparkles, Tablet, Terminal,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';

type Icon = ComponentType<{ className?: string }>;

interface SourceLook {
  /** A lucide icon, or letters drawn in the tile for a brand lucide has no mark for. */
  icon?: Icon;
  letters?: string;
  tile: string;
}

const SOURCE_LOOK: Record<string, SourceLook> = {
  google: { letters: 'G', tile: 'bg-white text-[#4285f4] ring-slate-200 dark:bg-slate-900 dark:ring-white/15' },
  bing: { letters: 'b', tile: 'bg-teal-50 text-teal-700 ring-teal-100 dark:bg-teal-500/10 dark:text-teal-300 dark:ring-teal-500/20' },
  duckduckgo: { letters: 'D', tile: 'bg-orange-50 text-[#de5833] ring-orange-100 dark:bg-orange-500/10 dark:text-orange-300 dark:ring-orange-500/20' },
  linkedin: { icon: Linkedin, tile: 'bg-[#0a66c2] text-white ring-[#0a66c2]/30' },
  facebook: { icon: Facebook, tile: 'bg-[#1877f2] text-white ring-[#1877f2]/30' },
  instagram: { icon: Instagram, tile: 'bg-gradient-to-br from-amber-400 via-pink-500 to-violet-600 text-white ring-pink-500/30' },
  x: { letters: '𝕏', tile: 'bg-black text-white ring-black/30 dark:bg-white dark:text-black dark:ring-white/30' },
  ai: { icon: Sparkles, tile: 'bg-violet-50 text-violet-600 ring-violet-100 dark:bg-violet-500/10 dark:text-violet-300 dark:ring-violet-500/20' },
  email: { icon: Mail, tile: 'bg-sky-50 text-sky-600 ring-sky-100 dark:bg-sky-500/10 dark:text-sky-300 dark:ring-sky-500/20' },
  direct: { icon: Link2, tile: 'bg-slate-100 text-slate-600 ring-slate-200 dark:bg-white/5 dark:text-slate-300 dark:ring-white/10' },
};

const CAMPAIGN_LOOK: SourceLook = { icon: Megaphone, tile: 'bg-amber-50 text-amber-700 ring-amber-100 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-500/20' };
const HOST_LOOK: SourceLook = { icon: Globe, tile: 'bg-slate-100 text-slate-500 ring-slate-200 dark:bg-white/5 dark:text-slate-300 dark:ring-white/10' };

/** Hosts that belong to a known source: a referrer from www.linkedin.com wears LinkedIn's mark. */
const HOST_SOURCES: [RegExp, string][] = [
  [/(^|\.)google\./, 'google'],
  [/(^|\.)bing\./, 'bing'],
  [/(^|\.)duckduckgo\./, 'duckduckgo'],
  [/(^|\.)(linkedin\.|lnkd\.in$)/, 'linkedin'],
  [/(^|\.)(facebook\.|fb\.)/, 'facebook'],
  [/(^|\.)instagram\./, 'instagram'],
  [/(^|\.)(x\.com$|t\.co$|twitter\.)/, 'x'],
  [/(^|\.)(chatgpt\.com$|openai\.com$|perplexity\.ai$|claude\.ai$|gemini\.google\.com$|copilot\.microsoft\.com$)/, 'ai'],
];

/** A known source by name or host; any other host a globe; a utm source a megaphone. */
function lookOf(source: string): SourceLook {
  const s = source.toLowerCase();
  const named = SOURCE_LOOK[s];
  if (named) return named;
  if (!s.includes('.')) return CAMPAIGN_LOOK;
  const ai = HOST_SOURCES[HOST_SOURCES.length - 1]!;
  if (ai[0].test(s)) return SOURCE_LOOK.ai!;
  const known = HOST_SOURCES.find(([pattern]) => pattern.test(s));
  return (known && SOURCE_LOOK[known[1]]) || HOST_LOOK;
}

/** Where a visit came from, as a small tile in that source's colour. */
export function SourceIcon({ source, size = 'md', className }: { source: string; size?: 'sm' | 'md'; className?: string }) {
  const look = lookOf(source ?? '');
  const Icon = look.icon;
  return (
    <span
      aria-hidden
      className={cn(
        'inline-flex shrink-0 items-center justify-center font-bold ring-1',
        size === 'md' ? 'h-7 w-7 rounded-lg text-[13px]' : 'h-5 w-5 rounded-md text-[10px]',
        look.tile,
        className,
      )}
    >
      {Icon ? <Icon className={size === 'md' ? 'h-3.5 w-3.5' : 'h-3 w-3'} /> : look.letters}
    </span>
  );
}

/** The device: a phone, a tablet, or a screen. */
export function deviceIcon(device: string): Icon {
  const d = (device ?? '').toLowerCase();
  if (d.includes('mobile') || d.includes('phone')) return Smartphone;
  if (d.includes('tablet')) return Tablet;
  if (d.includes('bot')) return Bot;
  if (d.includes('laptop')) return Laptop;
  return Monitor;
}

/** The browser, by its family. */
export function browserIcon(browser: string): Icon {
  const b = (browser ?? '').toLowerCase();
  if (b.includes('chrome') || b.includes('chromium') || b.includes('edge') || b.includes('brave') || b.includes('opera')) return Chrome;
  if (b.includes('firefox')) return Flame;
  if (b.includes('safari')) return Compass;
  return Globe;
}

/** The operating system, by its family. */
export function osIcon(os: string): Icon {
  const o = (os ?? '').toLowerCase();
  if (o.includes('mac') || o.includes('ios') || o.includes('ipad')) return Apple;
  if (o.includes('android')) return Smartphone;
  if (o.includes('linux')) return Terminal;
  return Monitor;
}
