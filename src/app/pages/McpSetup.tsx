import { useEffect, useState } from 'react';
import { Copy, Eye, EyeOff, Key, RefreshCw, AlertTriangle, Bot, Check } from 'lucide-react';
import { toast } from 'sonner';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { getApiBaseUrl, isUsingRealApi } from '@/app/api/apiClient';
import { generateWebhookApiKey, getWebhookInfo, type WebhookInfo } from '@/app/api/webhook';
import { isOrgAdmin } from '@/app/api/organizations';
import { useOrg } from '@/app/contexts/OrgContext';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { buildMcpConfig, exampleQuestions, isReachableByClaude, mcpServerName, type McpClient } from './mcp/config';
import { usePageLoading } from '@/app/components/LoadingOverlay';

function CopyRow({
  label,
  value,
  onCopy,
}: {
  label: string;
  value: string;
  onCopy: () => void;
}) {
  return (
    <div className="space-y-1.5">
      <p className="text-sm font-medium text-slate-800">{label}</p>
      <div className="flex gap-2">
        <Input readOnly value={value} className="min-w-0 flex-1 font-mono text-sm bg-slate-50" aria-label={label} />
        <Button type="button" variant="outline" className="h-11 shrink-0" onClick={onCopy}>
          <Copy className="w-4 h-4 mr-2" aria-hidden="true" />
          Copy
        </Button>
      </div>
    </div>
  );
}

