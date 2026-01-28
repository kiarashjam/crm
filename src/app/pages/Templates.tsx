import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Mail, MessageSquare, Calendar, Handshake, RotateCcw } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getTemplates } from '@/app/api';
import type { Template } from '@/app/api/types';

const categoryIcons: Record<string, typeof Mail> = {
  Sales: Mail,
  'Follow-up': MessageSquare,
  Meetings: Calendar,
  'Re-engagement': RotateCcw,
};

export default function Templates() {
  const [templates, setTemplates] = useState<Template[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    getTemplates()
      .then((list) => {
        setTemplates(list);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, []);

  const handleUseTemplate = (template: Template) => {
    navigate('/dashboard', { state: { templateId: template.id } });
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-5xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Templates</h1>
            <p className="text-slate-600">Quick-start templates for common scenarios</p>
          </div>

          {loading ? (
            <div className="py-14 flex justify-center">
              <LoadingSpinner size="md" />
            </div>
          ) : (
            <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
              {templates.map((template) => {
                const Icon = categoryIcons[template.category] ?? Handshake;
                return (
                  <article
                    key={template.id}
                    className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 hover:shadow-md transition-shadow"
                  >
                    <div className="flex items-start justify-between mb-4">
                      <div className="w-12 h-12 bg-orange-100 rounded-xl flex items-center justify-center text-orange-600">
                        <Icon className="w-6 h-6" aria-hidden />
                      </div>
                      <span className="text-xs bg-slate-100 text-slate-700 px-2.5 py-1 rounded-full font-medium">
                        {template.category}
                      </span>
                    </div>

                    <h2 className="font-semibold text-lg text-slate-900 mb-2">{template.title}</h2>
                    <p className="text-sm text-slate-600 mb-4">{template.description}</p>

                    <div className="flex items-center justify-between">
                      <p className="text-xs text-slate-500">Used {template.useCount} times</p>
                      <button
                        type="button"
                        onClick={() => handleUseTemplate(template)}
                        className="text-orange-600 hover:text-orange-700 font-medium text-sm transition-colors focus-visible:rounded focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                      >
                        Use template →
                      </button>
                    </div>
                  </article>
                );
              })}
            </div>
          )}

          <div className="mt-12 bg-sky-50 border border-sky-200 rounded-2xl p-6">
            <h2 className="font-semibold text-sky-900 mb-2">How Templates Work</h2>
            <p className="text-sm text-sky-800 mb-4">
              Templates pre-fill the copy generator with common settings and goals. You can still customize everything before generating.
            </p>
            <Link to="/help" className="text-sky-600 hover:text-sky-700 font-medium text-sm focus-visible:rounded focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:ring-offset-2">
              Learn more about templates →
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
