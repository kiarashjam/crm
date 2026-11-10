import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Check, Minus } from 'lucide-react';
import { Label } from '@/app/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  setMemberPermissions,
  type OrgMemberDto,
  type PermissionCatalogue,
} from '@/app/api/organizations';

interface MemberPermissionsEditorProps {
  organizationId: string;
  member: OrgMemberDto;
  catalogue: PermissionCatalogue;
  /** What the person editing may do: they can give no more, nor change someone who can do more. */
  mine: ReadonlySet<string>;
  /** Only the owner sets single permissions; everyone else who manages the team picks presets. */
  isOwner: boolean;
  onChanged: (member: OrgMemberDto) => void;
}

/**
 * What a member may do, on the Team page: the preset they start from and, for the
 * owner, each permission on its own. The server checks all of it again; this only
 * keeps out of reach what it would refuse.
 */
export function MemberPermissionsEditor({ organizationId, member, catalogue, mine, isOwner, onChanged }: MemberPermissionsEditorProps) {
  const [saving, setSaving] = useState(false);
  const held = useMemo(() => new Set(member.permissions ?? []), [member.permissions]);
  const presets = catalogue.presets.filter((p) => p.assignable);
  const current = presets.find((p) => p.key === member.preset);
  const withinMine = (permissions: Iterable<string>) => isOwner || [...permissions].every((p) => mine.has(p));
  // Someone who can do more than the editor is left to those who can do as much.
  const editable = withinMine(held);

  const save = async (preset: string, permissions?: string[]) => {
    setSaving(true);
    try {
      const updated = await setMemberPermissions(organizationId, member.userId, preset, permissions);
      onChanged({ ...member, ...updated });
      const label = presets.find((p) => p.key === updated.preset)?.label ?? preset;
      toast.success(permissions ? `Changed what ${member.name} may do` : `${member.name} is now ${label}`);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Could not change what they may do';
      toast.error(message.replace(/^HTTP \d+: /, ''));
    } finally {
      setSaving(false);
    }
  };

  const toggle = (key: string) => {
    const next = new Set(held);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    const preset = current?.key ?? presets[0]?.key;
    if (!preset) return;
    void save(preset, catalogue.permissions.map((p) => p.key).filter((k) => next.has(k)));
  };

  return (
    <div className="space-y-4">
      <div>
        <Label htmlFor="member-preset" className="mb-2 block text-sm text-slate-600">Preset</Label>
        <Select value={current?.key ?? ''} onValueChange={(v) => void save(v)} disabled={saving || !editable}>
          <SelectTrigger id="member-preset" aria-label="Preset" className="h-10">
            <SelectValue placeholder="Choose a preset" />
          </SelectTrigger>
          <SelectContent>
            {presets.map((p) => (
              <SelectItem key={p.key} value={p.key} disabled={!withinMine(p.permissions)}>
                {p.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {current && <p className="mt-1.5 text-xs text-slate-500">{current.description}</p>}
        {member.customised && (
          <p className="mt-1 text-xs font-medium text-amber-700">Changed from the preset: what they may do is listed below.</p>
        )}
        {!editable && (
          <p className="mt-1 text-xs text-slate-500">
            They can do more than you, so only someone who can do as much may change them.
          </p>
        )}
      </div>

      <fieldset>
        <legend className="mb-2 text-sm text-slate-600">What they may do</legend>
        <ul className="space-y-1">
          {catalogue.permissions.map((p) => {
            const on = held.has(p.key);
            return (
              <li key={p.key}>
                {isOwner ? (
                  <label className="flex cursor-pointer items-start gap-2.5 rounded-lg p-2 hover:bg-slate-50">
                    <input
                      type="checkbox"
                      checked={on}
                      disabled={saving}
                      onChange={() => toggle(p.key)}
                      className="mt-0.5 h-4 w-4 rounded border-slate-300 accent-violet-600"
                    />
                    <span>
                      <span className="block text-sm font-medium text-slate-800">{p.label}</span>
                      <span className="block text-xs text-slate-500">{p.description}</span>
                    </span>
                  </label>
                ) : (
                  <div className={`flex items-start gap-2.5 p-2 ${on ? '' : 'opacity-50'}`}>
                    {on
                      ? <Check className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-label="Allowed" />
                      : <Minus className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-label="Not allowed" />}
                    <span>
                      <span className="block text-sm font-medium text-slate-800">{p.label}</span>
                      <span className="block text-xs text-slate-500">{p.description}</span>
                    </span>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
        {!isOwner && <p className="mt-2 text-xs text-slate-500">Only the owner changes single permissions.</p>}
      </fieldset>
    </div>
  );
}

/** The role that goes with a preset, which the server also sets: for older clients that read it. */
export function roleForPreset(preset: string): number {
  if (preset === 'manager') return 2;
  if (preset === 'read_only_accountant') return 3;
  return 1;
}