export function ClaudeSection() {
  const { currentOrg } = useOrg();
  const [info, setInfo] = useState<WebhookInfo | null>(null);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [generating, setGenerating] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const [regenerateConfirm, setRegenerateConfirm] = useState(false);
  const [client, setClient] = useState<McpClient>('claude');

  useEffect(() => {
    if (!isUsingRealApi() || !currentOrg?.id) {
      setInfo(null);
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    getWebhookInfo(currentOrg.id)
      .then((data) => {
        if (!cancelled) setInfo(data);
      })
      .catch(() => {
        if (!cancelled) {
          setInfo(null);
          toast.error('Could not load the organization API key.');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [currentOrg?.id]);

  const orgName = currentOrg?.name ?? 'Your organization';
  const serverName = mcpServerName(orgName);
  const base = getApiBaseUrl();
  const mcpUrl = base ? `${base}/mcp` : 'https://your-api.example.com/mcp';
  const apiKey = info?.apiKey ?? '';
  const configKey = apiKey || 'YOUR_API_KEY';
  const bearer = `Bearer ${configKey}`;
  const connectorName = `Cadence — ${orgName}`;
  const canGenerate = isOrgAdmin(currentOrg);
  const claudeCanReach = isReachableByClaude(mcpUrl);
  const questions = exampleQuestions(orgName);

  const copy = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success(`${label} copied`);
    } catch {
      toast.error('Could not copy');
    }
  };

  const handleGenerate = async () => {
    if (!currentOrg?.id) return;
    setGenerating(true);
    try {
      await generateWebhookApiKey(currentOrg.id);
      const fresh = await getWebhookInfo(currentOrg.id);
      setInfo(fresh);
      setShowApiKey(true);
      toast.success(info?.hasApiKey ? 'API key regenerated' : 'API key generated');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not generate an API key');
    } finally {
      setGenerating(false);
      setRegenerateConfirm(false);
    }
  };

  const mask = (key: string) => {
    if (key.length <= 12) return key;
    return `${key.slice(0, 7)}${'*'.repeat(8)}${key.slice(-4)}`;
  };

  return (
    <div className="p-6 space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Bot className="w-5 h-5 text-orange-600" aria-hidden="true" />
          Ask Claude about {orgName}
        </h2>
        <p className="text-slate-600 mt-2 text-sm leading-relaxed">
          Connect this organization once. After that, ask questions in a normal Claude chat. Claude can read leads, contacts, companies, deals, and tasks. It cannot add, edit, or delete them.
        </p>
      </div>

          {!claudeCanReach && (
            <div className="mb-6 flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4" role="status">
              <AlertTriangle className="w-5 h-5 text-amber-600 mt-0.5 shrink-0" aria-hidden="true" />
              <div>
                <p className="font-medium text-amber-950">Claude cannot reach this address yet</p>
                <p className="text-sm text-amber-900 mt-1 leading-relaxed">
                  Claude calls the CRM from the internet. <span className="font-mono">{mcpUrl}</span> only works on this computer. Use a public <span className="font-mono">https://</span> address before you save the connector.
                </p>
              </div>
            </div>
          )}

          {loading ? (
            <div className="py-16 flex justify-center">
              <LoadingSpinner size="lg" />
            </div>
          ) : (
            <Tabs value={client} onValueChange={(value) => setClient(value as McpClient)}>
              <TabsList className="mb-6 h-11" aria-label="Which app are you connecting?">
                <TabsTrigger value="claude" className="h-9 px-4">Claude</TabsTrigger>
                <TabsTrigger value="cursor" className="h-9 px-4">Cursor</TabsTrigger>
                <TabsTrigger value="vscode" className="h-9 px-4">VS Code</TabsTrigger>
              </TabsList>

              <TabsContent value="claude" className="space-y-6">
                <ol className="space-y-6">
                  <li className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
                    <h2 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
                      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-orange-600 text-sm font-bold text-white">1</span>
                      <Key className="w-5 h-5 text-orange-600" aria-hidden="true" />
                      Create the key for {orgName}
                    </h2>
                    <p className="text-sm text-slate-600 mt-3 leading-relaxed">
                      This key opens only {orgName}. It is the same key as the lead webhook. Regenerating it disconnects Claude and the webhook until you paste the new value.
                    </p>
                    <div className="mt-4">
                      <KeyPanel
                        apiKey={apiKey}
                        showApiKey={showApiKey}
                        setShowApiKey={setShowApiKey}
                        canGenerate={canGenerate}
                        generating={generating}
                        masked={apiKey ? mask(apiKey) : ''}
                        onGenerate={handleGenerate}
                        onCopy={() => copy(apiKey, 'API key')}
                        onRegenerate={() => setRegenerateConfirm(true)}
                      />
                    </div>
                  </li>

                  <li className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
                    <h2 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
                      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-orange-600 text-sm font-bold text-white">2</span>
                      Paste these values into Claude
                    </h2>
                    <ol className="mt-4 space-y-4 text-sm text-slate-700 leading-relaxed list-decimal pl-5">
                      <li>In Claude, open <strong>Customize</strong>, then <strong>Connectors</strong>, then <strong>Add custom connector</strong>.</li>
                      <li>Paste the connector name, the address, and the header below. The header name is <span className="font-mono">Authorization</span>.</li>
                      <li>Save the connector. Claude will use this key for {orgName} only.</li>
                    </ol>
                    <div className="mt-5 space-y-4">
                      <CopyRow label="Connector name" value={connectorName} onCopy={() => copy(connectorName, 'Connector name')} />
                      <CopyRow label="Server address" value={mcpUrl} onCopy={() => copy(mcpUrl, 'Server address')} />
                      <CopyRow label="Header name" value="Authorization" onCopy={() => copy('Authorization', 'Header name')} />
                      <CopyRow label="Header value" value={bearer} onCopy={() => copy(bearer, 'Header value')} />
                    </div>
                  </li>

                  <li className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
                    <h2 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
                      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-orange-600 text-sm font-bold text-white">3</span>
                      Ask in a normal Claude chat
                    </h2>
                    <p className="text-sm text-slate-600 mt-3 leading-relaxed">
                      Copy a question and paste it into Claude. You do not need to open the CRM for the answer.
                    </p>
                    <ul className="mt-4 space-y-2">
                      {questions.map((question) => (
                        <li key={question}>
                          <button
                            type="button"
                            onClick={() => copy(question, 'Question')}
                            className="flex w-full items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-left text-sm text-slate-800 hover:border-orange-300 hover:bg-orange-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500"
                          >
                            <span>{question}</span>
                            <Copy className="w-4 h-4 shrink-0 text-slate-400" aria-hidden="true" />
                          </button>
                        </li>
                      ))}
                    </ul>
                  </li>
                </ol>

                <section className="grid gap-4 sm:grid-cols-2" aria-label="What Claude can and cannot do">
                  <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
                    <h2 className="font-semibold text-emerald-950 flex items-center gap-2">
                      <Check className="w-4 h-4" aria-hidden="true" />
                      Claude can read
                    </h2>
                    <ul className="mt-3 space-y-1 text-sm text-emerald-900">
                      <li>Leads, including one lead by name</li>
                      <li>Contacts and companies</li>
                      <li>Deals and tasks</li>
                      <li>A short snapshot of lead and task counts</li>
                    </ul>
                  </div>
                  <div className="rounded-2xl border border-slate-200 bg-white p-5">
                    <h2 className="font-semibold text-slate-900">Claude cannot change records</h2>
                    <p className="mt-3 text-sm text-slate-600 leading-relaxed">
                      It will not create, edit, or delete anything in {orgName}. Another organization’s key cannot see these records.
                    </p>
                  </div>
                </section>
              </TabsContent>

              <TabsContent value="cursor" className="space-y-4">
                <FileGuide
                  title="Add Cadence to every Cursor project on this computer"
                  steps={[
                    'Create the organization key in the Claude tab if you have not already.',
                    'On Windows, open %USERPROFILE%\\.cursor\\mcp.json. On Mac or Linux, open ~/.cursor/mcp.json.',
                    'Paste the block below. If the file already has other servers, add this name inside mcpServers.',
                    'Reload Cursor. Then ask it to list leads in ' + orgName + '.',
                  ]}
                  config={buildMcpConfig('cursor', { serverName, url: mcpUrl, apiKey: configKey })}
                  onCopy={() => copy(buildMcpConfig('cursor', { serverName, url: mcpUrl, apiKey: configKey }), 'Cursor config')}
                />
              </TabsContent>

              <TabsContent value="vscode" className="space-y-4">
                <FileGuide
                  title="Add Cadence in VS Code"
                  steps={[
                    'Create the organization key in the Claude tab if you have not already.',
                    'Open .vscode/mcp.json in the project, or your user MCP settings.',
                    'Paste the block below and reload VS Code.',
                    'Ask it about leads, deals, or tasks in ' + orgName + '.',
                  ]}
                  config={buildMcpConfig('vscode', { serverName, url: mcpUrl, apiKey: configKey })}
                  onCopy={() => copy(buildMcpConfig('vscode', { serverName, url: mcpUrl, apiKey: configKey }), 'VS Code config')}
                />
              </TabsContent>
            </Tabs>
          )}

      <AlertDialog open={regenerateConfirm} onOpenChange={setRegenerateConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Regenerate the organization API key?</AlertDialogTitle>
            <AlertDialogDescription>
              The current key stops working for the lead webhook and for Claude until you paste the new header value.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleGenerate} disabled={generating}>
              Regenerate
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function KeyPanel({
  apiKey,
  showApiKey,
  setShowApiKey,
  canGenerate,
  generating,
  masked,
  onGenerate,
  onCopy,
  onRegenerate,
}: {
  apiKey: string;
  showApiKey: boolean;
  setShowApiKey: (value: boolean | ((value: boolean) => boolean)) => void;
  canGenerate: boolean;
  generating: boolean;
  masked: string;
  onGenerate: () => void;
  onCopy: () => void;
  onRegenerate: () => void;
}) {
  if (!isUsingRealApi()) {
    return (
      <p className="text-sm text-slate-600">
        Connect the real API (<code className="text-xs bg-slate-100 px-1 rounded">VITE_API_URL</code>) to generate a key. Until then, step 2 shows a placeholder instead of a real key.
      </p>
    );
  }
  if (!apiKey) {
    return canGenerate ? (
      <Button type="button" onClick={onGenerate} disabled={generating} className="h-11 bg-orange-600 hover:bg-orange-500">
        {generating ? 'Generating...' : 'Generate API key'}
      </Button>
    ) : (
      <p className="text-sm text-slate-600">Ask an owner or manager to generate the key.</p>
    );
  }
  return (
    <div className="space-y-3">
      <div className="flex flex-col gap-2 sm:flex-row">
        <Input readOnly value={showApiKey ? apiKey : masked} className="font-mono text-sm bg-slate-50" aria-label="Organization API key" />
        <div className="flex gap-2">
          <Button type="button" variant="outline" className="h-11" onClick={() => setShowApiKey((value) => !value)} aria-label={showApiKey ? 'Hide API key' : 'Show API key'}>
            {showApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
          </Button>
          <Button type="button" variant="outline" className="h-11" onClick={onCopy}>
            <Copy className="w-4 h-4 mr-2" aria-hidden="true" />
            Copy key
          </Button>
        </div>
      </div>
      {canGenerate && (
        <Button type="button" variant="outline" onClick={onRegenerate} className="text-orange-600 border-orange-200 hover:bg-orange-50">
          <RefreshCw className="w-4 h-4 mr-2" aria-hidden="true" />
          Regenerate key
        </Button>
      )}
    </div>
  );
}

function FileGuide({
  title,
  steps,
  config,
  onCopy,
}: {
  title: string;
  steps: string[];
  config: string;
  onCopy: () => void;
}) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
      <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
      <ol className="mt-4 space-y-2 text-sm text-slate-700 leading-relaxed list-decimal pl-5">
        {steps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>
      <pre className="mt-4 text-xs bg-slate-900 text-slate-100 p-4 rounded-xl overflow-x-auto">{config}</pre>
      <Button type="button" variant="outline" className="mt-3 h-11" onClick={onCopy}>
        <Copy className="w-4 h-4 mr-2" aria-hidden="true" />
        Copy config
      </Button>
      <p className="mt-3 text-sm text-slate-500">
        Do not commit the key. In a shared file, replace it with <code className="bg-slate-100 px-1 rounded">{'${env:CADENCE_API_KEY}'}</code> and set that variable on each computer.
      </p>
    </section>
  );
}
