import { useState, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Upload, FileSpreadsheet, CheckCircle2, XCircle, AlertTriangle, ArrowLeft,
  Download, Sparkles, Users, Mail, Phone, Building2, Tag, Trash2, Eye, 
  ChevronRight, FileWarning, Check
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { createLead, getLeadStatuses, getLeadSources } from '@/app/api';
import type { LeadStatus, LeadSource } from '@/app/api/types';

type CSVRow = Record<string, string>;
type ColumnMapping = {
  name: string;
  email: string;
  phone: string;
  source: string;
  status: string;
  company: string;
};

type ImportResult = {
  success: number;
  failed: number;
  errors: { row: number; name: string; error: string }[];
};

const REQUIRED_FIELDS = ['name', 'email'] as const;
const OPTIONAL_FIELDS = ['phone', 'source', 'status', 'company'] as const;
const ALL_FIELDS = [...REQUIRED_FIELDS, ...OPTIONAL_FIELDS] as const;

const FIELD_INFO: Record<string, { label: string; icon: React.ReactNode; required: boolean; color: string }> = {
  name: { label: 'Full Name', icon: <Users className="w-3.5 h-3.5" />, required: true, color: 'orange' },
  email: { label: 'Email Address', icon: <Mail className="w-3.5 h-3.5" />, required: true, color: 'blue' },
  phone: { label: 'Phone Number', icon: <Phone className="w-3.5 h-3.5" />, required: false, color: 'emerald' },
  source: { label: 'Lead Source', icon: <Sparkles className="w-3.5 h-3.5" />, required: false, color: 'amber' },
  status: { label: 'Lead Status', icon: <Tag className="w-3.5 h-3.5" />, required: false, color: 'cyan' },
  company: { label: 'Company', icon: <Building2 className="w-3.5 h-3.5" />, required: false, color: 'purple' },
};

export default function LeadImport() {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  
  // State
  const [step, setStep] = useState<'upload' | 'mapping' | 'preview' | 'importing' | 'complete'>('upload');
  const [file, setFile] = useState<File | null>(null);
  const [csvHeaders, setCsvHeaders] = useState<string[]>([]);
  const [csvData, setCsvData] = useState<CSVRow[]>([]);
  const [columnMapping, setColumnMapping] = useState<ColumnMapping>({
    name: '',
    email: '',
    phone: '',
    source: '',
    status: '',
    company: '',
  });
  const [_importing, setImporting] = useState(false);
  const [importProgress, setImportProgress] = useState(0);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [leadStatuses, setLeadStatuses] = useState<LeadStatus[]>([]);
  const [leadSources, setLeadSources] = useState<LeadSource[]>([]);
  const [defaultSource, setDefaultSource] = useState('csv_import');
  const [defaultStatus, setDefaultStatus] = useState('New');

  // Parse CSV
  const parseCSV = (text: string): { headers: string[]; rows: CSVRow[] } => {
    const lines = text.split(/\r?\n/).filter(line => line.trim());
    if (lines.length < 2) return { headers: [], rows: [] };
    
    const parseRow = (line: string): string[] => {
      const result: string[] = [];
      let current = '';
      let inQuotes = false;
      
      for (let i = 0; i < line.length; i++) {
        const char = line[i];
        if (char === '"') {
          inQuotes = !inQuotes;
        } else if (char === ',' && !inQuotes) {
          result.push(current.trim());
          current = '';
        } else {
          current += char;
        }
      }
      result.push(current.trim());
      return result;
    };
    
    const firstLine = lines[0];
    if (!firstLine) return { headers: [], rows: [] };
    
    const headers = parseRow(firstLine);
    const rows = lines.slice(1).map(line => {
      const values = parseRow(line);
      const row: CSVRow = {};
      headers.forEach((header, i) => {
        row[header] = values[i] || '';
      });
      return row;
    }).filter(row => Object.values(row).some(v => v));
    
    return { headers, rows };
  };

  // Auto-detect column mapping
  const autoDetectMapping = (headers: string[]): Partial<ColumnMapping> => {
    const mapping: Partial<ColumnMapping> = {};
    const lowerHeaders = headers.map(h => h.toLowerCase());
    
    const patterns: Record<keyof ColumnMapping, RegExp[]> = {
      name: [/^name$/i, /full.?name/i, /lead.?name/i, /contact.?name/i, /^first.?name$/i],
      email: [/email/i, /e-mail/i, /mail/i],
      phone: [/phone/i, /tel/i, /mobile/i, /cell/i],
      source: [/source/i, /lead.?source/i, /origin/i, /channel/i],
      status: [/status/i, /lead.?status/i, /state/i],
      company: [/company/i, /organization/i, /org/i, /business/i, /employer/i],
    };
    
    for (const [field, fieldPatterns] of Object.entries(patterns)) {
      for (const pattern of fieldPatterns) {
        const matchIndex = lowerHeaders.findIndex(h => pattern.test(h));
        if (matchIndex !== -1) {
          const matchedHeader = headers[matchIndex];
          if (matchedHeader && !Object.values(mapping).includes(matchedHeader)) {
            mapping[field as keyof ColumnMapping] = matchedHeader;
            break;
          }
        }
      }
    }
    
    return mapping;
  };

  // Handle file upload
  const handleFile = useCallback(async (uploadedFile: File) => {
    if (!uploadedFile.name.endsWith('.csv')) {
      toast.error('Please upload a CSV file');
      return;
    }
    
    setFile(uploadedFile);
    
    try {
      const text = await uploadedFile.text();
      const { headers, rows } = parseCSV(text);
      
      if (headers.length === 0) {
        toast.error('CSV file appears to be empty or invalid');
        return;
      }
      
      setCsvHeaders(headers);
      setCsvData(rows);
      
      const autoMapping = autoDetectMapping(headers);
      setColumnMapping(prev => ({ ...prev, ...autoMapping }));
      
      // Load statuses and sources
      const [statuses, sources] = await Promise.all([getLeadStatuses(), getLeadSources()]);
      setLeadStatuses(statuses || []);
      setLeadSources(sources || []);
      
      setStep('mapping');
      toast.success(`Loaded ${rows.length} rows from CSV`);
    } catch {
      toast.error('Failed to parse CSV file');
    }
  }, []);

  // Drag and drop handlers
  const handleDrag = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === 'dragenter' || e.type === 'dragover') {
      setDragActive(true);
    } else if (e.type === 'dragleave') {
      setDragActive(false);
    }
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      handleFile(e.dataTransfer.files[0]);
    }
  }, [handleFile]);

  // Validate mapping
  const isMappingValid = columnMapping.name && columnMapping.email;

  // Get mapped data preview
  const getMappedData = () => {
    return csvData.map(row => ({
      name: row[columnMapping.name] || '',
      email: row[columnMapping.email] || '',
      phone: columnMapping.phone ? row[columnMapping.phone] : '',
      source: columnMapping.source ? row[columnMapping.source] : defaultSource,
      status: columnMapping.status ? row[columnMapping.status] : defaultStatus,
      company: columnMapping.company ? row[columnMapping.company] : '',
    }));
  };

  // Import leads
  const handleImport = async () => {
    setImporting(true);
    setStep('importing');
    setImportProgress(0);
    
    const mappedData = getMappedData();
    const result: ImportResult = { success: 0, failed: 0, errors: [] };
    
    for (let i = 0; i < mappedData.length; i++) {
      const lead = mappedData[i];
      
      if (!lead) {
        result.failed++;
        result.errors.push({ row: i + 2, name: '(empty)', error: 'Lead data is missing' });
        continue;
      }
      
      try {
        if (!lead.name.trim() || !lead.email.trim()) {
          result.failed++;
          result.errors.push({ row: i + 2, name: lead.name || '(empty)', error: 'Missing name or email' });
          continue;
        }
        
        const created = await createLead({
          name: lead.name.trim(),
          email: lead.email.trim(),
          phone: lead.phone?.trim() || undefined,
          source: lead.source || defaultSource,
          status: lead.status || defaultStatus,
        });
        
        if (created) {
          result.success++;
        } else {
          result.failed++;
          result.errors.push({ row: i + 2, name: lead.name, error: 'Failed to create lead' });
        }
      } catch {
        result.failed++;
        result.errors.push({ row: i + 2, name: lead?.name || '(empty)', error: 'API error' });
      }
      
      setImportProgress(Math.round(((i + 1) / mappedData.length) * 100));
    }
    
    setImportResult(result);
    setImporting(false);
    setStep('complete');
    
    if (result.success > 0) {
      toast.success(`Successfully imported ${result.success} leads`);
    }
    if (result.failed > 0) {
      toast.error(`Failed to import ${result.failed} leads`);
    }
  };

  // Download sample CSV
  const downloadSampleCSV = () => {
    const sample = `name,email,phone,source,status,company
John Smith,john@example.com,+1 555 123 4567,website,New,Acme Corp
Jane Doe,jane@example.com,+1 555 987 6543,referral,Contacted,Tech Solutions
Bob Wilson,bob@example.com,,ads,New,`;
    
    const blob = new Blob([sample], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'leads_sample.csv';
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        {/* Header */}
        <div className="mb-8">
          <Button
            variant="ghost"
            onClick={() => navigate('/leads')}
            className="gap-2 text-slate-600 hover:text-slate-900 -ml-2 mb-4"
          >
            <ArrowLeft className="w-4 h-4" />
            Back to Leads
          </Button>
          <div className="flex items-center gap-4">
            <div className="flex items-center justify-center w-14 h-14 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 shadow-lg shadow-orange-200/50">
              <Upload className="w-7 h-7 text-white" />
            </div>
            <div>
              <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Import Leads</h1>
              <p className="text-slate-600 mt-1">Upload a CSV file to bulk import leads into your CRM</p>
            </div>
          </div>
        </div>

        {/* Progress Steps */}
        <div className="mb-8">
          <div className="flex items-center justify-between max-w-2xl">
            {['Upload', 'Map Columns', 'Preview', 'Import'].map((label, i) => {
              const stepNames = ['upload', 'mapping', 'preview', 'importing'] as const;
              const currentIndex = stepNames.indexOf(step === 'complete' ? 'importing' : step);
              const isActive = i === currentIndex;
              const isComplete = i < currentIndex || step === 'complete';
              
              return (
                <div key={label} className="flex items-center">
                  <div className="flex flex-col items-center">
                    <div className={`w-10 h-10 rounded-full flex items-center justify-center font-medium text-sm transition-all ${
                      isComplete
                        ? 'bg-emerald-500 text-white'
                        : isActive
                          ? 'bg-orange-500 text-white shadow-lg shadow-orange-200/50'
                          : 'bg-slate-100 text-slate-400'
                    }`}>
                      {isComplete ? <Check className="w-5 h-5" /> : i + 1}
                    </div>
                    <span className={`mt-2 text-xs font-medium ${isActive || isComplete ? 'text-slate-700' : 'text-slate-400'}`}>
                      {label}
                    </span>
                  </div>
                  {i < 3 && (
                    <div className={`w-16 sm:w-24 h-1 mx-2 rounded-full ${
                      isComplete ? 'bg-emerald-500' : 'bg-slate-200'
                    }`} />
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Step 1: Upload */}
        {step === 'upload' && (
          <div className="space-y-6">
            <div
              onDragEnter={handleDrag}
              onDragLeave={handleDrag}
              onDragOver={handleDrag}
              onDrop={handleDrop}
              onClick={() => fileInputRef.current?.click()}
              className={`relative border-2 border-dashed rounded-2xl p-12 text-center cursor-pointer transition-all ${
                dragActive
                  ? 'border-orange-400 bg-orange-50'
                  : 'border-slate-200 hover:border-orange-300 hover:bg-slate-50'
              }`}
            >
              <input
                ref={fileInputRef}
                type="file"
                accept=".csv"
                onChange={(e) => e.target.files?.[0] && handleFile(e.target.files[0])}
                className="hidden"
              />
              
              <div className="flex flex-col items-center">
                <div className={`w-20 h-20 rounded-2xl flex items-center justify-center mb-6 transition-all ${
                  dragActive
                    ? 'bg-orange-500 shadow-lg shadow-orange-200/50'
                    : 'bg-gradient-to-br from-slate-100 to-slate-50'
                }`}>
                  <FileSpreadsheet className={`w-10 h-10 ${dragActive ? 'text-white' : 'text-slate-400'}`} />
                </div>
                
                <h3 className="text-lg font-semibold text-slate-800 mb-2">
                  {dragActive ? 'Drop your CSV here' : 'Drag & drop your CSV file'}
                </h3>
                <p className="text-slate-500 mb-4">or click to browse from your computer</p>
                
                <div className="flex items-center gap-2 text-sm text-slate-400">
                  <span>Supported format:</span>
                  <span className="px-2 py-0.5 bg-slate-100 rounded text-slate-600 font-mono">.csv</span>
                </div>
              </div>
            </div>

            {/* Sample CSV Download */}
            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <div className="flex items-start gap-4">
                <div className="flex items-center justify-center w-10 h-10 rounded-lg bg-blue-100 text-blue-600 shrink-0">
                  <Download className="w-5 h-5" />
                </div>
                <div className="flex-1">
                  <h4 className="font-semibold text-slate-800 mb-1">Need a template?</h4>
                  <p className="text-sm text-slate-500 mb-3">
                    Download our sample CSV file to see the expected format with example data.
                  </p>
                  <Button variant="outline" onClick={downloadSampleCSV} className="gap-2">
                    <Download className="w-4 h-4" />
                    Download Sample CSV
                  </Button>
                </div>
              </div>
            </div>

            {/* Tips */}
            <div className="bg-gradient-to-br from-amber-50 to-orange-50 rounded-xl border border-amber-200/50 p-6">
              <h4 className="font-semibold text-amber-800 flex items-center gap-2 mb-3">
                <Sparkles className="w-5 h-5" />
                Tips for successful import
              </h4>
              <ul className="space-y-2 text-sm text-amber-700">
                <li className="flex items-start gap-2">
                  <CheckCircle2 className="w-4 h-4 mt-0.5 text-emerald-500" />
                  Include a header row with column names (name, email, phone, etc.)
                </li>
                <li className="flex items-start gap-2">
                  <CheckCircle2 className="w-4 h-4 mt-0.5 text-emerald-500" />
                  <strong>Name</strong> and <strong>Email</strong> columns are required
                </li>
                <li className="flex items-start gap-2">
                  <CheckCircle2 className="w-4 h-4 mt-0.5 text-emerald-500" />
                  Use UTF-8 encoding for special characters
                </li>
                <li className="flex items-start gap-2">
                  <CheckCircle2 className="w-4 h-4 mt-0.5 text-emerald-500" />
                  Maximum recommended: 1,000 leads per import
                </li>
              </ul>
            </div>
          </div>
        )}

        {/* Step 2: Column Mapping */}
        {step === 'mapping' && (
          <div className="space-y-6">
            {/* File Info */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center">
                  <FileSpreadsheet className="w-5 h-5 text-emerald-600" />
                </div>
                <div>
                  <p className="font-medium text-slate-800">{file?.name}</p>
                  <p className="text-sm text-slate-500">{csvData.length} rows detected</p>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => { setStep('upload'); setFile(null); setCsvData([]); }}
                className="gap-1.5 text-slate-600"
              >
                <Trash2 className="w-4 h-4" />
                Remove
              </Button>
            </div>

            {/* Mapping */}
            <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
              <div className="px-6 py-4 border-b border-slate-100 bg-slate-50/50">
                <h3 className="font-semibold text-slate-800">Map your columns</h3>
                <p className="text-sm text-slate-500 mt-1">Match your CSV columns to lead fields. We've auto-detected some for you.</p>
              </div>
              
              <div className="p-6 space-y-4">
                {ALL_FIELDS.map((field) => {
                  const info = FIELD_INFO[field];
                  if (!info) return null;
                  const colorClasses = {
                    orange: 'bg-orange-100 text-orange-600',
                    blue: 'bg-blue-100 text-blue-600',
                    emerald: 'bg-emerald-100 text-emerald-600',
                    amber: 'bg-amber-100 text-amber-600',
                    cyan: 'bg-cyan-100 text-cyan-600',
                    purple: 'bg-purple-100 text-purple-600',
                  };
                  
                  return (
                    <div key={field} className="flex items-center gap-4">
                      <div className="w-44 shrink-0">
                        <Label className="flex items-center gap-2 text-sm font-medium text-slate-700">
                          <div className={`flex items-center justify-center w-6 h-6 rounded-md ${colorClasses[info.color as keyof typeof colorClasses]}`}>
                            {info.icon}
                          </div>
                          {info.label}
                          {info.required && <span className="text-orange-500">*</span>}
                        </Label>
                      </div>
                      <ChevronRight className="w-4 h-4 text-slate-300 shrink-0" />
                      <Select
                        value={columnMapping[field as keyof ColumnMapping] || 'skip'}
                        onValueChange={(v) => setColumnMapping(prev => ({ ...prev, [field]: v === 'skip' ? '' : v }))}
                      >
                        <SelectTrigger className={`flex-1 ${columnMapping[field as keyof ColumnMapping] ? 'border-emerald-300 bg-emerald-50/50' : ''}`}>
                          <SelectValue placeholder="Select a column" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="skip">
                            <span className="text-slate-400">— Skip this field —</span>
                          </SelectItem>
                          {csvHeaders.map(header => (
                            <SelectItem key={header} value={header}>{header}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {columnMapping[field as keyof ColumnMapping] && (
                        <CheckCircle2 className="w-5 h-5 text-emerald-500 shrink-0" />
                      )}
                    </div>
                  );
                })}
              </div>

              {/* Default values for unmapped fields */}
              <div className="px-6 py-4 border-t border-slate-100 bg-slate-50/30">
                <p className="text-sm font-medium text-slate-600 mb-3">Default values for unmapped fields</p>
                <div className="flex flex-wrap gap-4">
                  <div className="flex items-center gap-2">
                    <Label className="text-sm text-slate-500">Source:</Label>
                    <Select value={defaultSource} onValueChange={setDefaultSource}>
                      <SelectTrigger className="w-32 h-8 text-sm">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="csv_import">csv_import</SelectItem>
                        {leadSources.map(s => (
                          <SelectItem key={s.id} value={s.name}>{s.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex items-center gap-2">
                    <Label className="text-sm text-slate-500">Status:</Label>
                    <Select value={defaultStatus} onValueChange={setDefaultStatus}>
                      <SelectTrigger className="w-32 h-8 text-sm">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="New">New</SelectItem>
                        {leadStatuses.map(s => (
                          <SelectItem key={s.id} value={s.name}>{s.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              </div>
            </div>

            {/* Validation */}
            {!isMappingValid && (
              <div className="flex items-center gap-3 p-4 rounded-lg bg-amber-50 border border-amber-200">
                <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0" />
                <p className="text-sm text-amber-700">
                  Please map both <strong>Name</strong> and <strong>Email</strong> columns to continue.
                </p>
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-between pt-4">
              <Button variant="outline" onClick={() => setStep('upload')} className="gap-2">
                <ArrowLeft className="w-4 h-4" />
                Back
              </Button>
              <Button
                onClick={() => setStep('preview')}
                disabled={!isMappingValid}
                className="gap-2 bg-orange-600 hover:bg-orange-500"
              >
                Preview Import
                <Eye className="w-4 h-4" />
              </Button>
            </div>
          </div>
        )}

        {/* Step 3: Preview */}
        {step === 'preview' && (
          <div className="space-y-6">
            <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
              <div className="px-6 py-4 border-b border-slate-100 bg-slate-50/50 flex items-center justify-between">
                <div>
                  <h3 className="font-semibold text-slate-800">Preview your import</h3>
                  <p className="text-sm text-slate-500 mt-1">Review the first 10 rows before importing</p>
                </div>
                <span className="px-3 py-1 rounded-full bg-orange-100 text-orange-700 text-sm font-medium">
                  {csvData.length} leads total
                </span>
              </div>
              
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="bg-slate-50">
                      <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">#</th>
                      <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Name</th>
                      <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Email</th>
                      <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Phone</th>
                      <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Source</th>
                      <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {getMappedData().slice(0, 10).map((row, i) => {
                      const hasError = !row.name || !row.email;
                      return (
                        <tr key={i} className={hasError ? 'bg-red-50' : 'hover:bg-slate-50'}>
                          <td className="px-4 py-3 text-sm text-slate-400">{i + 1}</td>
                          <td className="px-4 py-3 text-sm font-medium text-slate-800">
                            {row.name || <span className="text-red-500 italic">Missing</span>}
                          </td>
                          <td className="px-4 py-3 text-sm text-slate-600">
                            {row.email || <span className="text-red-500 italic">Missing</span>}
                          </td>
                          <td className="px-4 py-3 text-sm text-slate-500">{row.phone || '—'}</td>
                          <td className="px-4 py-3 text-sm">
                            <span className="px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs">
                              {row.source}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-sm">
                            <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-xs">
                              {row.status}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              
              {csvData.length > 10 && (
                <div className="px-6 py-3 border-t border-slate-100 bg-slate-50/50 text-center text-sm text-slate-500">
                  ... and {csvData.length - 10} more rows
                </div>
              )}
            </div>

            {/* Actions */}
            <div className="flex items-center justify-between pt-4">
              <Button variant="outline" onClick={() => setStep('mapping')} className="gap-2">
                <ArrowLeft className="w-4 h-4" />
                Back to Mapping
              </Button>
              <Button
                onClick={handleImport}
                className="gap-2 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-200/50"
              >
                <Sparkles className="w-4 h-4" />
                Import {csvData.length} Leads
              </Button>
            </div>
          </div>
        )}

        {/* Step 4: Importing */}
        {step === 'importing' && (
          <div className="flex flex-col items-center justify-center py-16">
            <div className="relative w-32 h-32 mb-8">
              <svg className="w-32 h-32 transform -rotate-90">
                <circle
                  cx="64"
                  cy="64"
                  r="56"
                  stroke="currentColor"
                  strokeWidth="12"
                  fill="none"
                  className="text-slate-100"
                />
                <circle
                  cx="64"
                  cy="64"
                  r="56"
                  stroke="currentColor"
                  strokeWidth="12"
                  fill="none"
                  strokeDasharray={2 * Math.PI * 56}
                  strokeDashoffset={2 * Math.PI * 56 * (1 - importProgress / 100)}
                  className="text-orange-500 transition-all duration-300"
                  strokeLinecap="round"
                />
              </svg>
              <div className="absolute inset-0 flex items-center justify-center">
                <span className="text-3xl font-bold text-slate-800">{importProgress}%</span>
              </div>
            </div>
            <h3 className="text-xl font-semibold text-slate-800 mb-2">Importing leads...</h3>
            <p className="text-slate-500">Please wait while we process your data</p>
          </div>
        )}

        {/* Step 5: Complete */}
        {step === 'complete' && importResult && (
          <div className="space-y-6">
            {/* Success/Summary Card */}
            <div className={`rounded-2xl border-2 p-8 text-center ${
              importResult.failed === 0
                ? 'bg-gradient-to-br from-emerald-50 to-green-50 border-emerald-200'
                : importResult.success === 0
                  ? 'bg-gradient-to-br from-red-50 to-orange-50 border-red-200'
                  : 'bg-gradient-to-br from-amber-50 to-yellow-50 border-amber-200'
            }`}>
              <div className={`w-20 h-20 rounded-full mx-auto mb-6 flex items-center justify-center ${
                importResult.failed === 0
                  ? 'bg-emerald-500'
                  : importResult.success === 0
                    ? 'bg-red-500'
                    : 'bg-amber-500'
              }`}>
                {importResult.failed === 0 ? (
                  <CheckCircle2 className="w-10 h-10 text-white" />
                ) : importResult.success === 0 ? (
                  <XCircle className="w-10 h-10 text-white" />
                ) : (
                  <AlertTriangle className="w-10 h-10 text-white" />
                )}
              </div>
              
              <h2 className="text-2xl font-bold text-slate-800 mb-2">
                {importResult.failed === 0
                  ? 'Import Complete!'
                  : importResult.success === 0
                    ? 'Import Failed'
                    : 'Import Completed with Errors'}
              </h2>
              
              <div className="flex items-center justify-center gap-6 mt-6">
                <div className="text-center">
                  <div className="text-3xl font-bold text-emerald-600">{importResult.success}</div>
                  <div className="text-sm text-slate-500">Imported</div>
                </div>
                <div className="w-px h-12 bg-slate-200" />
                <div className="text-center">
                  <div className="text-3xl font-bold text-red-500">{importResult.failed}</div>
                  <div className="text-sm text-slate-500">Failed</div>
                </div>
              </div>
            </div>

            {/* Error Details */}
            {importResult.errors.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-slate-100 bg-red-50/50">
                  <h3 className="font-semibold text-red-800 flex items-center gap-2">
                    <FileWarning className="w-5 h-5" />
                    Failed Rows ({importResult.errors.length})
                  </h3>
                </div>
                <div className="max-h-64 overflow-y-auto">
                  <table className="w-full">
                    <thead>
                      <tr className="bg-slate-50">
                        <th className="px-4 py-2 text-left text-xs font-semibold text-slate-500">Row</th>
                        <th className="px-4 py-2 text-left text-xs font-semibold text-slate-500">Name</th>
                        <th className="px-4 py-2 text-left text-xs font-semibold text-slate-500">Error</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {importResult.errors.map((err, i) => (
                        <tr key={i} className="bg-red-50/30">
                          <td className="px-4 py-2 text-sm text-slate-600">{err.row}</td>
                          <td className="px-4 py-2 text-sm text-slate-800">{err.name}</td>
                          <td className="px-4 py-2 text-sm text-red-600">{err.error}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-center gap-4 pt-4">
              <Button variant="outline" onClick={() => { setStep('upload'); setFile(null); setCsvData([]); setImportResult(null); }} className="gap-2">
                <Upload className="w-4 h-4" />
                Import More
              </Button>
              <Button onClick={() => navigate('/leads')} className="gap-2 bg-orange-600 hover:bg-orange-500">
                <Users className="w-4 h-4" />
                View Leads
              </Button>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
