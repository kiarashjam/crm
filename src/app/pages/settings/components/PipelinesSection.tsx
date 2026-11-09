import { useState } from 'react';
import { Kanban, Plus, Pencil, Trash2, ChevronRight } from 'lucide-react';
import type { Pipeline, DealStage } from '@/app/api/types';
import { servesDemoData } from '@/app/api/apiClient';
import { isOrgAdmin } from '@/app/api/organizations';
import { StaleDaysSetting, parseStaleDays } from './StaleDaysSetting';

/** What a stage does to the deals in it: keeps them open, or closes them as won or lost. */
export type StageOutcome = 'open' | 'won' | 'lost';

/** A stage's settings beyond its name. A probability of null takes the stage's default away. */
export interface StageSettings {
  probability: number | null;
  outcome: StageOutcome;
}

/**
 * What saving a new outcome does to the deals already in the stage: the server gives the
 * ones that had the stage's old outcome its new one, as moving them there would, so the
 * board and the reports keep agreeing. A deal closed the other way keeps its outcome.
 */
function outcomeChangeNote(from: StageOutcome, to: StageOutcome): string | null {
  if (from === to) return null;
  const deals = from === 'open' ? 'The open deals in this stage' : `The ${from} deals in this stage`;
  return to === 'open' ? `${deals} are reopened.` : `${deals} are marked ${to}.`;
}

function OutcomeChangeNote({ from, to }: { from: StageOutcome; to: StageOutcome }) {
  const note = outcomeChangeNote(from, to);
  return note ? <p className="basis-full text-xs text-amber-700">{note}</p> : null;
}

/** A pipeline's own stale days; null follows the workspace's. */
export interface PipelineSettings {
  staleDealDays: number | null;
}

