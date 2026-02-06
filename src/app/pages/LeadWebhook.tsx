import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Copy, Eye, EyeOff, Key, Code, CheckCircle, Link2, Globe, Mail, ShoppingCart, Smartphone, Zap, BarChart3, RefreshCw, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getWebhookInfo, generateWebhookApiKey, type WebhookInfo } from '@/app/api/webhook';
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

const CODE_EXAMPLES = {
  curl: `curl -X POST \\
  https://your-domain.com/api/webhook/leads \\
  -H "X-Api-Key: YOUR_API_KEY" \\
  -H "Content-Type: application/json" \\
  -d '{
    "name": "Jane Smith",
    "email": "jane@company.com",
    "phone": "+1-555-0123",
    "companyName": "TechCorp",
    "source": "website-form"
  }'`,
  javascript: `fetch('https://your-domain.com/api/webhook/leads', {
  method: 'POST',
  headers: {
    'X-Api-Key': 'YOUR_API_KEY',
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    name: 'Jane Smith',
    email: 'jane@company.com',
    phone: '+1-555-0123',
    companyName: 'TechCorp',
    source: 'website-form',
  }),
})
  .then(response => response.json())
  .then(data => console.log('Lead created:', data))
  .catch(error => console.error('Error:', error));`,
  python: `import requests

url = 'https://your-domain.com/api/webhook/leads'
headers = {
    'X-Api-Key': 'YOUR_API_KEY',
    'Content-Type': 'application/json',
}
data = {
    'name': 'Jane Smith',
    'email': 'jane@company.com',
    'phone': '+1-555-0123',
    'companyName': 'TechCorp',
    'source': 'website-form',
}

response = requests.post(url, json=data, headers=headers)
print(response.json())`,
  php: `<?php
$url = 'https://your-domain.com/api/webhook/leads';
$data = [
    'name' => 'Jane Smith',
    'email' => 'jane@company.com',
    'phone' => '+1-555-0123',
    'companyName' => 'TechCorp',
    'source' => 'website-form',
];

$ch = curl_init($url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($data));
curl_setopt($ch, CURLOPT_HTTPHEADER, [
    'X-Api-Key: YOUR_API_KEY',
    'Content-Type: application/json',
]);

$response = curl_exec($ch);
curl_close($ch);
echo $response;
?>`,
};

