import { Users, UserCheck, TrendingUp, Target, Zap, Clock } from 'lucide-react';
import type { LeadStats as LeadStatsType } from './types';

interface LeadStatsProps {
  stats: LeadStatsType;
}

export function LeadStats({ stats }: LeadStatsProps) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-6">
      {/* Total Leads */}
      <div className="bg-white rounded-xl border border-slate-200 p-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center">
            <Users className="w-5 h-5 text-blue-600" />
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-800">{stats.total}</p>
            <p className="text-xs text-slate-500">Total Leads</p>
          </div>
        </div>
      </div>

      {/* New Leads */}
      <div className="bg-white rounded-xl border border-slate-200 p-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-cyan-100 flex items-center justify-center">
            <Target className="w-5 h-5 text-cyan-600" />
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-800">{stats.newLeads}</p>
            <p className="text-xs text-slate-500">New</p>
          </div>
        </div>
      </div>

      {/* Contacted */}
      <div className="bg-white rounded-xl border border-slate-200 p-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-amber-100 flex items-center justify-center">
            <Clock className="w-5 h-5 text-amber-600" />
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-800">{stats.contacted}</p>
            <p className="text-xs text-slate-500">Contacted</p>
          </div>
        </div>
      </div>

      {/* Qualified */}
      <div className="bg-white rounded-xl border border-slate-200 p-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center">
            <UserCheck className="w-5 h-5 text-emerald-600" />
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-800">{stats.qualified}</p>
            <p className="text-xs text-slate-500">Qualified</p>
          </div>
        </div>
      </div>

      {/* Hot Leads */}
      <div className="bg-white rounded-xl border border-slate-200 p-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-red-100 flex items-center justify-center">
            <Zap className="w-5 h-5 text-red-600" />
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-800">{stats.hotLeads}</p>
            <p className="text-xs text-slate-500">Hot</p>
          </div>
        </div>
      </div>

      {/* Conversion Rate */}
      <div className="bg-white rounded-xl border border-slate-200 p-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-purple-100 flex items-center justify-center">
            <TrendingUp className="w-5 h-5 text-purple-600" />
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-800">{stats.conversionRate}%</p>
            <p className="text-xs text-slate-500">Conversion</p>
          </div>
        </div>
      </div>
    </div>
  );
}

export default LeadStats;
