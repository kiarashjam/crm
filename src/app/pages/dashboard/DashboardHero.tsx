import { Link } from 'react-router-dom';
import {
  TrendingUp,
  Users,
  Briefcase,
  Target,
  Calendar,
  Plus,
  ArrowUpRight,
} from 'lucide-react';
import type { DashboardStats } from './types';

interface DashboardHeroProps {
  displayName: string;
  stats: DashboardStats | null;
}

export function DashboardHero({ displayName, stats }: DashboardHeroProps) {
  return (
    <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 overflow-hidden rounded-2xl mb-8">
      {/* Animated background elements */}
      <div className="absolute inset-0">
        <div className="absolute top-0 left-1/4 w-96 h-96 bg-orange-500/20 rounded-full blur-[120px]" />
        <div className="absolute bottom-0 right-1/4 w-96 h-96 bg-violet-500/20 rounded-full blur-[120px]" />
        <div 
          className="absolute inset-0 opacity-[0.03]"
          style={{
            backgroundImage: `linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)`,
            backgroundSize: '40px 40px',
          }}
        />
      </div>

      <div className="relative px-6 lg:px-8 py-8 lg:py-12">
        {/* Welcome & Date */}
        <div className="flex flex-col lg:flex-row lg:items-end lg:justify-between gap-6 mb-8">
          <div>
            <p className="text-orange-400 text-sm font-medium mb-1 flex items-center gap-2">
              <Calendar className="w-4 h-4" />
              {new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
            </p>
            <h1 className="text-3xl lg:text-4xl font-bold text-white">
              Welcome back, <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-400 to-amber-400">{displayName}</span>
            </h1>
            <p className="mt-2 text-slate-400">Here's what's happening with your sales pipeline today.</p>
          </div>
          <div className="flex gap-3">
            <Link
              to="/deals"
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-white/10 hover:bg-white/20 border border-white/10 rounded-xl text-white text-sm font-medium transition-all"
            >
              <Plus className="w-4 h-4" />
              New Deal
            </Link>
            <Link
              to="/leads"
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 rounded-xl text-white text-sm font-medium transition-all shadow-lg shadow-orange-500/25"
            >
              <Users className="w-4 h-4" />
              Add Lead
            </Link>
          </div>
        </div>

        {/* Main Stats Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6">
          {/* Pipeline Value - Hero stat */}
          <div className="col-span-2 lg:col-span-1 relative group">
            <div className="absolute inset-0 bg-gradient-to-br from-orange-500 to-amber-500 rounded-2xl blur-xl opacity-50 group-hover:opacity-70 transition-opacity" />
            <div className="relative bg-gradient-to-br from-orange-500 to-amber-500 rounded-2xl p-5 lg:p-6 h-full">
              <div className="flex items-center justify-between mb-4">
                <div className="w-10 h-10 rounded-xl bg-white/20 flex items-center justify-center">
                  <TrendingUp className="w-5 h-5 text-white" />
                </div>
                <span className="text-xs font-medium text-white/70 bg-white/10 px-2 py-1 rounded-full">+12.5%</span>
              </div>
              <p className="text-3xl lg:text-4xl font-bold text-white mb-1">
                ${stats ? Number(stats.pipelineValue).toLocaleString() : '0'}
              </p>
              <p className="text-sm text-white/80">Total Pipeline Value</p>
            </div>
          </div>

          {/* Active Leads */}
          <div className="bg-white/5 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/10 transition-all group">
            <div className="flex items-center justify-between mb-3">
              <div className="w-10 h-10 rounded-xl bg-blue-500/20 flex items-center justify-center">
                <Users className="w-5 h-5 text-blue-400" />
              </div>
              <ArrowUpRight className="w-4 h-4 text-slate-500 group-hover:text-blue-400 transition-colors" />
            </div>
            <p className="text-2xl lg:text-3xl font-bold text-white mb-1">{stats?.activeLeadsCount ?? 0}</p>
            <p className="text-sm text-slate-400">Active Leads</p>
          </div>

          {/* Open Deals */}
          <div className="bg-white/5 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/10 transition-all group">
            <div className="flex items-center justify-between mb-3">
              <div className="w-10 h-10 rounded-xl bg-violet-500/20 flex items-center justify-center">
                <Briefcase className="w-5 h-5 text-violet-400" />
              </div>
              <ArrowUpRight className="w-4 h-4 text-slate-500 group-hover:text-violet-400 transition-colors" />
            </div>
            <p className="text-2xl lg:text-3xl font-bold text-white mb-1">{stats?.activeDealsCount ?? 0}</p>
            <p className="text-sm text-slate-400">Open Deals</p>
          </div>

          {/* Won vs Lost */}
          <div className="bg-white/5 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/10 transition-all">
            <div className="flex items-center justify-between mb-3">
              <div className="w-10 h-10 rounded-xl bg-emerald-500/20 flex items-center justify-center">
                <Target className="w-5 h-5 text-emerald-400" />
              </div>
            </div>
            <div className="flex items-baseline gap-3">
              <div>
                <p className="text-2xl font-bold text-emerald-400">{stats?.dealsWonCount ?? 0}</p>
                <p className="text-xs text-slate-500">Won</p>
              </div>
              <span className="text-slate-600">/</span>
              <div>
                <p className="text-2xl font-bold text-red-400">{stats?.dealsLostCount ?? 0}</p>
                <p className="text-xs text-slate-500">Lost</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default DashboardHero;
