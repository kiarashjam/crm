import React from 'react';

interface StatCardProps {
  icon: React.ElementType;
  label: string;
  value: number | string;
  subtext?: string;
  gradient: string;
  colorClass?: string;
}

const colorMap: Record<string, { border: string; bg: string; text: string; shadow: string }> = {
  violet: { border: 'border-violet-100', bg: 'from-violet-50 to-violet-100', text: 'text-violet-600', shadow: 'hover:shadow-violet-100' },
  amber: { border: 'border-amber-100', bg: 'from-amber-50 to-amber-100', text: 'text-amber-600', shadow: 'hover:shadow-amber-100' },
  blue: { border: 'border-blue-100', bg: 'from-blue-50 to-blue-100', text: 'text-blue-600', shadow: 'hover:shadow-blue-100' },
  emerald: { border: 'border-emerald-100', bg: 'from-emerald-50 to-emerald-100', text: 'text-emerald-600', shadow: 'hover:shadow-emerald-100' },
  slate: { border: 'border-slate-200/80', bg: 'from-slate-100 to-slate-50', text: 'text-slate-600', shadow: 'hover:shadow-slate-100' },
};

export function StatCard({ 
  icon: Icon, 
  label, 
  value, 
  subtext, 
  gradient,
  colorClass = 'slate'
}: StatCardProps) {
  const colors = colorMap[colorClass] || colorMap.slate!;
  
  return (
    <div className={`group relative bg-white rounded-2xl border ${colors.border} p-5 shadow-sm hover:shadow-xl ${colors.shadow} transition-all duration-300 overflow-hidden`}>
      <div className={`absolute top-0 right-0 w-20 h-20 bg-gradient-to-br ${colors.bg} rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform`} />
      <div className="relative">
        <div className={`w-10 h-10 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center mb-3 group-hover:scale-110 transition-transform shadow-lg`}>
          <Icon className="w-5 h-5 text-white" />
        </div>
        <p className={`text-3xl font-bold ${colors.text === 'text-slate-600' ? 'text-slate-900' : colors.text} tracking-tight`}>{value}</p>
        <p className="text-xs font-medium text-slate-500 mt-1">{label}</p>
        {subtext && <p className="text-[10px] text-slate-400 mt-0.5">{subtext}</p>}
      </div>
    </div>
  );
}
