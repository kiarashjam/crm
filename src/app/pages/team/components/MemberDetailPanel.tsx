import { useState, useEffect } from 'react';
import { toast } from 'sonner';
import {
  Mail, X, Copy, BarChart3, Target, Check, Activity,
  TrendingUp, Clock, UserPlus, Phone, Handshake, Trash2,
  UserCircle, ShieldCheck
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import type { OrgMemberDto } from '@/app/api/organizations';
import { getOrgMemberActivityCounts } from '@/app/api/activities';
import { getRoleInfo } from '../config';

interface MemberDetailPanelProps {
  member: OrgMemberDto;
  onClose: () => void;
  isOwner: boolean;
  onRoleChange: (role: number) => void;
  onRemove: () => void;
}

export function MemberDetailPanel({
  member,
  onClose,
  isOwner,
  onRoleChange,
  onRemove,
}: MemberDetailPanelProps) {
  const roleInfo = getRoleInfo(member.role);
  const RoleIcon = roleInfo.icon;
  
  // HP-3 Fix: Fetch real activity counts from backend
  const [activityCount, setActivityCount] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    getOrgMemberActivityCounts()
      .then((counts) => {
        if (!cancelled) {
          setActivityCount(counts[member.userId] ?? 0);
        }
      })
      .catch(() => {
        if (!cancelled) setActivityCount(null);
      });
    return () => { cancelled = true; };
  }, [member.userId]);

  const mockStats = {
    leadsAssigned: '—' as string | number,
    dealsWon: '—' as string | number,
    tasksCompleted: '—' as string | number,
    activitiesLogged: activityCount !== null ? activityCount : '—' as string | number,
    conversionRate: '—' as string | number,
    avgResponseTime: '—' as string,
  };

  // HP-3: Removed hardcoded fake recent activities. Will show empty state until real API is available.
  const recentActivities: { action: string; time: string; icon: typeof UserPlus }[] = [];

  return (
    <div className="fixed inset-y-0 right-0 w-full sm:w-[420px] bg-white border-l border-slate-200 shadow-2xl z-50 overflow-y-auto">
      {/* Header */}
      <div className="sticky top-0 bg-white border-b border-slate-200 p-4 z-10">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900">Member Details</h3>
          <Button variant="ghost" size="sm" onClick={onClose}>
            <X className="w-4 h-4" />
          </Button>
        </div>
      </div>

      <div className="p-6 space-y-6">
        {/* Profile Card */}
        <div className="text-center">
          <div className={`w-20 h-20 mx-auto rounded-2xl bg-gradient-to-br ${roleInfo.bgGradient} flex items-center justify-center text-white font-bold text-2xl shadow-xl`}>
            {member.name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase()}
          </div>
          <h2 className="text-xl font-bold text-slate-900 mt-4">{member.name}</h2>
          <p className="text-slate-500">{member.email}</p>
          <div className="flex items-center justify-center gap-2 mt-3">
            <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-medium ${roleInfo.bg} ${roleInfo.color}`}>
              <RoleIcon className="w-4 h-4" />
              {roleInfo.label}
            </span>
          </div>
        </div>

        {/* Quick Actions */}
        <div className="flex gap-2">
          <Button variant="outline" className="flex-1 h-10 text-sm" asChild>
            <a href={`mailto:${member.email}`}>
              <Mail className="w-4 h-4 mr-2" />
              Email
            </a>
          </Button>
          <Button 
            variant="outline" 
            className="flex-1 h-10 text-sm"
            onClick={() => {
              navigator.clipboard.writeText(member.email);
              toast.success('Email copied to clipboard');
            }}
          >
            <Copy className="w-4 h-4 mr-2" />
            Copy Email
          </Button>
        </div>

        {/* Performance Stats */}
        <div>
          <h4 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
            <BarChart3 className="w-4 h-4 text-violet-500" />
            Performance Overview
          </h4>
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-slate-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-slate-500 text-xs mb-1">
                <Target className="w-3.5 h-3.5" />
                Leads Assigned
              </div>
              <p className="text-lg font-bold text-slate-900">{mockStats.leadsAssigned}</p>
            </div>
            <div className="bg-emerald-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-emerald-600 text-xs mb-1">
                <Handshake className="w-3.5 h-3.5" />
                Deals Won
              </div>
              <p className="text-lg font-bold text-emerald-700">{mockStats.dealsWon}</p>
            </div>
            <div className="bg-blue-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-blue-600 text-xs mb-1">
                <Check className="w-3.5 h-3.5" />
                Tasks Done
              </div>
              <p className="text-lg font-bold text-blue-700">{mockStats.tasksCompleted}</p>
            </div>
            <div className="bg-amber-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-amber-600 text-xs mb-1">
                <Activity className="w-3.5 h-3.5" />
                Activities
              </div>
              <p className="text-lg font-bold text-amber-700">{mockStats.activitiesLogged}</p>
            </div>
          </div>
        </div>

        {/* Metrics */}
        <div>
          <h4 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
            <TrendingUp className="w-4 h-4 text-emerald-500" />
            Key Metrics
          </h4>
          <div className="space-y-3">
            <div className="flex items-center justify-between p-3 bg-slate-50 rounded-xl">
              <span className="text-sm text-slate-600">Conversion Rate</span>
              <span className="text-sm font-bold text-emerald-600">{mockStats.conversionRate === '—' ? '—' : `${mockStats.conversionRate}%`}</span>
            </div>
            <div className="flex items-center justify-between p-3 bg-slate-50 rounded-xl">
              <span className="text-sm text-slate-600">Avg Response Time</span>
              <span className="text-sm font-bold text-blue-600">{mockStats.avgResponseTime}</span>
            </div>
          </div>
        </div>

        {/* Recent Activity */}
        <div>
          <h4 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
            <Clock className="w-4 h-4 text-slate-500" />
            Recent Activity
          </h4>
          <div className="space-y-2">
            {recentActivities.map((activity, i) => (
              <div key={i} className="flex items-start gap-3 p-2 rounded-lg hover:bg-slate-50 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center shrink-0">
                  <activity.icon className="w-4 h-4 text-slate-500" />
                </div>
                <div className="min-w-0">
                  <p className="text-sm text-slate-700">{activity.action}</p>
                  <p className="text-xs text-slate-400">{activity.time}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Admin Actions */}
        {isOwner && member.role !== 0 && (
          <div className="border-t border-slate-200 pt-6 space-y-4">
            <h4 className="text-sm font-semibold text-slate-700">Admin Actions</h4>
            
            <div>
              <Label className="text-sm text-slate-600 mb-2 block">Change Role</Label>
              <Select
                value={member.role.toString()}
                onValueChange={(v) => onRoleChange(parseInt(v))}
              >
                <SelectTrigger className="h-10">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">
                    <span className="flex items-center gap-2">
                      <UserCircle className="w-4 h-4 text-slate-500" />
                      Member
                    </span>
                  </SelectItem>
                  <SelectItem value="2">
                    <span className="flex items-center gap-2">
                      <ShieldCheck className="w-4 h-4 text-blue-500" />
                      Manager
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <Button 
              variant="outline" 
              className="w-full text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700"
              onClick={onRemove}
            >
              <Trash2 className="w-4 h-4 mr-2" />
              Remove from Team
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