/** Text typed as a probability: a whole number 0–100, blank for none, or undefined when it is neither. */
export function parseProbability(text: string): number | null | undefined {
  const trimmed = text.trim();
  if (trimmed === '') return null;
  if (!/^\d{1,3}$/.test(trimmed)) return undefined;
  const n = Number(trimmed);
  return n <= 100 ? n : undefined;
}

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
  handleUpdatePipeline: (id: string, settings?: PipelineSettings) => void;
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
  handleUpdateStage: (stageId: string, pipelineId: string, settings?: StageSettings) => void;
  handleDeleteStage: (stageId: string, pipelineId: string) => void;
  deletingStageId: string | null;
  currentOrg: { id: string; isOwner: boolean; role?: number } | null;
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
  // What the open edit rows say beyond the names, which the page keeps.
  const [editingStaleDays, setEditingStaleDays] = useState('');
  const [editingProbability, setEditingProbability] = useState('');
  const [editingOutcome, setEditingOutcome] = useState<StageOutcome>('open');

  if (servesDemoData() || !isOrgAdmin(currentOrg)) {
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
          <p className="text-slate-600">Pipeline management is available for organization owners and managers.</p>
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

      <StaleDaysSetting />

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
                  <div className="flex-1 flex flex-wrap gap-2 items-center">
                    <input
                      type="text"
                      aria-label="Pipeline name"
                      value={editingPipelineName}
                      onChange={(e) => setEditingPipelineName(e.target.value)}
                      className="flex-1 min-w-0 h-10 px-3 border border-slate-300 rounded-lg text-slate-900"
                    />
                    <label className="flex items-center gap-1.5 text-xs text-slate-600">
                      Stale after
                      <input
                        type="number"
                        min={0}
                        max={365}
                        aria-label={`Stale days for ${p.name}`}
                        placeholder="workspace's"
                        value={editingStaleDays}
                        onChange={(e) => setEditingStaleDays(e.target.value)}
                        className="h-10 w-24 px-2 border border-slate-300 rounded-lg text-slate-900"
                      />
                      days
                    </label>
                    <button
                      type="button"
                      disabled={editingStaleDays.trim() !== '' && parseStaleDays(editingStaleDays) === null}
                      onClick={() => handleUpdatePipeline(p.id, {
                        staleDealDays: editingStaleDays.trim() === '' ? null : parseStaleDays(editingStaleDays),
                      })}
                      aria-label={`Save pipeline ${p.name}`}
                      className="h-10 px-4 bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50"
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
                      <span className="text-xs text-slate-500 font-normal">
                        ({(pipelineStages[p.id] ?? p.dealStages ?? []).length} stages
                        {p.staleDealDays === undefined
                          ? ''
                          : p.staleDealDays === 0 ? ' · never stale' : ` · stale after ${p.staleDealDays} days`})
                      </span>
                    </button>
                    <div className="flex gap-1">
                      <button
                        type="button"
                        onClick={() => {
                          setEditingPipelineId(p.id);
                          setEditingPipelineName(p.name);
                          setEditingStaleDays(p.staleDealDays === undefined ? '' : String(p.staleDealDays));
                        }}
                        aria-label={`Edit pipeline ${p.name}`}
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
                              <div className="flex-1 flex flex-wrap gap-2 items-center">
                                <input
                                  type="text"
                                  aria-label="Stage name"
                                  value={editingStageName}
                                  onChange={(e) => setEditingStageName(e.target.value)}
                                  className="flex-1 min-w-0 h-8 px-2 border border-slate-300 rounded text-slate-900 text-sm"
                                />
                                <label className="flex items-center gap-1 text-xs text-slate-600">
                                  <input
                                    type="number"
                                    min={0}
                                    max={100}
                                    aria-label={`Probability for ${s.name}`}
                                    placeholder="none"
                                    value={editingProbability}
                                    onChange={(e) => setEditingProbability(e.target.value)}
                                    className="h-8 w-16 px-2 border border-slate-300 rounded text-slate-900 text-sm"
                                  />
                                  %
                                </label>
                                <select
                                  aria-label={`Outcome for ${s.name}`}
                                  value={editingOutcome}
                                  onChange={(e) => setEditingOutcome(e.target.value as StageOutcome)}
                                  className="h-8 px-2 border border-slate-300 rounded text-slate-900 text-sm bg-white"
                                >
                                  <option value="open">Open</option>
                                  <option value="won">Won</option>
                                  <option value="lost">Lost</option>
                                </select>
                                <button
                                  type="button"
                                  disabled={parseProbability(editingProbability) === undefined}
                                  onClick={() => handleUpdateStage(s.id, p.id, {
                                    probability: parseProbability(editingProbability) ?? null,
                                    outcome: editingOutcome,
                                  })}
                                  aria-label={`Save stage ${s.name}`}
                                  className="h-8 px-3 bg-emerald-600 text-white rounded text-sm disabled:opacity-50"
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
                                <OutcomeChangeNote from={s.isWon ? 'won' : s.isLost ? 'lost' : 'open'} to={editingOutcome} />
                              </div>
                            ) : (
                              <>
                                <div className="flex items-center gap-2">
                                  <span className="w-6 h-6 rounded-full bg-orange-100 text-orange-700 text-xs font-medium flex items-center justify-center">
                                    {idx + 1}
                                  </span>
                                  <span className="font-medium text-slate-800">
                                    {s.name}
                                    {s.isWon && <span className="text-emerald-700 text-xs ml-1">(Won)</span>}
                                    {s.isLost && <span className="text-red-600 text-xs ml-1">(Lost)</span>}
                                    {s.probability !== undefined && (
                                      <span className="text-slate-500 text-xs ml-1" title="The chance a deal here is won, for deals with no probability of their own">
                                        {s.probability}%
                                      </span>
                                    )}
                                  </span>
                                </div>
                                <div className="flex gap-1">
                                  <button
                                    type="button"
                                    aria-label={`Edit ${s.name}`}
                                    onClick={() => {
                                      setEditingStageId(s.id);
                                      setEditingStageName(s.name);
                                      setEditingProbability(s.probability === undefined ? '' : String(s.probability));
                                      setEditingOutcome(s.isWon ? 'won' : s.isLost ? 'lost' : 'open');
                                    }}
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
