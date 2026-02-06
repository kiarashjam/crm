import { useState, useEffect } from 'react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getABTests,
  createABTest,
  updateABTest,
  deleteABTest,
  ABTest,
  CreateABTestRequest,
  getTestStatusColor,
  calculateWinner,
} from '../api/abTests';

export function ABTests() {
  const [tests, setTests] = useState<ABTest[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [selectedTest, setSelectedTest] = useState<ABTest | null>(null);
  const [formData, setFormData] = useState<CreateABTestRequest>({
    name: '',
    description: '',
    copyTypeId: 'cold-email',
    variants: [
      { name: 'Variant A', subject: '', body: '' },
      { name: 'Variant B', subject: '', body: '' },
    ],
  });

  useEffect(() => {
    loadTests();
  }, []);

  async function loadTests() {
    setLoading(true);
    try {
      const data = await getABTests();
      setTests(data);
    } catch (error) {
      console.error('Failed to load tests:', error);
    } finally {
      setLoading(false);
    }
  }

  function openCreateModal() {
    setFormData({
      name: '',
      description: '',
      copyTypeId: 'cold-email',
      variants: [
        { name: 'Variant A', subject: '', body: '' },
        { name: 'Variant B', subject: '', body: '' },
      ],
    });
    setShowModal(true);
  }

  async function handleCreate() {
    try {
      await createABTest(formData);
      setShowModal(false);
      loadTests();
    } catch (error) {
      console.error('Failed to create test:', error);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Are you sure you want to delete this A/B test?')) return;
    try {
      await deleteABTest(id);
      loadTests();
    } catch (error) {
      console.error('Failed to delete:', error);
    }
  }

  async function handleStatusChange(test: ABTest, status: ABTest['status']) {
    try {
      await updateABTest(test.id, { status });
      loadTests();
    } catch (error) {
      console.error('Failed to update status:', error);
    }
  }

  async function declareWinner(test: ABTest, winnerId: string) {
    try {
      await updateABTest(test.id, { winnerId, status: 'completed' });
      loadTests();
    } catch (error) {
      console.error('Failed to declare winner:', error);
    }
  }

  function addVariant() {
    setFormData(prev => ({
      ...prev,
      variants: [
        ...prev.variants,
        { name: `Variant ${String.fromCharCode(65 + prev.variants.length)}`, subject: '', body: '' },
      ],
    }));
  }

  function updateVariant(index: number, field: string, value: string) {
    setFormData(prev => ({
      ...prev,
      variants: prev.variants.map((v, i) =>
        i === index ? { ...v, [field]: value } : v
      ),
    }));
  }

  function removeVariant(index: number) {
    if (formData.variants.length <= 2) return;
    setFormData(prev => ({
      ...prev,
      variants: prev.variants.filter((_, i) => i !== index),
    }));
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
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
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
                </svg>
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">A/B Tests</h1>
                <p className="text-slate-400 mt-1">
                  Compare different copy variants to find what works best
                </p>
              </div>
            </div>
            
            <div className="flex items-center gap-3">
              <button
                onClick={openCreateModal}
                className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white transition-colors"
              >
                Create A/B Test
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Tests Grid */}
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        {tests.map((test) => {
          const winner = test.winnerId 
            ? test.variants.find(v => v.id === test.winnerId)
            : calculateWinner(test);
          
          return (
            <div
              key={test.id}
              className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden"
            >
              <div className="px-4 py-4 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <h3
                    className="text-lg font-medium text-gray-900 truncate cursor-pointer hover:text-orange-600"
                    onClick={() => setSelectedTest(test)}
                  >
                    {test.name}
                  </h3>
                  <span className={`px-2 py-1 text-xs rounded-full ${getTestStatusColor(test.status)}`}>
                    {test.status}
                  </span>
                </div>
                <p className="mt-1 text-sm text-slate-500 truncate">{test.description}</p>
              </div>

              {/* Variants Summary */}
              <div className="px-4 py-3 space-y-2">
                {test.variants.map((variant) => (
                  <div
                    key={variant.id}
                    className={`flex items-center justify-between p-2 rounded ${
                      test.winnerId === variant.id || (!test.winnerId && winner?.id === variant.id)
                        ? 'bg-green-50 border border-green-200'
                        : 'bg-gray-50'
                    }`}
                  >
                    <div className="flex-1 min-w-0">
                      <span className="text-sm font-medium text-gray-900">{variant.name}</span>
                      {(test.winnerId === variant.id) && (
                        <span className="ml-2 text-xs text-green-600">Winner</span>
                      )}
                    </div>
                    <div className="text-right text-xs text-slate-500">
                      <div>{variant.impressions} views</div>
                      <div className="font-medium text-gray-900">
                        {variant.conversionRate.toFixed(1)}% conv
                      </div>
                    </div>
                  </div>
                ))}
              </div>

              {/* Actions */}
              <div className="px-4 py-3 bg-gray-50 border-t border-gray-200 flex items-center justify-between">
                <div className="flex space-x-2">
                  {test.status === 'draft' && (
                    <button
                      onClick={() => handleStatusChange(test, 'running')}
                      className="px-2 py-1 text-xs bg-green-100 text-green-700 rounded hover:bg-green-200"
                    >
                      Start
                    </button>
                  )}
                  {test.status === 'running' && (
                    <>
                      <button
                        onClick={() => handleStatusChange(test, 'paused')}
                        className="px-2 py-1 text-xs bg-yellow-100 text-yellow-700 rounded hover:bg-yellow-200"
                      >
                        Pause
                      </button>
                      {winner && (
                        <button
                          onClick={() => declareWinner(test, winner.id)}
                          className="px-2 py-1 text-xs bg-blue-100 text-blue-700 rounded hover:bg-blue-200"
                        >
                          End & Pick Winner
                        </button>
                      )}
                    </>
                  )}
                  {test.status === 'paused' && (
                    <button
                      onClick={() => handleStatusChange(test, 'running')}
                      className="px-2 py-1 text-xs bg-green-100 text-green-700 rounded hover:bg-green-200"
                    >
                      Resume
                    </button>
                  )}
                </div>
                <button
                  onClick={() => handleDelete(test.id)}
                  className="px-2 py-1 text-xs text-red-600 hover:text-red-800"
                >
                  Delete
                </button>
              </div>
            </div>
          );
        })}

        {tests.length === 0 && (
          <div className="col-span-full text-center py-12 bg-white rounded-lg border border-gray-200">
            <svg className="mx-auto h-12 w-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            <h3 className="mt-2 text-sm font-medium text-gray-900">No A/B tests</h3>
            <p className="mt-1 text-sm text-slate-500">Create your first A/B test to compare copy variants.</p>
          </div>
        )}
      </div>

      {/* Detail Modal */}
      {selectedTest && (
        <div className="fixed inset-0 z-50 overflow-y-auto">
          <div className="flex items-center justify-center min-h-screen px-4">
            <div className="fixed inset-0 bg-gray-500 bg-opacity-75" onClick={() => setSelectedTest(null)} />
            <div className="relative bg-white rounded-lg shadow-xl max-w-4xl w-full max-h-[90vh] overflow-y-auto">
              <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-medium text-gray-900">{selectedTest.name}</h3>
                  <p className="text-sm text-slate-500">{selectedTest.description}</p>
                </div>
                <button onClick={() => setSelectedTest(null)} className="text-gray-400 hover:text-slate-500">
                  <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
              <div className="px-6 py-4">
                <h4 className="text-sm font-medium text-gray-900 mb-4">Variant Performance</h4>
                <div className="space-y-4">
                  {selectedTest.variants.map((variant) => (
                    <div key={variant.id} className="border border-gray-200 rounded-lg p-4">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center space-x-2">
                          <span className="font-medium text-gray-900">{variant.name}</span>
                          {selectedTest.winnerId === variant.id && (
                            <span className="px-2 py-0.5 text-xs bg-green-100 text-green-800 rounded">Winner</span>
                          )}
                        </div>
                      </div>
                      <div className="grid grid-cols-4 gap-4 mb-3">
                        <div className="text-center p-2 bg-gray-50 rounded">
                          <div className="text-lg font-semibold text-gray-900">{variant.impressions}</div>
                          <div className="text-xs text-slate-500">Impressions</div>
                        </div>
                        <div className="text-center p-2 bg-gray-50 rounded">
                          <div className="text-lg font-semibold text-gray-900">{variant.clicks}</div>
                          <div className="text-xs text-slate-500">Clicks</div>
                        </div>
                        <div className="text-center p-2 bg-gray-50 rounded">
                          <div className="text-lg font-semibold text-gray-900">{variant.clickRate.toFixed(1)}%</div>
                          <div className="text-xs text-slate-500">Click Rate</div>
                        </div>
                        <div className="text-center p-2 bg-blue-50 rounded">
                          <div className="text-lg font-semibold text-blue-900">{variant.conversionRate.toFixed(1)}%</div>
                          <div className="text-xs text-blue-600">Conv. Rate</div>
                        </div>
                      </div>
                      <div className="bg-gray-50 rounded p-3">
                        <div className="text-xs text-slate-500 mb-1">Subject:</div>
                        <div className="text-sm text-gray-900 mb-2">{variant.subject}</div>
                        <div className="text-xs text-slate-500 mb-1">Body:</div>
                        <div className="text-sm text-gray-700 whitespace-pre-wrap">{variant.body}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Create Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 overflow-y-auto">
          <div className="flex items-center justify-center min-h-screen px-4">
            <div className="fixed inset-0 bg-gray-500 bg-opacity-75" onClick={() => setShowModal(false)} />
            <div className="relative bg-white rounded-lg shadow-xl max-w-3xl w-full max-h-[90vh] overflow-y-auto">
              <div className="px-6 py-4 border-b border-gray-200">
                <h3 className="text-lg font-medium text-gray-900">Create A/B Test</h3>
              </div>
              <div className="px-6 py-4 space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700">Name</label>
                    <input
                      type="text"
                      value={formData.name}
                      onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                      className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                      placeholder="Subject Line Test"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700">Copy Type</label>
                    <select
                      value={formData.copyTypeId}
                      onChange={(e) => setFormData({ ...formData, copyTypeId: e.target.value })}
                      className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                    >
                      <option value="cold-email">Cold Email</option>
                      <option value="follow-up">Follow-up</option>
                      <option value="intro-meeting">Meeting Request</option>
                      <option value="linkedin-message">LinkedIn Message</option>
                    </select>
                  </div>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700">Description</label>
                  <input
                    type="text"
                    value={formData.description}
                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                    placeholder="Testing different approaches..."
                  />
                </div>

                {/* Variants */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-sm font-medium text-gray-700">Variants</label>
                    <button
                      type="button"
                      onClick={addVariant}
                      className="text-sm text-orange-600 hover:text-orange-500"
                    >
                      + Add Variant
                    </button>
                  </div>
                  <div className="space-y-3">
                    {formData.variants.map((variant, index) => (
                      <div key={index} className="border border-gray-200 rounded-lg p-3">
                        <div className="flex items-center justify-between mb-2">
                          <input
                            type="text"
                            value={variant.name}
                            onChange={(e) => updateVariant(index, 'name', e.target.value)}
                            className="text-sm font-medium bg-transparent border-none focus:ring-0 p-0"
                          />
                          {formData.variants.length > 2 && (
                            <button
                              type="button"
                              onClick={() => removeVariant(index)}
                              className="text-red-500 hover:text-red-700 text-sm"
                            >
                              Remove
                            </button>
                          )}
                        </div>
                        <div className="space-y-2">
                          <input
                            type="text"
                            value={variant.subject}
                            onChange={(e) => updateVariant(index, 'subject', e.target.value)}
                            className="block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                            placeholder="Subject line..."
                          />
                          <textarea
                            value={variant.body}
                            onChange={(e) => updateVariant(index, 'body', e.target.value)}
                            rows={3}
                            className="block w-full rounded-md border-gray-300 shadow-sm focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
                            placeholder="Email body..."
                          />
                        </div>
                      </div>
                    ))}
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
                  onClick={handleCreate}
                  disabled={!formData.name || formData.variants.some(v => !v.subject || !v.body)}
                  className="px-4 py-2 text-sm font-medium text-white bg-orange-600 border border-transparent rounded-md hover:bg-orange-500 disabled:opacity-50"
                >
                  Create Test
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
