import { useState, useEffect } from 'react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getEmailSequences,
  createEmailSequence,
  updateEmailSequence,
  deleteEmailSequence,
  getEnrollments,
  EmailSequence,
  EmailSequenceEnrollment,
  CreateEmailSequenceRequest,
} from '../api/emailSequences';

export function EmailSequences() {
  const [sequences, setSequences] = useState<EmailSequence[]>([]);
  const [enrollments, setEnrollments] = useState<EmailSequenceEnrollment[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'sequences' | 'enrollments'>('sequences');
  const [showModal, setShowModal] = useState(false);
  const [editingSequence, setEditingSequence] = useState<EmailSequence | null>(null);
  const [formData, setFormData] = useState<CreateEmailSequenceRequest>({
    name: '',
    description: '',
    steps: [],
  });

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    try {
      const [seqData, enrollData] = await Promise.all([
        getEmailSequences(),
        getEnrollments(),
      ]);
      setSequences(seqData);
      setEnrollments(enrollData);
    } catch (error) {
      console.error('Failed to load data:', error);
    } finally {
      setLoading(false);
    }
  }

  function openCreateModal() {
    setEditingSequence(null);
    setFormData({ name: '', description: '', steps: [] });
    setShowModal(true);
  }

  function openEditModal(sequence: EmailSequence) {
    setEditingSequence(sequence);
    setFormData({
      name: sequence.name,
      description: sequence.description,
      steps: sequence.steps.map(s => ({
        order: s.order,
        delayDays: s.delayDays,
        copyTypeId: s.copyTypeId,
        subject: s.subject,
        body: s.body,
      })),
    });
    setShowModal(true);
  }

  async function handleSave() {
    try {
      if (editingSequence) {
        await updateEmailSequence(editingSequence.id, {
          name: formData.name,
          description: formData.description,
        });
      } else {
        await createEmailSequence(formData);
      }
      setShowModal(false);
      loadData();
    } catch (error) {
      console.error('Failed to save:', error);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Are you sure you want to delete this sequence?')) return;
    try {
      await deleteEmailSequence(id);
      loadData();
    } catch (error) {
      console.error('Failed to delete:', error);
    }
  }

  async function toggleActive(sequence: EmailSequence) {
    try {
      await updateEmailSequence(sequence.id, { isActive: !sequence.isActive });
      loadData();
    } catch (error) {
      console.error('Failed to toggle:', error);
    }
  }

  function addStep() {
    setFormData(prev => ({
      ...prev,
      steps: [
        ...prev.steps,
        {
          order: prev.steps.length + 1,
          delayDays: prev.steps.length === 0 ? 0 : 3,
          copyTypeId: 'follow-up',
          subject: '',
          body: '',
        },
      ],
    }));
  }

  function updateStep(index: number, field: string, value: string | number) {
    setFormData(prev => ({
      ...prev,
      steps: prev.steps.map((step, i) =>
        i === index ? { ...step, [field]: value } : step
      ),
    }));
  }

  function removeStep(index: number) {
    setFormData(prev => ({
      ...prev,
      steps: prev.steps.filter((_, i) => i !== index).map((s, i) => ({ ...s, order: i + 1 })),
    }));
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-orange-600"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
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
                <svg className="w-8 h-8 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Email Sequences</h1>
                <p className="text-slate-400 mt-1">
                  Create automated multi-step email campaigns
                </p>
              </div>
            </div>
            
            <div className="flex items-center gap-3">
              <button
                onClick={openCreateModal}
                className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white transition-colors"
              >
                Create Sequence
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="-mb-px flex space-x-8">
          <button
            onClick={() => setActiveTab('sequences')}
            className={`py-2 px-1 border-b-2 font-medium text-sm ${
              activeTab === 'sequences'
                ? 'border-orange-500 text-orange-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            Sequences ({sequences.length})
          </button>
          <button
            onClick={() => setActiveTab('enrollments')}
            className={`py-2 px-1 border-b-2 font-medium text-sm ${
              activeTab === 'enrollments'
                ? 'border-orange-500 text-orange-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            Enrollments ({enrollments.length})
          </button>
        </nav>
      </div>

      {activeTab === 'sequences' && (
        <div className="bg-white shadow overflow-hidden sm:rounded-lg">
          {sequences.length === 0 ? (
            <div className="text-center py-12">
              <svg className="mx-auto h-12 w-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
              </svg>
              <h3 className="mt-2 text-sm font-medium text-gray-900">No sequences</h3>
              <p className="mt-1 text-sm text-gray-500">Create your first email sequence to get started.</p>
            </div>
          ) : (
            <ul className="divide-y divide-gray-200">
              {sequences.map((sequence) => (
                <li key={sequence.id} className="px-6 py-4 hover:bg-gray-50">
                  <div className="flex items-center justify-between">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center space-x-3">
                        <h3 className="text-sm font-medium text-gray-900 truncate">
                          {sequence.name}
                        </h3>
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            sequence.isActive
                              ? 'bg-green-100 text-green-800'
                              : 'bg-gray-100 text-gray-800'
                          }`}
                        >
                          {sequence.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </div>
                      <p className="mt-1 text-sm text-gray-500">{sequence.description}</p>
                      <div className="mt-2 flex items-center space-x-4 text-xs text-gray-500">
                        <span>{sequence.steps.length} steps</span>
                        <span>{sequence.totalEnrollments} enrollments</span>
                      </div>
                    </div>
                    <div className="flex items-center space-x-2">
                      <button
                        onClick={() => toggleActive(sequence)}
                        className={`px-3 py-1 text-xs rounded ${
                          sequence.isActive
                            ? 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                            : 'bg-green-100 text-green-700 hover:bg-green-200'
                        }`}
                      >
                        {sequence.isActive ? 'Pause' : 'Activate'}
                      </button>
                      <button
                        onClick={() => openEditModal(sequence)}
                        className="px-3 py-1 text-xs bg-orange-100 text-orange-700 rounded hover:bg-orange-200"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => handleDelete(sequence.id)}
                        className="px-3 py-1 text-xs bg-red-100 text-red-700 rounded hover:bg-red-200"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {activeTab === 'enrollments' && (
        <div className="bg-white shadow overflow-hidden sm:rounded-lg">
          {enrollments.length === 0 ? (
            <div className="text-center py-12">
              <svg className="mx-auto h-12 w-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
              <h3 className="mt-2 text-sm font-medium text-gray-900">No enrollments</h3>
              <p className="mt-1 text-sm text-gray-500">Contacts will appear here when enrolled in sequences.</p>
            </div>
          ) : (
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Sequence
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Progress
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Next Send
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {enrollments.map((enrollment) => (
                  <tr key={enrollment.id}>
                    <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                      {enrollment.sequenceName}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          enrollment.status === 'active'
                            ? 'bg-green-100 text-green-800'
                            : enrollment.status === 'completed'
                            ? 'bg-blue-100 text-blue-800'
                            : enrollment.status === 'paused'
                            ? 'bg-yellow-100 text-yellow-800'
                            : 'bg-gray-100 text-gray-800'
                        }`}
                      >
                        {enrollment.status}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                      Step {enrollment.currentStep}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                      {enrollment.nextSendAt
                        ? new Date(enrollment.nextSendAt).toLocaleDateString()
                        : '-'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 overflow-y-auto">
          <div className="flex items-center justify-center min-h-screen px-4">
            <div className="fixed inset-0 bg-gray-500 bg-opacity-75" onClick={() => setShowModal(false)} />
            <div className="relative bg-white rounded-lg shadow-xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
              <div className="px-6 py-4 border-b border-gray-200">
                <h3 className="text-lg font-medium text-gray-900">
                  {editingSequence ? 'Edit Sequence' : 'Create Sequence'}
                </h3>
              </div>
              <div className="px-6 py-4 space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700">Name</label>
                  <input
                    type="text"
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                    placeholder="Welcome Series"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700">Description</label>
                  <textarea
                    value={formData.description}
                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                    rows={2}
                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                    placeholder="Describe this sequence..."
                  />
                </div>
                
                {/* Steps */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-sm font-medium text-gray-700">Steps</label>
                    <button
                      type="button"
                      onClick={addStep}
                      className="text-sm text-orange-600 hover:text-orange-500"
                    >
                      + Add Step
                    </button>
                  </div>
                  <div className="space-y-3">
                    {formData.steps.map((step, index) => (
                      <div key={index} className="border border-gray-200 rounded-lg p-3">
                        <div className="flex items-center justify-between mb-2">
                          <span className="text-sm font-medium text-gray-700">
                            Step {index + 1}
                          </span>
                          <button
                            type="button"
                            onClick={() => removeStep(index)}
                            className="text-red-500 hover:text-red-700 text-sm"
                          >
                            Remove
                          </button>
                        </div>
                        <div className="grid grid-cols-2 gap-2 mb-2">
                          <div>
                            <label className="block text-xs text-gray-500">Delay (days)</label>
                            <input
                              type="number"
                              min="0"
                              value={step.delayDays}
                              onChange={(e) => updateStep(index, 'delayDays', parseInt(e.target.value) || 0)}
                              className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                            />
                          </div>
                          <div>
                            <label className="block text-xs text-gray-500">Copy Type</label>
                            <select
                              value={step.copyTypeId}
                              onChange={(e) => updateStep(index, 'copyTypeId', e.target.value)}
                              className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                            >
                              <option value="cold-email">Cold Email</option>
                              <option value="follow-up">Follow-up</option>
                              <option value="intro-meeting">Meeting Request</option>
                            </select>
                          </div>
                        </div>
                        <div className="mb-2">
                          <label className="block text-xs text-gray-500">Subject</label>
                          <input
                            type="text"
                            value={step.subject}
                            onChange={(e) => updateStep(index, 'subject', e.target.value)}
                            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                            placeholder="Email subject..."
                          />
                        </div>
                        <div>
                          <label className="block text-xs text-gray-500">Body</label>
                          <textarea
                            value={step.body}
                            onChange={(e) => updateStep(index, 'body', e.target.value)}
                            rows={2}
                            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                            placeholder="Email body..."
                          />
                        </div>
                      </div>
                    ))}
                    {formData.steps.length === 0 && (
                      <p className="text-sm text-gray-500 text-center py-4">
                        No steps yet. Click "Add Step" to create your first email.
                      </p>
                    )}
                  </div>
                </div>
              </div>
              <div className="px-6 py-4 border-t border-gray-200 flex justify-end space-x-3">
                <button
                  onClick={() => setShowModal(false)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  onClick={handleSave}
                  disabled={!formData.name}
                  className="px-4 py-2 text-sm font-medium text-white bg-orange-600 border border-transparent rounded-xl hover:bg-orange-500 disabled:opacity-50"
                >
                  {editingSequence ? 'Save Changes' : 'Create Sequence'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
      </main>
    </div>
  );
}
