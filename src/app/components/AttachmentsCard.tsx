import { useCallback, useEffect, useRef, useState } from 'react';
import { Paperclip, Upload, Trash2, Download, Loader2, FileText } from 'lucide-react';
import { toast } from 'sonner';
import { cn } from '@/app/components/ui/utils';
import {
  getAttachments, uploadAttachment, deleteAttachment, attachmentHref,
  type AttachmentEntity, type Attachment,
} from '@/app/api';

interface AttachmentsCardProps {
  entityType: AttachmentEntity;
  recordId: string;
  className?: string;
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** File attachments for a record: upload (button or drag-drop), list, download, delete. */
export default function AttachmentsCard({ entityType, recordId, className }: AttachmentsCardProps) {
  const [items, setItems] = useState<Attachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [dragging, setDragging] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const load = useCallback(() => {
    setLoading(true);
    getAttachments(entityType, recordId).then(setItems).catch(() => setItems([])).finally(() => setLoading(false));
  }, [entityType, recordId]);
  useEffect(() => { load(); }, [load]);

  const doUpload = async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    setUploading(true);
    try {
      for (const file of Array.from(files)) {
        const res = await uploadAttachment(entityType, recordId, file);
        if (res) setItems((prev) => [res, ...prev]);
        else toast.error(`Failed to upload ${file.name}`);
      }
      toast.success('Uploaded');
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  };

  const remove = async (a: Attachment) => {
    setItems((prev) => prev.filter((x) => x.id !== a.id));
    const ok = await deleteAttachment(a.id);
    if (ok) toast.success('Attachment removed'); else { toast.error('Failed to remove'); load(); }
  };

  const download = (a: Attachment) => {
    const href = attachmentHref(a);
    if (!href) { toast.error('File content unavailable'); return; }
    const link = document.createElement('a');
    link.href = href;
    link.download = a.fileName;
    link.target = '_blank';
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className={className ?? 'rounded-2xl border border-slate-200 bg-white p-5 shadow-sm'}>
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-700">
          <Paperclip className="h-4 w-4 text-indigo-500" /> Attachments
          {items.length > 0 && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500">{items.length}</span>}
        </h3>
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          disabled={uploading}
          className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800 disabled:opacity-60"
        >
          {uploading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Upload className="h-3.5 w-3.5" />} Upload
        </button>
        <input ref={inputRef} type="file" multiple className="hidden" onChange={(e) => doUpload(e.target.files)} />
      </div>

      {loading ? (
        <div className="py-4 text-center text-slate-400"><Loader2 className="mx-auto h-4 w-4 animate-spin" /></div>
      ) : items.length === 0 ? (
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => { e.preventDefault(); setDragging(false); doUpload(e.dataTransfer.files); }}
          className={cn(
            'flex w-full flex-col items-center gap-1 rounded-xl border border-dashed px-4 py-6 text-sm transition-colors',
            dragging ? 'border-indigo-400 bg-indigo-50/50 text-indigo-700' : 'border-slate-200 text-slate-400 hover:border-indigo-300 hover:text-indigo-600',
          )}
        >
          <Upload className="h-5 w-5" />
          Drop a file here or click to upload
        </button>
      ) : (
        <ul className="space-y-1.5">
          {items.map((a) => (
            <li key={a.id} className="group flex items-center gap-3 rounded-lg border border-slate-100 px-3 py-2">
              <FileText className="h-4 w-4 shrink-0 text-slate-400" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-slate-700">{a.fileName}</p>
                <p className="text-xs text-slate-400">
                  {formatSize(a.size)}{a.uploadedByName ? ` · ${a.uploadedByName}` : ''}
                </p>
              </div>
              <button type="button" onClick={() => download(a)} title="Download" className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"><Download className="h-4 w-4" /></button>
              <button type="button" onClick={() => remove(a)} title="Remove" className="rounded p-1.5 text-slate-400 opacity-0 transition-opacity hover:bg-red-50 hover:text-red-600 group-hover:opacity-100"><Trash2 className="h-4 w-4" /></button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
