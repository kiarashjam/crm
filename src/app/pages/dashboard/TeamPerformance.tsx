import { Users } from 'lucide-react';
import type { PipelineValueByAssignee } from '@/app/api/reporting';

interface TeamPerformanceProps {
  members: PipelineValueByAssignee[];
}

export function TeamPerformance({ members }: TeamPerformanceProps) {
  if (members.length === 0) return null;

  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-100 flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-500 flex items-center justify-center">
          <Users className="w-5 h-5 text-white" />
        </div>
        <div>
          <h2 className="text-base font-bold text-slate-900">Team Performance</h2>
          <p className="text-xs text-slate-500">Pipeline value by team member</p>
        </div>
      </div>
      <div className="p-6">
        <div className="space-y-3">
          {members.map((member) => (
            <div key={member.assigneeUserId || 'unassigned'} className="flex items-center gap-4 p-3 bg-slate-50 rounded-xl hover:bg-slate-100 transition-colors">
              <div className="w-10 h-10 rounded-full bg-gradient-to-br from-slate-600 to-slate-700 flex items-center justify-center text-white font-semibold text-sm">
                {member.assigneeName.charAt(0).toUpperCase()}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-slate-900 truncate">{member.assigneeName}</p>
                <p className="text-xs text-slate-500">{member.dealCount} deals</p>
              </div>
              <p className="text-sm font-bold text-emerald-600">
                ${member.value.toLocaleString()}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export default TeamPerformance;
