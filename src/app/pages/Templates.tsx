import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Mail, MessageSquare, Calendar, Handshake, RotateCcw, Plus, Pencil, Trash2, X, Users, Sparkles, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
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
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';

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
  usePageLoading(loading);
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

  // The library's numbers, for the header.
  const sharedCount = templates.filter((t) => t.isSharedWithOrganization).length;
  const systemCount = templates.filter((t) => t.isSystemTemplate).length;
  const categoryCount = new Set(templates.map((t) => t.category)).size;
  const totalUses = templates.reduce((sum, t) => sum + (t.useCount ?? 0), 0);

  // How this person likes templates shown, saved for them. Cards, as they always were, until they choose.
  const view = useViewPreference('templates', { layouts: ['grid', 'list', 'table'] });

  const templateIcon = (t: ExtendedTemplate, size: 'sm' | 'md' = 'md') => {
    const Icon = categoryIcons[t.category] ?? Handshake;
    return (
      <span className={`flex shrink-0 items-center justify-center rounded-xl bg-teal-100 text-teal-700 ${size === 'sm' ? 'h-7 w-7' : 'h-10 w-10'}`}>
        <Icon className={size === 'sm' ? 'h-3.5 w-3.5' : 'h-5 w-5'} aria-hidden />
      </span>
    );
  };

  const sharing = (t: ExtendedTemplate) => (
    <span className="flex flex-wrap gap-1">
      {t.isSharedWithOrganization && (
        <span className="text-xs bg-violet-100 text-violet-700 px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1">
          <Users className="w-3 h-3" />
          Team
        </span>
      )}
      {t.isSystemTemplate && (
        <span className="text-xs bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full font-medium">System</span>
      )}
      {!t.isSharedWithOrganization && !t.isSystemTemplate && <span className="text-xs text-slate-500">Just you</span>}
    </span>
  );

  /** Use, edit and delete, the same on a row and a table line as on the card. */
  const templateActions = (t: ExtendedTemplate) => (
    <div className="flex items-center justify-end gap-1">
      {!t.isSystemTemplate && (
        <WriteOnly>
          <button
            type="button"
            onClick={() => openEditModal(t)}
            className="p-1.5 text-slate-500 hover:text-orange-600 hover:bg-orange-50 rounded-lg transition-colors"
            aria-label={`Edit template ${t.title}`}
          >
            <Pencil className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={() => handleDelete(t)}
            disabled={deleting === t.id}
            className="p-1.5 text-slate-500 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors disabled:opacity-50"
            aria-label={`Delete template ${t.title}`}
          >
            {deleting === t.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
          </button>
        </WriteOnly>
      )}
      <button
        type="button"
        onClick={() => handleUseTemplate(t)}
        className="ml-1 whitespace-nowrap rounded-lg px-2 py-1 text-sm font-medium text-orange-700 transition-colors hover:bg-orange-50 hover:text-orange-800 focus-visible:ring-2 focus-visible:ring-orange-500"
      >
        Use →
      </button>
    </div>
  );

  const templateColumns: RecordColumn<ExtendedTemplate>[] = [
    {
      id: 'title',
      label: 'Template',
      required: true,
      cell: (t) => (
        <span className="flex items-center gap-2.5">
          {templateIcon(t, 'sm')}
          <span className="truncate">{t.title}</span>
        </span>
      ),
    },
    { id: 'category', label: 'Category', cell: (t) => t.category },
    { id: 'sharing', label: 'Shared', cell: sharing },
    { id: 'uses', label: 'Used', numeric: true, cell: (t) => t.useCount ?? 0 },
    { id: 'type', label: 'Type', byDefault: false, cell: (t) => COPY_TYPES.find((c) => c.id === t.copyTypeId)?.label ?? t.copyTypeId },
    { id: 'goal', label: 'Goal', byDefault: false, cell: (t) => t.goal || '—' },
    { id: 'description', label: 'Description', byDefault: false, cell: (t) => <span className="line-clamp-1">{t.description || '—'}</span> },
  ];

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <div className="w-full">
          <PageHero
            icon={Mail}
            tone="fuchsia"
            title="Templates"
            subtitle="Quick-start templates for common scenarios"
            actions={
              <WriteOnly>
                <button
                  type="button"
                  onClick={openCreateModal}
                  className={`inline-flex items-center text-sm transition-opacity ${heroPrimary('fuchsia')}`}
                >
                  <Plus className="h-4 w-4" />
                  Create Template
                </button>
              </WriteOnly>
            }
            stats={loading ? undefined : [
              { label: 'Templates', value: templates.length, icon: Mail, tone: 'fuchsia', hint: `${categoryCount} ${categoryCount === 1 ? 'category' : 'categories'}` },
              { label: 'Shared with team', value: sharedCount, icon: Users, tone: 'violet', hint: 'whole team' },
              { label: 'Custom', value: templates.length - systemCount, icon: Sparkles, tone: 'teal', hint: `${systemCount} system` },
              { label: 'Times used', value: totalUses, icon: Handshake, tone: 'orange', hint: 'all templates' },
            ]}
          />

          {loading ? (
            <ContentSkeleton rows={6} />
          ) : (
            <>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-slate-500">
                {templates.length} {templates.length === 1 ? 'template' : 'templates'}
              </p>
              <ViewSwitcher view={view} columns={columnChoices(templateColumns)} />
            </div>
            <ViewTransition layout={view.layout}>
            {view.layout === 'list' ? (
              <RecordList
                label="Templates"
                items={templates}
                view={view}
                getKey={(t) => t.id}
                leading={(t) => templateIcon(t)}
                title={(t) => t.title}
                subtitle={(t) => t.description || t.category}
                columns={templateColumns}
                actions={templateActions}
              />
            ) : view.layout === 'table' ? (
              <RecordTable
                label="Templates"
                items={templates}
                view={view}
                getKey={(t) => t.id}
                columns={templateColumns}
                actions={templateActions}
              />
            ) : (
            <div className={`grid md:grid-cols-2 lg:grid-cols-3 ${gridDensity(view, 'gap-6', 'gap-3 xl:grid-cols-4')}`}>
              {templates.map((template, index) => {
                const Icon = categoryIcons[template.category] ?? Handshake;
                const isSystemTemplate = template.isSystemTemplate;
                const isShared = template.isSharedWithOrganization;
                
                return (
                  <StaggerItem
                    as="article"
                    key={template.id}
                    index={index}
                    className={`bg-white rounded-2xl border border-slate-200 shadow-sm hover:border-fuchsia-200 relative group ${view.density === 'compact' ? 'p-4' : 'p-6'} ${liftOnHover}`}
                  >
                    {/* Edit/Delete buttons (only for non-system templates) */}
                    {!isSystemTemplate && (
                      <WriteOnly>
                        <div className="absolute top-4 right-4 opacity-0 group-hover:opacity-100 transition-opacity flex gap-1">
                          <button
                            type="button"
                            onClick={() => openEditModal(template)}
                            className="p-1.5 text-slate-500 hover:text-orange-600 hover:bg-orange-50 rounded-lg transition-colors"
                            aria-label="Edit template"
                          >
                            <Pencil className="w-4 h-4" />
                          </button>
                          <button
                            type="button"
                            onClick={() => handleDelete(template)}
                            disabled={deleting === template.id}
                            className="p-1.5 text-slate-500 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors disabled:opacity-50"
                            aria-label="Delete template"
                          >
                            {deleting === template.id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                              <Trash2 className="w-4 h-4" />
                            )}
                          </button>
                        </div>
                      </WriteOnly>
                    )}

                    <div className="flex items-start justify-between mb-4">
                      <div className="w-12 h-12 bg-teal-100 rounded-xl flex items-center justify-center text-teal-700">
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
                        className="text-orange-700 hover:text-orange-800 font-medium text-sm transition-colors focus-visible:rounded focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                      >
                        Use template →
                      </button>
                    </div>
                  </StaggerItem>
                );
              })}
            </div>
            )}
            </ViewTransition>
            </>
          )}

          <div className="mt-12 bg-teal-50 border border-teal-200 rounded-2xl p-6">
            <h2 className="font-semibold text-teal-900 mb-2">How Templates Work</h2>
            <p className="text-sm text-teal-800 mb-4">
              Templates pre-fill the Intelligent Sales Writer with common settings and goals. You can still customize everything before generating.
            </p>
            <Link to="/help" className="text-teal-700 hover:text-teal-800 font-medium text-sm focus-visible:rounded focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500 focus-visible:ring-offset-2">
              Learn more about templates →
            </Link>
          </div>
        </div>
        </main>
      </PageTransition>

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
                className="p-2 text-slate-500 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
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
                  className="w-5 h-5 rounded border-slate-300 text-orange-700 focus:ring-orange-500"
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
