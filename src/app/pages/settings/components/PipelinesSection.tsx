import { Kanban, Plus, Pencil, Trash2, ChevronRight } from 'lucide-react';
import type { Pipeline, DealStage } from '@/app/api/types';
import { isUsingRealApi } from '@/app/api/apiClient';

export interface PipelinesSectionProps {
  pipelines: Pipeline[];
  pipelineStages: Record<string, DealStage[]>;
  expandedPipelineId: string | null;
  setExpandedPipelineId: (id: string | null) => void;
  newPipelineName: string;
  setNewPipelineName: (val: string) => void;
  handleCreatePipeline: (e: React.FormEvent) => void;
  creatingPipeline: boolean;
  editingPipelineId: string | null;
  setEditingPipelineId: (id: string | null) => void;
  editingPipelineName: string;
  setEditingPipelineName: (val: string) => void;
  handleUpdatePipeline: (id: string) => void;
  handleDeletePipeline: (id: string) => void;
  deletingPipelineId: string | null;
  newStagePipelineId: string | null;
  setNewStagePipelineId: (id: string | null) => void;
  newStageName: string;
  setNewStageName: (val: string) => void;
  handleCreateStage: (e: React.FormEvent, pipelineId: string) => void;
  creatingStage: boolean;
  editingStageId: string | null;
  setEditingStageId: (id: string | null) => void;
  editingStageName: string;
  setEditingStageName: (val: string) => void;
  handleUpdateStage: (stageId: string, pipelineId: string) => void;
  handleDeleteStage: (stageId: string, pipelineId: string) => void;
  deletingStageId: string | null;
  currentOrg: { id: string; isOwner: boolean } | null;
}

