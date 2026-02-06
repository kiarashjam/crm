import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Mail, MessageSquare, Calendar, Handshake, RotateCcw, Plus, Pencil, Trash2, X, Users, Sparkles, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { 
  getTemplates, 
  createTemplate, 
  updateTemplate, 
  deleteTemplate,
  type ExtendedTemplate,
  type CreateTemplateRequest,
  type UpdateTemplateRequest 
} from '@/app/api/templates';
import type { Template, CopyTypeId } from '@/app/api/types';

const categoryIcons: Record<string, typeof Mail> = {
  Sales: Mail,
  'Follow-up': MessageSquare,
  Meetings: Calendar,
  'Re-engagement': RotateCcw,
  Custom: Sparkles,
};

const COPY_TYPES: { id: CopyTypeId; label: string }[] = [
  { id: 'sales-email', label: 'Sales Email' },
  { id: 'follow-up', label: 'Follow-up' },
  { id: 'crm-note', label: 'CRM Note' },
  { id: 'deal-message', label: 'Deal Message' },
  { id: 'workflow-message', label: 'Workflow Message' },
];

const GOALS = [
  'Schedule a meeting',
  'Follow up after demo',
  'Request feedback',
  'Share resources',
  'Check in on progress',
  'Close the deal',
];

const CATEGORIES = ['Sales', 'Follow-up', 'Meetings', 'Re-engagement', 'Custom'];

interface TemplateFormData {
  title: string;
  description: string;
  category: string;
  copyTypeId: CopyTypeId;
  goal: string;
  content: string;
  brandTone: string;
  length: string;
  isSharedWithOrganization: boolean;
}

const emptyFormData: TemplateFormData = {
  title: '',
  description: '',
  category: 'Custom',
  copyTypeId: 'sales-email',
  goal: 'Schedule a meeting',
  content: '',
  brandTone: 'professional',
  length: 'medium',
  isSharedWithOrganization: false,
};

