import { Search, SlidersHorizontal, ArrowUpDown, ArrowUp, ArrowDown, X, Tag, Sparkles } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';

interface LeadFiltersProps {
  searchQuery: string;
  onSearchChange: (value: string) => void;
  filterStatus: string;
  onFilterStatusChange: (value: string) => void;
  filterSource: string;
  onFilterSourceChange: (value: string) => void;
  filterConverted: 'all' | 'converted' | 'active';
  onFilterConvertedChange: (value: 'all' | 'converted' | 'active') => void;
  sortField: 'name' | 'email' | 'status' | 'createdAt';
  sortDirection: 'asc' | 'desc';
  onSortChange: (field: 'name' | 'email' | 'status' | 'createdAt', direction: 'asc' | 'desc') => void;
  statusOptions: { id: string; name: string }[];
  sourceOptions: { id: string; name: string }[];
  showFilters: boolean;
  onToggleFilters: () => void;
  activeFilterCount: number;
}

export function LeadFilters({
  searchQuery,
  onSearchChange,
  filterStatus,
  onFilterStatusChange,
  filterSource,
  onFilterSourceChange,
  filterConverted,
  onFilterConvertedChange,
  sortField,
  sortDirection,
  onSortChange,
  statusOptions,
  sourceOptions,
  showFilters,
  onToggleFilters,
  activeFilterCount,
}: LeadFiltersProps) {
  return (
    <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
      {/* Decorative elements */}
      <div className="absolute top-0 right-0 w-32 h-32 bg-orange-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
      <div className="absolute bottom-0 left-0 w-24 h-24 bg-violet-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
      
      <div className="relative flex flex-col sm:flex-row gap-3">
        {/* Search Input */}
        <div className="relative flex-1 group">
          <div className="absolute inset-0 bg-gradient-to-r from-orange-500/20 via-amber-500/20 to-orange-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
          <div className="relative">
            <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-orange-500/20 to-amber-500/20 flex items-center justify-center group-focus-within:from-orange-500/40 group-focus-within:to-amber-500/40 transition-all duration-300">
              <Search className="w-4 h-4 text-orange-300 group-focus-within:text-orange-200 transition-colors" aria-hidden />
            </div>
            <Input
              type="search"
              placeholder="Search by name, email, or phone..."
              value={searchQuery}
              onChange={(e) => onSearchChange(e.target.value)}
              className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
              aria-label="Search leads"
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => onSearchChange('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 w-6 h-6 rounded-md bg-white/10 hover:bg-red-500/30 flex items-center justify-center text-slate-400 hover:text-red-300 transition-all duration-200"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>

        {/* Filter & Sort Controls */}
        <div className="flex gap-2">
          {/* Filter Button */}
          <div className="relative group/btn">
            <div className={`absolute inset-0 rounded-xl blur-lg transition-all duration-300 ${
              showFilters || activeFilterCount > 0 
                ? 'bg-gradient-to-r from-orange-500/40 to-amber-500/40 opacity-100' 
                : 'bg-white/10 opacity-0 group-hover/btn:opacity-50'
            }`} />
            <Button
              variant="outline"
              onClick={onToggleFilters}
              className={`relative h-11 px-4 rounded-xl border shadow-xl shadow-black/10 transition-all duration-300 ${
                showFilters || activeFilterCount > 0
                  ? 'bg-gradient-to-r from-orange-500 to-amber-500 border-orange-400/50 text-white hover:from-orange-400 hover:to-amber-400 hover:shadow-orange-500/25'
                  : 'bg-white/5 backdrop-blur-md border-white/10 text-white hover:bg-white/10 hover:border-white/20'
              }`}
            >
              <div className={`w-6 h-6 rounded-lg flex items-center justify-center mr-2 transition-all duration-300 ${
                showFilters || activeFilterCount > 0 ? 'bg-white/20' : 'bg-white/10'
              }`}>
                <SlidersHorizontal className="w-3.5 h-3.5" />
              </div>
              <span className="font-medium text-sm">Filters</span>
              {activeFilterCount > 0 && (
                <span className="ml-2 flex items-center justify-center w-5 h-5 rounded-md bg-white/25 text-white text-xs font-bold">
                  {activeFilterCount}
                </span>
              )}
            </Button>
          </div>

          {/* Sort Dropdown */}
          <div className="relative group/sort">
            <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/sort:opacity-50 transition-all duration-300" />
            <Select
              value={`${sortField}-${sortDirection}`}
              onValueChange={(v) => {
                const [field, dir] = v.split('-') as [typeof sortField, typeof sortDirection];
                onSortChange(field, dir);
              }}
            >
              <SelectTrigger className="relative h-11 w-[180px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                    <ArrowUpDown className="w-3.5 h-3.5 text-slate-300" />
                  </div>
                  <SelectValue placeholder="Sort by..." />
                </div>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="createdAt-desc">
                  <span className="flex items-center gap-2">
                    <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                    Newest first
                  </span>
                </SelectItem>
                <SelectItem value="createdAt-asc">
                  <span className="flex items-center gap-2">
                    <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                    Oldest first
                  </span>
                </SelectItem>
                <SelectItem value="name-asc">
                  <span className="flex items-center gap-2">
                    <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                    Name A-Z
                  </span>
                </SelectItem>
                <SelectItem value="name-desc">
                  <span className="flex items-center gap-2">
                    <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                    Name Z-A
                  </span>
                </SelectItem>
                <SelectItem value="status-asc">
                  <span className="flex items-center gap-2">
                    <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                    Status A-Z
                  </span>
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {/* Expandable Filter Panel */}
      {showFilters && (
        <div className="mt-4 bg-white/10 backdrop-blur-sm rounded-xl border border-white/10 p-4 animate-in slide-in-from-top-2 duration-200">
          <div className="flex flex-wrap gap-4">
            {/* Status Filter */}
            <div className="min-w-[160px]">
              <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                <Tag className="w-3.5 h-3.5" />
                Status
              </label>
              <Select value={filterStatus} onValueChange={onFilterStatusChange}>
                <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                  <SelectValue placeholder="All statuses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">
                    <span className="flex items-center gap-2">
                      <span className="w-2 h-2 rounded-full bg-slate-300" />
                      All statuses
                    </span>
                  </SelectItem>
                  {statusOptions.map((s) => {
                    const colors: Record<string, string> = {
                      New: 'bg-blue-500',
                      Contacted: 'bg-amber-500',
                      Qualified: 'bg-emerald-500',
                      Lost: 'bg-slate-400',
                    };
                    return (
                      <SelectItem key={s.id} value={s.name}>
                        <span className="flex items-center gap-2">
                          <span className={`w-2 h-2 rounded-full ${colors[s.name] || 'bg-slate-400'}`} />
                          {s.name}
                        </span>
                      </SelectItem>
                    );
                  })}
                </SelectContent>
              </Select>
            </div>

            {/* Source Filter */}
            <div className="min-w-[160px]">
              <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                <Sparkles className="w-3.5 h-3.5" />
                Source
              </label>
              <Select value={filterSource} onValueChange={onFilterSourceChange}>
                <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                  <SelectValue placeholder="All sources" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All sources</SelectItem>
                  {sourceOptions.map((s) => (
                    <SelectItem key={s.id} value={s.name}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Converted Filter */}
            <div className="min-w-[160px]">
              <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                Conversion Status
              </label>
              <Select value={filterConverted} onValueChange={(v) => onFilterConvertedChange(v as 'all' | 'converted' | 'active')}>
                <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All leads</SelectItem>
                  <SelectItem value="active">Active only</SelectItem>
                  <SelectItem value="converted">Converted only</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default LeadFilters;