export function PipelinesSection({
  pipelines,
  pipelineStages,
  expandedPipelineId,
  setExpandedPipelineId,
  newPipelineName,
  setNewPipelineName,
  handleCreatePipeline,
  creatingPipeline,
  editingPipelineId,
  setEditingPipelineId,
  editingPipelineName,
  setEditingPipelineName,
  handleUpdatePipeline,
  handleDeletePipeline,
  deletingPipelineId,
  newStagePipelineId,
  setNewStagePipelineId,
  newStageName,
  setNewStageName,
  handleCreateStage,
  creatingStage,
  editingStageId,
  setEditingStageId,
  editingStageName,
  setEditingStageName,
  handleUpdateStage,
  handleDeleteStage,
  deletingStageId,
  currentOrg,
}: PipelinesSectionProps) {
  if (!isUsingRealApi() || !currentOrg?.isOwner) {
    return (
      <div className="p-6">
        <div>
          <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
            <Kanban className="w-5 h-5 text-orange-600" />
            Pipelines & Stages
          </h2>
          <p className="text-slate-600 text-sm mt-1">Configure your sales pipeline stages</p>
        </div>
        <div className="mt-6 p-4 bg-slate-50 rounded-xl text-center">
          <p className="text-slate-600">Pipeline management is available for organization owners.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Kanban className="w-5 h-5 text-orange-600" />
          Pipelines & Stages
        </h2>
        <p className="text-slate-600 text-sm mt-1">Configure your sales pipeline stages for deals</p>
      </div>

      <form onSubmit={handleCreatePipeline} className="flex gap-2">
        <input
          type="text"
          value={newPipelineName}
          onChange={(e) => setNewPipelineName(e.target.value)}
          placeholder="New pipeline name..."
          className="flex-1 min-w-0 h-11 px-4 border border-slate-300 rounded-xl text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
        />
        <button
          type="submit"
          disabled={!newPipelineName.trim() || creatingPipeline}
          className="h-11 px-5 bg-orange-600 hover:bg-orange-700 disabled:opacity-50 text-white font-medium rounded-xl flex items-center gap-2 transition-colors"
        >
          <Plus className="w-4 h-4" />
          {creatingPipeline ? 'Adding...' : 'Add Pipeline'}
        </button>
      </form>

      {pipelines.length === 0 ? (
        <div className="p-8 text-center bg-slate-50 rounded-xl">
          <Kanban className="w-12 h-12 mx-auto text-slate-300 mb-3" />
          <p className="text-slate-600">No pipelines yet. Create one to manage deal stages.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {pipelines.map((p) => (
            <div key={p.id} className="border border-slate-200 rounded-xl overflow-hidden">
              <div className="flex items-center justify-between gap-2 p-4 bg-slate-50">
                {editingPipelineId === p.id ? (
                  <div className="flex-1 flex gap-2 items-center">
                    <input
                      type="text"
                      value={editingPipelineName}
                      onChange={(e) => setEditingPipelineName(e.target.value)}
                      className="flex-1 min-w-0 h-10 px-3 border border-slate-300 rounded-lg text-slate-900"
                    />
                    <button
                      type="button"
                      onClick={() => handleUpdatePipeline(p.id)}
                      className="h-10 px-4 bg-emerald-600 text-white rounded-lg font-medium"
                    >
                      Save
                    </button>
                    <button
                      type="button"
                      onClick={() => { setEditingPipelineId(null); setEditingPipelineName(''); }}
                      className="h-10 px-4 border border-slate-300 rounded-lg text-slate-700"
                    >
                      Cancel
                    </button>
                  </div>
                ) : (
                  <>
                    <button
                      type="button"
                      onClick={() => setExpandedPipelineId(expandedPipelineId === p.id ? null : p.id)}
                      className="flex-1 flex items-center gap-2 text-left font-medium text-slate-900 hover:text-orange-600"
                    >
                      <ChevronRight className={`w-4 h-4 transition-transform ${expandedPipelineId === p.id ? 'rotate-90' : ''}`} />
                      {p.name}
                      <span className="text-xs text-slate-400 font-normal">
                        ({(pipelineStages[p.id] ?? p.dealStages ?? []).length} stages)
                      </span>
                    </button>
                    <div className="flex gap-1">
                      <button
                        type="button"
                        onClick={() => { setEditingPipelineId(p.id); setEditingPipelineName(p.name); }}
                        className="p-2 text-slate-500 hover:bg-slate-200 rounded-lg"
                        title="Edit"
                      >
                        <Pencil className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDeletePipeline(p.id)}
                        disabled={deletingPipelineId === p.id}
                        className="p-2 text-red-600 hover:bg-red-50 rounded-lg disabled:opacity-50"
                        title="Delete"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </>
                )}
              </div>

              {expandedPipelineId === p.id && (
                <div className="p-4 border-t border-slate-200 bg-white">
                  <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-3">Stages</p>
                  {(pipelineStages[p.id] ?? p.dealStages ?? []).length === 0 ? (
                    <p className="text-sm text-slate-500 mb-3">No stages yet.</p>
                  ) : (
                    <ul className="space-y-2 mb-4">
                      {(pipelineStages[p.id] ?? p.dealStages ?? [])
                        .slice()
                        .sort((a, b) => a.displayOrder - b.displayOrder)
                        .map((s, idx) => (
                          <li key={s.id} className="flex items-center justify-between gap-2 py-2 px-3 rounded-lg bg-slate-50">
                            {editingStageId === s.id ? (
                              <div className="flex-1 flex gap-2 items-center">
                                <input
                                  type="text"
                                  value={editingStageName}
                                  onChange={(e) => setEditingStageName(e.target.value)}
                                  className="flex-1 min-w-0 h-8 px-2 border border-slate-300 rounded text-slate-900 text-sm"
                                />
                                <button
                                  type="button"
                                  onClick={() => handleUpdateStage(s.id, p.id)}
                                  className="h-8 px-3 bg-emerald-600 text-white rounded text-sm"
                                >
                                  Save
                                </button>
                                <button
                                  type="button"
                                  onClick={() => { setEditingStageId(null); setEditingStageName(''); }}
                                  className="h-8 px-3 border border-slate-300 rounded text-slate-600 text-sm"
                                >
                                  Cancel
                                </button>
                              </div>
                            ) : (
                              <>
                                <div className="flex items-center gap-2">
                                  <span className="w-6 h-6 rounded-full bg-orange-100 text-orange-700 text-xs font-medium flex items-center justify-center">
                                    {idx + 1}
                                  </span>
                                  <span className="font-medium text-slate-800">
                                    {s.name}
                                    {s.isWon && <span className="text-emerald-600 text-xs ml-1">(Won)</span>}
                                    {s.isLost && <span className="text-red-600 text-xs ml-1">(Lost)</span>}
                                  </span>
                                </div>
                                <div className="flex gap-1">
                                  <button
                                    type="button"
                                    onClick={() => { setEditingStageId(s.id); setEditingStageName(s.name); }}
                                    className="p-1.5 text-slate-500 hover:bg-slate-200 rounded"
                                    title="Edit"
                                  >
                                    <Pencil className="w-3.5 h-3.5" />
                                  </button>
                                  <button
                                    type="button"
                                    onClick={() => handleDeleteStage(s.id, p.id)}
                                    disabled={deletingStageId === s.id}
                                    className="p-1.5 text-red-600 hover:bg-red-50 rounded disabled:opacity-50"
                                    title="Delete"
                                  >
                                    <Trash2 className="w-3.5 h-3.5" />
                                  </button>
                                </div>
                              </>
                            )}
                          </li>
                        ))}
                    </ul>
                  )}

                  {newStagePipelineId === p.id ? (
                    <form onSubmit={(e) => handleCreateStage(e, p.id)} className="flex gap-2">
                      <input
                        type="text"
                        value={newStageName}
                        onChange={(e) => setNewStageName(e.target.value)}
                        placeholder="Stage name..."
                        className="flex-1 min-w-0 h-9 px-3 border border-slate-300 rounded-lg text-slate-900 text-sm"
                      />
                      <button
                        type="submit"
                        disabled={!newStageName.trim() || creatingStage}
                        className="h-9 px-4 bg-orange-600 text-white rounded-lg text-sm font-medium disabled:opacity-50"
                      >
                        {creatingStage ? 'Adding...' : 'Add'}
                      </button>
                      <button
                        type="button"
                        onClick={() => { setNewStagePipelineId(null); setNewStageName(''); }}
                        className="h-9 px-4 border border-slate-300 rounded-lg text-slate-600 text-sm"
                      >
                        Cancel
                      </button>
                    </form>
                  ) : (
                    <button
                      type="button"
                      onClick={() => setNewStagePipelineId(p.id)}
                      className="h-9 px-4 border border-dashed border-slate-300 rounded-lg text-slate-600 text-sm font-medium hover:bg-slate-50 flex items-center gap-1"
                    >
                      <Plus className="w-4 h-4" />
                      Add Stage
                    </button>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
