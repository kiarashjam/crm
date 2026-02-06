import { Link } from 'react-router-dom';
import { LayoutTemplate, Settings } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { quickNavItems } from './config';

export function QuickNav() {
  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
      <h2 className="text-sm font-bold text-slate-900 mb-4">Quick Access</h2>
      <div className="grid grid-cols-3 gap-3">
        {quickNavItems.map((item) => (
          <Link
            key={item.to}
            to={item.to}
            className="flex flex-col items-center p-3 rounded-xl hover:bg-slate-50 transition-all group"
          >
            <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center mb-2 transition-transform group-hover:scale-110", item.color)}>
              <item.icon className="w-5 h-5 text-white" />
            </div>
            <span className="text-xs font-medium text-slate-700">{item.label}</span>
          </Link>
        ))}
      </div>
      <div className="mt-4 pt-4 border-t border-slate-100 grid grid-cols-2 gap-2">
        <Link to="/templates" className="flex items-center justify-center gap-2 py-2.5 px-3 rounded-xl bg-slate-50 hover:bg-slate-100 text-sm font-medium text-slate-700 transition-colors">
          <LayoutTemplate className="w-4 h-4" />
          Templates
        </Link>
        <Link to="/settings" className="flex items-center justify-center gap-2 py-2.5 px-3 rounded-xl bg-slate-50 hover:bg-slate-100 text-sm font-medium text-slate-700 transition-colors">
          <Settings className="w-4 h-4" />
          Settings
        </Link>
      </div>
    </section>
  );
}

export default QuickNav;
