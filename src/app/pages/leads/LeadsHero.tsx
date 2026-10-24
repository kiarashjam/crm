import { motion } from 'motion/react';
import { Download, Link2, Plus, SlidersHorizontal, Upload, Users } from 'lucide-react';
import { Reveal } from '@/app/components/motion/PageEnter';
import { Button } from '@/app/components/ui/button';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

/** Soft lights that drift behind the header, each on its own slow loop. */
const LIGHTS = [
  { className: '-right-16 -top-24 h-72 w-72 bg-orange-300/40', drift: { x: [0, -24, 0], y: [0, 18, 0] }, duration: 14 },
  { className: 'right-1/3 -bottom-28 h-64 w-64 bg-fuchsia-300/30', drift: { x: [0, 30, 0], y: [0, -14, 0] }, duration: 17 },
  { className: '-left-20 -top-16 h-56 w-56 bg-sky-300/30', drift: { x: [0, 20, 0], y: [0, 22, 0] }, duration: 19 },
];

const SECONDARY =
  'h-10 gap-2 rounded-xl border-slate-200/80 bg-white/80 text-slate-700 shadow-sm backdrop-blur hover:border-orange-300 hover:bg-white hover:text-orange-700';

interface LeadsHeroProps {
  status: string;
  readOnly: boolean;
  exporting: boolean;
  onRouting: () => void;
  onWebhook: () => void;
  onImport: () => void;
  onExport: () => void;
  onAdd: () => void;
}

/** The leads page header: a bright band with the page's actions. */
export function LeadsHero({
  status, readOnly, exporting, onRouting, onWebhook, onImport, onExport, onAdd,
}: LeadsHeroProps) {
  const reduce = useMotionPreference();
  return (
    <Reveal
      index={0}
      className="relative mb-8 overflow-hidden rounded-3xl border border-orange-100/80 bg-gradient-to-br from-orange-50 via-white to-fuchsia-50 shadow-sm"
    >
      <div aria-hidden className="pointer-events-none absolute inset-0">
        {LIGHTS.map((light) => (
          <motion.span
            key={light.className}
            className={`absolute rounded-full blur-3xl ${light.className}`}
            animate={reduce ? undefined : light.drift}
            transition={{ duration: light.duration, repeat: Infinity, ease: 'easeInOut' }}
          />
        ))}
      </div>

      <div className="relative px-6 py-7 lg:px-8 lg:py-8">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-center gap-5">
            <motion.div
              className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 via-pink-500 to-fuchsia-500 shadow-xl shadow-pink-500/30"
              animate={reduce ? undefined : { y: [0, -5, 0], rotate: [0, -3, 0] }}
              transition={{ duration: 4, repeat: Infinity, ease: 'easeInOut' }}
            >
              <Users className="h-8 w-8 text-white" />
            </motion.div>
            <div>
              <h1 className="bg-gradient-to-r from-orange-600 via-pink-600 to-violet-600 bg-clip-text text-2xl font-bold tracking-tight text-transparent lg:text-3xl">
                Leads
              </h1>
              <p className="mt-1 text-slate-500">{status}</p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            {!readOnly && (
              <>
                <Button onClick={onRouting} variant="outline" className={SECONDARY}>
                  <SlidersHorizontal className="h-4 w-4" />
                  <span className="hidden sm:inline">Routing</span>
                </Button>
                <Button onClick={onWebhook} variant="outline" className={SECONDARY}>
                  <Link2 className="h-4 w-4" />
                  <span className="hidden sm:inline">Webhook</span>
                </Button>
                <Button onClick={onImport} variant="outline" className={SECONDARY}>
                  <Upload className="h-4 w-4" />
                  <span className="hidden sm:inline">Import</span>
                </Button>
              </>
            )}
            {/* Export is read-only, so viewers keep it. */}
            <Button onClick={onExport} disabled={exporting} variant="outline" className={`${SECONDARY} disabled:opacity-60`}>
              <Download className={`h-4 w-4 ${exporting ? 'animate-pulse' : ''}`} />
              <span className="hidden sm:inline">{exporting ? 'Exporting...' : 'Export CSV'}</span>
            </Button>
            {!readOnly && (
              <Button
                onClick={onAdd}
                className="group/add relative h-10 gap-2 overflow-hidden rounded-xl bg-gradient-to-r from-orange-500 to-pink-500 px-5 font-semibold text-white shadow-lg shadow-pink-500/30 hover:opacity-95"
              >
                {!reduce && (
                  <span
                    aria-hidden
                    className="pointer-events-none absolute inset-y-0 -left-1/2 w-1/2 -skew-x-12 bg-gradient-to-r from-transparent via-white/40 to-transparent transition-transform duration-700 ease-out group-hover/add:translate-x-[320%]"
                  />
                )}
                <Plus className="h-4 w-4 transition-transform duration-300 group-hover/add:rotate-90" />
                Add Lead
              </Button>
            )}
          </div>
        </div>
      </div>
    </Reveal>
  );
}
