import { Link } from 'react-router-dom';
import { Sparkles, ArrowRight, Command, ChevronRight } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { quickNavPrimary, quickNavSecondary, quickNavUtility } from './config';

export function QuickNav() {
  return (
    <section className="relative overflow-hidden rounded-2xl border border-slate-200/60 bg-gradient-to-br from-white via-slate-50/30 to-white shadow-xl shadow-slate-200/40">
      {/* Animated background gradient */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-32 -right-32 w-64 h-64 bg-gradient-to-br from-orange-500/8 via-amber-500/5 to-transparent rounded-full blur-3xl animate-pulse" style={{ animationDuration: '4s' }} />
        <div className="absolute top-1/2 -left-24 w-48 h-48 bg-gradient-to-br from-blue-500/8 via-indigo-500/5 to-transparent rounded-full blur-3xl animate-pulse" style={{ animationDuration: '5s', animationDelay: '1s' }} />
        <div className="absolute -bottom-20 right-1/4 w-40 h-40 bg-gradient-to-br from-violet-500/8 via-purple-500/5 to-transparent rounded-full blur-3xl animate-pulse" style={{ animationDuration: '6s', animationDelay: '2s' }} />
        {/* Subtle grid pattern */}
        <div className="absolute inset-0 bg-[linear-gradient(to_right,#8882_1px,transparent_1px),linear-gradient(to_bottom,#8882_1px,transparent_1px)] bg-[size:24px_24px] opacity-[0.03]" />
      </div>

      {/* Header */}
      <div className="relative px-5 pt-5 pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="relative">
              <div className="absolute inset-0 bg-gradient-to-br from-orange-500 to-amber-500 rounded-xl blur-lg opacity-40" />
              <div className="relative flex items-center justify-center w-10 h-10 rounded-xl bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 shadow-lg ring-1 ring-white/10">
                <Sparkles className="w-5 h-5 text-amber-400" />
              </div>
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900">Quick Access</h2>
              <p className="text-xs text-slate-500">Navigate your workspace</p>
            </div>
          </div>
          <div className="hidden sm:flex items-center gap-1 px-2 py-1 rounded-md bg-slate-100/80 border border-slate-200/60">
            <Command className="w-3 h-3 text-slate-400" />
            <span className="text-[10px] font-medium text-slate-500">+ K</span>
          </div>
        </div>
      </div>

      {/* Primary navigation - Main CRM features */}
      <div className="relative px-4 pb-3">
        <div className="flex items-center gap-2 px-1 mb-2">
          <div className="h-px flex-1 bg-gradient-to-r from-slate-200 to-transparent" />
          <span className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Core</span>
          <div className="h-px flex-1 bg-gradient-to-l from-slate-200 to-transparent" />
        </div>
        <div className="grid grid-cols-3 gap-2">
          {quickNavPrimary.map((item, index) => (
            <Link
              key={item.to}
              to={item.to}
              className={cn(
                "group relative flex flex-col items-center p-3 rounded-xl",
                "transition-all duration-300 ease-out",
                "hover:scale-[1.03] active:scale-[0.97]",
                item.bgHover,
                "border border-transparent hover:border-slate-200/80",
                "hover:shadow-lg hover:shadow-slate-200/50"
              )}
              style={{ animationDelay: `${index * 40}ms` }}
            >
              {/* Hover glow */}
              <div 
                className={cn(
                  "absolute inset-0 rounded-xl opacity-0 group-hover:opacity-20 transition-opacity duration-500",
                  "bg-gradient-to-br",
                  item.gradient
                )} 
              />
              
              {/* Icon container */}
              <div className="relative mb-2">
                <div 
                  className={cn(
                    "absolute inset-0 rounded-xl blur-md opacity-0 group-hover:opacity-60 transition-opacity duration-300",
                    "bg-gradient-to-br",
                    item.gradient
                  )} 
                />
                <div className={cn(
                  "relative w-11 h-11 rounded-xl flex items-center justify-center",
                  "bg-gradient-to-br shadow-md transition-all duration-300",
                  item.gradient,
                  "group-hover:shadow-xl group-hover:scale-105"
                )}>
                  <div className="absolute inset-0 rounded-xl bg-gradient-to-tr from-white/30 via-white/10 to-transparent" />
                  <item.icon className="relative w-5 h-5 text-white drop-shadow-sm" />
                </div>
              </div>
              
              {/* Label */}
              <span className="text-xs font-semibold text-slate-700 group-hover:text-slate-900 transition-colors text-center">
                {item.label}
              </span>
              
              {/* Keyboard shortcut */}
              <div className="absolute top-1.5 right-1.5 opacity-0 group-hover:opacity-100 transition-opacity duration-200">
                <span className="flex items-center justify-center w-4 h-4 rounded text-[9px] font-bold bg-slate-900/80 text-white">
                  {item.shortcut}
                </span>
              </div>
            </Link>
          ))}
        </div>
      </div>

      {/* Secondary navigation - Tools & Analytics */}
      <div className="relative px-4 pb-3">
        <div className="flex items-center gap-2 px-1 mb-2">
          <div className="h-px flex-1 bg-gradient-to-r from-slate-200 to-transparent" />
          <span className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Tools</span>
          <div className="h-px flex-1 bg-gradient-to-l from-slate-200 to-transparent" />
        </div>
        <div className="grid grid-cols-4 gap-2">
          {quickNavSecondary.map((item, index) => (
            <Link
              key={item.to}
              to={item.to}
              className={cn(
                "group relative flex flex-col items-center p-2.5 rounded-xl",
                "transition-all duration-300 ease-out",
                "hover:scale-[1.04] active:scale-[0.96]",
                item.bgHover,
                "border border-slate-100 hover:border-slate-200/80",
                "bg-white/50 hover:bg-white",
                "hover:shadow-md"
              )}
              style={{ animationDelay: `${(index + 6) * 40}ms` }}
            >
              <div className={cn(
                "w-9 h-9 rounded-lg flex items-center justify-center mb-1.5",
                "bg-gradient-to-br shadow-sm transition-all duration-300",
                item.gradient,
                "group-hover:shadow-md group-hover:scale-105"
              )}>
                <div className="absolute inset-0 rounded-lg bg-gradient-to-tr from-white/25 to-transparent" />
                <item.icon className="relative w-4 h-4 text-white" />
              </div>
              <span className="text-[11px] font-medium text-slate-600 group-hover:text-slate-900 transition-colors">
                {item.label}
              </span>
            </Link>
          ))}
        </div>
      </div>

      {/* Utility navigation */}
      <div className="relative px-4 pb-4">
        <div className="h-px bg-gradient-to-r from-transparent via-slate-200 to-transparent mb-3" />
        <div className="grid grid-cols-4 gap-1.5">
          {quickNavUtility.map((item, index) => (
            <Link
              key={item.to}
              to={item.to}
              className={cn(
                "group flex flex-col items-center justify-center py-2.5 px-2 rounded-xl",
                "bg-slate-50/80 hover:bg-slate-100",
                "border border-transparent hover:border-slate-200/60",
                "transition-all duration-200",
                "hover:shadow-sm"
              )}
            >
              <item.icon className="w-4 h-4 text-slate-500 group-hover:text-slate-700 mb-1 transition-all duration-200 group-hover:scale-110" />
              <span className="text-[10px] font-medium text-slate-500 group-hover:text-slate-700 transition-colors">
                {item.label}
              </span>
            </Link>
          ))}
        </div>
      </div>

      {/* Bottom action bar */}
      <div className="relative px-4 pb-4">
        <Link
          to="/deals"
          className="group flex items-center justify-between w-full p-3 rounded-xl bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 hover:from-slate-800 hover:via-slate-700 hover:to-slate-800 transition-all duration-300 shadow-lg hover:shadow-xl"
        >
          <div className="flex items-center gap-3">
            <div className="flex items-center justify-center w-8 h-8 rounded-lg bg-gradient-to-br from-orange-500 to-amber-500 shadow-md shadow-orange-500/30">
              <Sparkles className="w-4 h-4 text-white" />
            </div>
            <div className="text-left">
              <p className="text-sm font-semibold text-white">View Pipeline</p>
              <p className="text-[11px] text-slate-400">Track all your active deals</p>
            </div>
          </div>
          <div className="flex items-center gap-1 text-slate-400 group-hover:text-white transition-colors">
            <span className="text-xs font-medium">Open</span>
            <ChevronRight className="w-4 h-4 transition-transform group-hover:translate-x-0.5" />
          </div>
        </Link>
      </div>
    </section>
  );
}

export default QuickNav;