export default function LeadWebhook() {
  const navigate = useNavigate();
  const { currentOrg } = useOrg();
  const [webhookInfo, setWebhookInfo] = useState<WebhookInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const [regenerateConfirm, setRegenerateConfirm] = useState(false);
  const [selectedLanguage, setSelectedLanguage] = useState<keyof typeof CODE_EXAMPLES>('curl');

  useEffect(() => {
    if (!currentOrg?.id) {
      navigate('/leads');
      return;
    }
    loadWebhookInfo();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentOrg?.id]);

  const loadWebhookInfo = async () => {
    if (!currentOrg?.id) return;
    setLoading(true);
    try {
      const info = await getWebhookInfo(currentOrg.id);
      setWebhookInfo(info);
    } catch (error) {
      toast.error('Failed to load webhook information');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateKey = async () => {
    if (!currentOrg?.id) return;
    setGenerating(true);
    try {
      await generateWebhookApiKey(currentOrg.id);
      await loadWebhookInfo();
      setShowApiKey(true);
      toast.success('API key generated successfully');
    } catch (error) {
      toast.error('Failed to generate API key');
      console.error(error);
    } finally {
      setGenerating(false);
      setRegenerateConfirm(false);
    }
  };

  const copyToClipboard = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success(`${label} copied to clipboard!`);
    } catch (error) {
      toast.error('Failed to copy to clipboard');
      console.error('Clipboard error:', error);
    }
  };

  const maskApiKey = (key: string | null): string => {
    if (!key) return '';
    if (key.length <= 12) return key;
    return `${key.substring(0, 7)}${'*'.repeat(Math.max(8, key.length - 15))}${key.substring(key.length - 4)}`;
  };

  const formatDate = (dateStr: string | null): string => {
    if (!dateStr) return '';
    try {
      return new Date(dateStr).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      });
    } catch {
      return '';
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)] flex items-center justify-center" tabIndex={-1}>
          <LoadingSpinner size="lg" />
        </main>
      </div>
    );
  }

  const apiKey = webhookInfo?.apiKey || null;
  const maskedKey = maskApiKey(apiKey);

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <Link
          to="/leads"
          className="inline-flex items-center gap-2 text-sm text-slate-600 hover:text-orange-600 mb-6 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Back to Leads
        </Link>

        {/* Hero Section */}
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-orange-500 to-orange-600 flex items-center justify-center text-white shadow-lg">
              <Link2 className="w-6 h-6" />
            </div>
            <div>
              <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Lead Webhook Integration</h1>
              <p className="text-slate-600 mt-1">Automatically capture leads from external sources</p>
            </div>
          </div>
        </div>

        {/* Webhook URL Section */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
          <h2 className="text-lg font-semibold text-slate-900 mb-4">Webhook Endpoint</h2>
          <div className="flex gap-2">
            <Input
              readOnly
              value={webhookInfo?.webhookUrl || ''}
              className="font-mono text-sm bg-slate-50 flex-1"
              aria-label="Webhook endpoint URL"
            />
            <Button
              variant="outline"
              size="icon"
              onClick={() => webhookInfo?.webhookUrl && copyToClipboard(webhookInfo.webhookUrl, 'Webhook URL')}
              aria-label="Copy webhook URL"
              title="Copy webhook URL"
            >
              <Copy className="w-4 h-4" />
            </Button>
          </div>
          <p className="text-sm text-slate-600 mt-2">POST requests to this URL will create leads</p>
        </div>

        {/* API Key Section */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
          <h2 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2">
            <Key className="w-5 h-5 text-orange-600" />
            API Key
          </h2>
          {apiKey ? (
            <>
              <div className="flex gap-2 mb-4">
                <Input
                  readOnly
                  type="text"
                  value={showApiKey ? apiKey : maskedKey}
                  className="font-mono text-sm bg-slate-50"
                />
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => setShowApiKey(!showApiKey)}
                  aria-label={showApiKey ? 'Hide API key' : 'Show API key'}
                >
                  {showApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </Button>
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => apiKey && copyToClipboard(apiKey, 'API Key')}
                  aria-label="Copy API key"
                >
                  <Copy className="w-4 h-4" />
                </Button>
              </div>
              {webhookInfo?.apiKeyCreatedAt && (
                <p className="text-sm text-slate-600 mb-4">Created: {formatDate(webhookInfo.apiKeyCreatedAt)}</p>
              )}
              <Button
                variant="outline"
                onClick={() => setRegenerateConfirm(true)}
                className="text-orange-600 border-orange-200 hover:bg-orange-50"
              >
                <RefreshCw className="w-4 h-4 mr-2" />
                Regenerate Key
              </Button>
              <div className="mt-3 p-3 bg-amber-50 border border-amber-200 rounded-lg">
                <div className="flex items-start gap-2">
                  <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 shrink-0" />
                  <p className="text-sm text-amber-800">Regenerating will invalidate the old key. Make sure to update any integrations using it.</p>
                </div>
              </div>
            </>
          ) : (
            <div>
              <p className="text-slate-600 mb-4">No API key generated yet. Generate one to start using the webhook.</p>
              <Button onClick={handleGenerateKey} disabled={generating} className="bg-orange-600 hover:bg-orange-500">
                {generating ? 'Generating...' : 'Generate API Key'}
              </Button>
            </div>
          )}
        </div>

        {/* Step-by-Step Guide */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
          <h2 className="text-lg font-semibold text-slate-900 mb-6 flex items-center gap-2">
            <span className="text-2xl">🚀</span>
            Get Started in 3 Steps
          </h2>

          {/* Progress Indicator */}
          <div className="flex items-center justify-center mb-8">
            <div className="flex items-center gap-2 sm:gap-4 flex-wrap justify-center">
              <div className="flex flex-col items-center">
                <div className="w-10 h-10 sm:w-12 sm:h-12 rounded-full bg-gradient-to-br from-orange-500 to-orange-600 flex items-center justify-center text-white font-bold shadow-lg">
                  1
                </div>
                <span className="text-xs text-slate-600 mt-2 text-center">Copy Key</span>
              </div>
              <div className="w-8 sm:w-16 h-0.5 bg-slate-300"></div>
              <div className="flex flex-col items-center">
                <div className="w-10 h-10 sm:w-12 sm:h-12 rounded-full bg-gradient-to-br from-orange-500 to-orange-600 flex items-center justify-center text-white font-bold shadow-lg">
                  2
                </div>
                <span className="text-xs text-slate-600 mt-2 text-center">Build Request</span>
              </div>
              <div className="w-8 sm:w-16 h-0.5 bg-slate-300"></div>
              <div className="flex flex-col items-center">
                <div className="w-10 h-10 sm:w-12 sm:h-12 rounded-full bg-gradient-to-br from-orange-500 to-orange-600 flex items-center justify-center text-white font-bold shadow-lg">
                  3
                </div>
                <span className="text-xs text-slate-600 mt-2 text-center">Send & Done</span>
              </div>
            </div>
          </div>

          {/* Step 1 */}
          <div className="mb-6 pb-6 border-b border-slate-200">
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center shrink-0">
                <Key className="w-5 h-5 text-orange-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-slate-900 mb-2">Step 1: Copy your API Key</h3>
                <p className="text-sm text-slate-600 mb-3">Scroll up and copy your unique API key. Keep it secure - treat it like a password.</p>
                <div className="bg-slate-900 text-slate-100 p-3 rounded-lg font-mono text-xs">
                  X-Api-Key: {maskedKey || 'your-api-key'}
                </div>
              </div>
            </div>
          </div>

          {/* Step 2 */}
          <div className="mb-6 pb-6 border-b border-slate-200">
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center shrink-0">
                <Code className="w-5 h-5 text-orange-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-slate-900 mb-2">Step 2: Build your request</h3>
                <p className="text-sm text-slate-600 mb-3">Send lead data as JSON to our endpoint.</p>
                <Tabs value={selectedLanguage} onValueChange={(v) => setSelectedLanguage(v as keyof typeof CODE_EXAMPLES)}>
                  <TabsList className="mb-3">
                    <TabsTrigger value="curl">cURL</TabsTrigger>
                    <TabsTrigger value="javascript">JavaScript</TabsTrigger>
                    <TabsTrigger value="python">Python</TabsTrigger>
                    <TabsTrigger value="php">PHP</TabsTrigger>
                  </TabsList>
                  <TabsContent value={selectedLanguage}>
                    <div className="relative">
                      <pre className="bg-slate-900 text-slate-100 p-4 rounded-lg overflow-x-auto text-xs font-mono">
                        <code>{CODE_EXAMPLES[selectedLanguage]}</code>
                      </pre>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="absolute top-2 right-2 text-slate-400 hover:text-slate-100 bg-slate-800/50 hover:bg-slate-700/50"
                        onClick={() => copyToClipboard(CODE_EXAMPLES[selectedLanguage], 'Code example')}
                        aria-label="Copy code example"
                        title="Copy code example"
                      >
                        <Copy className="w-4 h-4" />
                      </Button>
                    </div>
                  </TabsContent>
                </Tabs>
              </div>
            </div>
          </div>

          {/* Step 3 */}
          <div>
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center shrink-0">
                <CheckCircle className="w-5 h-5 text-orange-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-slate-900 mb-2">Step 3: Check the response</h3>
                <div className="space-y-3">
                  <div className="bg-emerald-50 border border-emerald-200 rounded-lg p-3">
                    <div className="flex items-center gap-2 mb-2">
                      <div className="w-2 h-2 rounded-full bg-emerald-500"></div>
                      <span className="font-semibold text-emerald-900">201 Created</span>
                    </div>
                    <pre className="text-xs font-mono text-emerald-800 bg-emerald-100 p-2 rounded">{`{
  "id": "abc-123-...",
  "name": "Jane Smith",
  "email": "jane@company.com",
  "status": "New",
  "source": "webhook"
}`}</pre>
                  </div>
                  <div className="bg-red-50 border border-red-200 rounded-lg p-3">
                    <div className="flex items-center gap-2 mb-2">
                      <div className="w-2 h-2 rounded-full bg-red-500"></div>
                      <span className="font-semibold text-red-900">401 - Invalid or missing API key</span>
                    </div>
                  </div>
                  <div className="bg-amber-50 border border-amber-200 rounded-lg p-3">
                    <div className="flex items-center gap-2 mb-2">
                      <div className="w-2 h-2 rounded-full bg-amber-500"></div>
                      <span className="font-semibold text-amber-900">400 - Missing name or email</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Field Reference */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
          <h2 className="text-lg font-semibold text-slate-900 mb-4">📋 Field Reference</h2>
          <div className="grid md:grid-cols-2 gap-4">
            <div className="border border-slate-200 rounded-lg p-4">
              <h3 className="font-semibold text-slate-900 mb-3">Required</h3>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="font-mono text-slate-700">name</span>
                  <span className="text-slate-600">string</span>
                </div>
                <p className="text-slate-600 text-xs">Lead's full name</p>
                <div className="flex justify-between mt-3">
                  <span className="font-mono text-slate-700">email</span>
                  <span className="text-slate-600">string</span>
                </div>
                <p className="text-slate-600 text-xs">Valid email address</p>
              </div>
            </div>
            <div className="border border-slate-200 rounded-lg p-4">
              <h3 className="font-semibold text-slate-900 mb-3">Optional</h3>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="font-mono text-slate-700">phone</span>
                  <span className="text-slate-600">string</span>
                </div>
                <p className="text-slate-600 text-xs">Phone number</p>
                <div className="flex justify-between mt-3">
                  <span className="font-mono text-slate-700">companyName</span>
                  <span className="text-slate-600">string</span>
                </div>
                <p className="text-slate-600 text-xs">Company/organization</p>
                <div className="flex justify-between mt-3">
                  <span className="font-mono text-slate-700">source</span>
                  <span className="text-slate-600">string</span>
                </div>
                <p className="text-slate-600 text-xs">Where lead came from (defaults to "webhook")</p>
              </div>
            </div>
          </div>
        </div>

        {/* Integration Ideas */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
          <h2 className="text-lg font-semibold text-slate-900 mb-4">💡 Integration Ideas</h2>
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            {[
              { icon: Globe, label: 'Website Forms', color: 'blue' },
              { icon: Mail, label: 'Email Marketing', color: 'purple' },
              { icon: ShoppingCart, label: 'E-commerce Checkout', color: 'green' },
              { icon: Smartphone, label: 'Mobile Apps', color: 'indigo' },
              { icon: Zap, label: 'Zapier/Make', color: 'yellow' },
              { icon: BarChart3, label: 'Ad Platforms', color: 'pink' },
            ].map((item) => (
              <div
                key={item.label}
                className="flex flex-col items-center p-4 border border-slate-200 rounded-xl hover:border-orange-200 hover:shadow-md transition-all cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                tabIndex={0}
                role="button"
                aria-label={`Integration idea: ${item.label}`}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                  }
                }}
              >
                <item.icon className={`w-8 h-8 text-slate-600 mb-2`} />
                <span className="text-sm font-medium text-slate-700 text-center">{item.label}</span>
              </div>
            ))}
          </div>
          <p className="text-sm text-slate-600 mt-4 text-center">Connect any system that can make HTTP requests!</p>
        </div>

        {/* Regenerate Confirmation Dialog */}
        <AlertDialog open={regenerateConfirm} onOpenChange={setRegenerateConfirm}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Regenerate API Key?</AlertDialogTitle>
              <AlertDialogDescription>
                This will invalidate your current API key and generate a new one. Any integrations using the old key will stop working until you update them with the new key.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleGenerateKey}
                disabled={generating}
                className="bg-orange-600 hover:bg-orange-500"
              >
                {generating ? 'Regenerating...' : 'Regenerate'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </main>
    </div>
  );
}