export default function Templates() {
  const [templates, setTemplates] = useState<ExtendedTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<ExtendedTemplate | null>(null);
  const [formData, setFormData] = useState<TemplateFormData>(emptyFormData);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const navigate = useNavigate();

  const loadTemplates = async () => {
    try {
      const list = await getTemplates();
      setTemplates(list);
    } catch {
      toast.error('Failed to load templates');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTemplates();
  }, []);

  const handleUseTemplate = (template: Template) => {
    navigate('/dashboard', { state: { templateId: template.id } });
  };

  const openCreateModal = () => {
    setEditingTemplate(null);
    setFormData(emptyFormData);
    setShowModal(true);
  };

  const openEditModal = (template: ExtendedTemplate) => {
    if (template.isSystemTemplate) {
      toast.error('System templates cannot be edited');
      return;
    }
    setEditingTemplate(template);
    setFormData({
      title: template.title,
      description: template.description,
      category: template.category,
      copyTypeId: template.copyTypeId,
      goal: template.goal,
      content: template.content ?? '',
      brandTone: template.brandTone ?? 'professional',
      length: template.length ?? 'medium',
      isSharedWithOrganization: template.isSharedWithOrganization,
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!formData.title.trim()) {
      toast.error('Title is required');
      return;
    }

    setSaving(true);
    try {
      if (editingTemplate) {
        const request: UpdateTemplateRequest = {
          title: formData.title,
          description: formData.description,
          category: formData.category,
          copyTypeId: formData.copyTypeId,
          goal: formData.goal,
          content: formData.content || undefined,
          brandTone: formData.brandTone,
          length: formData.length,
          isSharedWithOrganization: formData.isSharedWithOrganization,
        };
        await updateTemplate(editingTemplate.id, request);
        toast.success('Template updated');
      } else {
        const request: CreateTemplateRequest = {
          title: formData.title,
          description: formData.description,
          category: formData.category,
          copyTypeId: formData.copyTypeId,
          goal: formData.goal,
          content: formData.content || undefined,
          brandTone: formData.brandTone,
          length: formData.length,
          isSharedWithOrganization: formData.isSharedWithOrganization,
        };
        await createTemplate(request);
        toast.success('Template created');
      }
      setShowModal(false);
      await loadTemplates();
    } catch {
      toast.error('Failed to save template');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (template: ExtendedTemplate) => {
    if (template.isSystemTemplate) {
      toast.error('System templates cannot be deleted');
      return;
    }
    if (!confirm(`Delete "${template.title}"?`)) return;

    setDeleting(template.id);
    try {
      await deleteTemplate(template.id);
      toast.success('Template deleted');
      await loadTemplates();
    } catch {
      toast.error('Failed to delete template');
    } finally {
      setDeleting(null);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="w-full">
          {/* Enhanced Header Section with Dark Decorative Elements */}
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            {/* Decorative blur elements */}
            <div className="absolute inset-0 overflow-hidden">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-orange-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl" />
              <div className="absolute inset-0 opacity-5" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.343 0L13.857 8.485 15.272 9.9l9.9-9.9h-2.83zM32 0l-3.486 3.485-1.414-1.414L30.586 0H32zM0 5.373l.828-.83 1.415 1.415L0 8.2V5.374zm0 5.656l.828-.829 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 8.485 8.485-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 11.314 11.314-1.414 1.414L0 22.343v-.001zM60 5.373l-.828-.83-1.415 1.415L60 8.2V5.374zm0 5.656l-.828-.829-5.657 5.657 1.414 1.414L60 11.03v-.001zm0 5.656l-.828-.828-8.485 8.485 1.414 1.414L60 16.686v-.001zm0 5.657l-.828-.828-11.314 11.314 1.414 1.414L60 22.343v-.001z' fill='%23fff' fill-opacity='1' fill-rule='evenodd'/%3E%3C/svg%3E")` }} />
            </div>
            
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-orange-500/30">
                    <Mail className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Templates</h1>
                    <p className="text-slate-400 mt-1">
                      Quick-start templates for common scenarios
                    </p>
                  </div>
                </div>
                
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={openCreateModal}
                    className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white transition-colors"
                  >
                    <Plus className="w-4 h-4" />
                    Create Template
                  </button>
                </div>
              </div>
            </div>
          </div>

          {loading ? (
            <div className="py-14 flex justify-center">
              <LoadingSpinner size="md" />
            </div>
          ) : (
            <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
              {templates.map((template) => {
                const Icon = categoryIcons[template.category] ?? Handshake;
                const isSystemTemplate = template.isSystemTemplate;
                const isShared = template.isSharedWithOrganization;
                
                return (
                  <article
                    key={template.id}
                    className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 hover:shadow-md transition-shadow relative group"
                  >
                    {/* Edit/Delete buttons (only for non-system templates) */}
                    {!isSystemTemplate && (
                      <div className="absolute top-4 right-4 opacity-0 group-hover:opacity-100 transition-opacity flex gap-1">
                        <button
                          type="button"
                          onClick={() => openEditModal(template)}
                          className="p-1.5 text-slate-400 hover:text-orange-600 hover:bg-orange-50 rounded-lg transition-colors"
                          aria-label="Edit template"
                        >
                          <Pencil className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => handleDelete(template)}
                          disabled={deleting === template.id}
                          className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors disabled:opacity-50"
                          aria-label="Delete template"
                        >
                          {deleting === template.id ? (
                            <Loader2 className="w-4 h-4 animate-spin" />
                          ) : (
                            <Trash2 className="w-4 h-4" />
                          )}
                        </button>
                      </div>
                    )}

                    <div className="flex items-start justify-between mb-4">
                      <div className="w-12 h-12 bg-teal-100 rounded-xl flex items-center justify-center text-teal-600">
                        <Icon className="w-6 h-6" aria-hidden />
                      </div>
                      <div className="flex gap-1.5">
                        {isShared && (
                          <span className="text-xs bg-violet-100 text-violet-700 px-2 py-1 rounded-full font-medium flex items-center gap-1">
                            <Users className="w-3 h-3" />
                            Team
                          </span>
                        )}
                        {isSystemTemplate && (
                          <span className="text-xs bg-slate-100 text-slate-600 px-2 py-1 rounded-full font-medium">
                            System
                          </span>
                        )}
                        <span className="text-xs bg-slate-100 text-slate-700 px-2.5 py-1 rounded-full font-medium">
                          {template.category}
                        </span>
                      </div>
                    </div>

                    <h2 className="font-semibold text-lg text-slate-900 mb-2">{template.title}</h2>
                    <p className="text-sm text-slate-600 mb-4 line-clamp-2">{template.description}</p>

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

          <div className="mt-12 bg-teal-50 border border-teal-200 rounded-2xl p-6">
            <h2 className="font-semibold text-teal-900 mb-2">How Templates Work</h2>
            <p className="text-sm text-teal-800 mb-4">
              Templates pre-fill the Intelligent Sales Writer with common settings and goals. You can still customize everything before generating.
            </p>
            <Link to="/help" className="text-teal-600 hover:text-teal-700 font-medium text-sm focus-visible:rounded focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500 focus-visible:ring-offset-2">
              Learn more about templates →
            </Link>
          </div>
        </div>
      </main>

      {/* Create/Edit Template Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-6 border-b border-slate-200">
              <h2 className="text-xl font-bold text-slate-900">
                {editingTemplate ? 'Edit Template' : 'Create Template'}
              </h2>
              <button
                type="button"
                onClick={() => setShowModal(false)}
                className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4">
              <div>
                <label htmlFor="template-title" className="block text-sm font-medium text-slate-700 mb-1.5">
                  Title *
                </label>
                <input
                  id="template-title"
                  type="text"
                  value={formData.title}
                  onChange={(e) => setFormData({ ...formData, title: e.target.value })}
                  placeholder="e.g., Cold Outreach Email"
                  className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                />
              </div>

              <div>
                <label htmlFor="template-description" className="block text-sm font-medium text-slate-700 mb-1.5">
                  Description
                </label>
                <textarea
                  id="template-description"
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  placeholder="Brief description of when to use this template..."
                  rows={2}
                  className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none resize-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label htmlFor="template-category" className="block text-sm font-medium text-slate-700 mb-1.5">
                    Category
                  </label>
                  <select
                    id="template-category"
                    value={formData.category}
                    onChange={(e) => setFormData({ ...formData, category: e.target.value })}
                    className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                  >
                    {CATEGORIES.map((cat) => (
                      <option key={cat} value={cat}>{cat}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label htmlFor="template-copytype" className="block text-sm font-medium text-slate-700 mb-1.5">
                    Copy Type
                  </label>
                  <select
                    id="template-copytype"
                    value={formData.copyTypeId}
                    onChange={(e) => setFormData({ ...formData, copyTypeId: e.target.value as CopyTypeId })}
                    className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                  >
                    {COPY_TYPES.map((type) => (
                      <option key={type.id} value={type.id}>{type.label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label htmlFor="template-goal" className="block text-sm font-medium text-slate-700 mb-1.5">
                  Goal
                </label>
                <select
                  id="template-goal"
                  value={formData.goal}
                  onChange={(e) => setFormData({ ...formData, goal: e.target.value })}
                  className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                >
                  {GOALS.map((goal) => (
                    <option key={goal} value={goal}>{goal}</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label htmlFor="template-tone" className="block text-sm font-medium text-slate-700 mb-1.5">
                    Brand Tone
                  </label>
                  <select
                    id="template-tone"
                    value={formData.brandTone}
                    onChange={(e) => setFormData({ ...formData, brandTone: e.target.value })}
                    className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                  >
                    <option value="professional">Professional</option>
                    <option value="friendly">Friendly</option>
                    <option value="persuasive">Persuasive</option>
                  </select>
                </div>

                <div>
                  <label htmlFor="template-length" className="block text-sm font-medium text-slate-700 mb-1.5">
                    Length
                  </label>
                  <select
                    id="template-length"
                    value={formData.length}
                    onChange={(e) => setFormData({ ...formData, length: e.target.value })}
                    className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                  >
                    <option value="short">Short</option>
                    <option value="medium">Medium</option>
                    <option value="long">Long</option>
                  </select>
                </div>
              </div>

              <div>
                <label htmlFor="template-content" className="block text-sm font-medium text-slate-700 mb-1.5">
                  Template Content (Optional)
                </label>
                <textarea
                  id="template-content"
                  value={formData.content}
                  onChange={(e) => setFormData({ ...formData, content: e.target.value })}
                  placeholder="Pre-written template content that will be used as a base..."
                  rows={4}
                  className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none resize-none font-mono text-sm"
                />
              </div>

              <label className="flex items-center gap-3 p-3 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
                <input
                  type="checkbox"
                  checked={formData.isSharedWithOrganization}
                  onChange={(e) => setFormData({ ...formData, isSharedWithOrganization: e.target.checked })}
                  className="w-5 h-5 rounded border-slate-300 text-orange-600 focus:ring-orange-500"
                />
                <div>
                  <p className="text-sm font-medium text-slate-900">Share with team</p>
                  <p className="text-xs text-slate-500">Make this template available to all team members</p>
                </div>
              </label>
            </div>

            <div className="flex gap-3 p-6 border-t border-slate-200">
              <button
                type="button"
                onClick={() => setShowModal(false)}
                className="flex-1 px-4 py-3 border border-slate-300 text-slate-700 font-medium rounded-xl hover:bg-slate-50 transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleSave}
                disabled={saving}
                className="flex-1 px-4 py-3 bg-orange-600 hover:bg-orange-500 text-white font-semibold rounded-xl transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
              >
                {saving && <Loader2 className="w-4 h-4 animate-spin" />}
                {editingTemplate ? 'Update' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
