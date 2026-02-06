import { useState, useEffect, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Copy, RotateCcw, Send, Mail, Linkedin, MessageSquare, Loader2, ShieldCheck, AlertTriangle, CheckCircle2, SendHorizontal } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { rewriteCopy, checkSpamScore, trackCopyEvent, type SpamCheckResult } from '@/app/api/copyGenerator';
import { sendEmail, getSmtpSettings } from '@/app/api/emailSender';

const DEFAULT_COPY = `Hi [First Name],

I hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

We've helped companies similar to yours:
• Increase productivity by 40%
• Reduce operational costs by 25%
• Streamline workflows and save time

Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

Looking forward to connecting!

Best regards,
[Your Name]`;

// Character limits for different platforms
const CHAR_LIMITS = {
  'linkedin-connect': 300,
  'linkedin-inmail': 1900,
  'sms': 160,
  'email': null, // No limit
};

type AdjustmentType = 'shorter' | 'friendlier' | 'persuasive';

function GeneratedCopy() {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as { 
    copy?: string; 
    copyTypeLabel?: string;
    copyTypeId?: string;
    subject?: string;
  } | null;
  
  const [generatedText, setGeneratedText] = useState(state?.copy ?? DEFAULT_COPY);
  const [subject, setSubject] = useState(state?.subject ?? '');
  const [copyTypeLabel, setCopyTypeLabel] = useState(state?.copyTypeLabel ?? 'Copy');
  const [copyTypeId, setCopyTypeId] = useState(state?.copyTypeId ?? 'sales-email');
  const [copied, setCopied] = useState(false);
  const [adjusting, setAdjusting] = useState<AdjustmentType | null>(null);
  const [spamCheck, setSpamCheck] = useState<SpamCheckResult | null>(null);
  const [checkingSpam, setCheckingSpam] = useState(false);
  const [showSendModal, setShowSendModal] = useState(false);
  const [recipientEmail, setRecipientEmail] = useState('');
  const [recipientName, setRecipientName] = useState('');
  const [sendingEmail, setSendingEmail] = useState(false);
  const [hasSmtpSettings, setHasSmtpSettings] = useState(false);
  const copyTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Check SMTP settings on load
  useEffect(() => {
    getSmtpSettings().then(settings => {
      setHasSmtpSettings(settings?.isValid ?? false);
    }).catch(() => {});
  }, []);

  // Determine character limit based on copy type
  const charLimit = CHAR_LIMITS[copyTypeId as keyof typeof CHAR_LIMITS] ?? null;
  const charCount = generatedText.length;
  const isOverLimit = charLimit !== null && charCount > charLimit;

  // Check if this is an email type (for subject line)
  const isEmailType = copyTypeId.includes('email');
  
  // Check if this is LinkedIn
  const isLinkedIn = copyTypeId.includes('linkedin');
  
  // Check if this is SMS
  const isSms = copyTypeId === 'sms';

  useEffect(() => {
    if (state?.copy) setGeneratedText(state.copy);
    if (state?.copyTypeLabel) setCopyTypeLabel(state.copyTypeLabel);
    if (state?.copyTypeId) setCopyTypeId(state.copyTypeId);
    if (state?.subject) setSubject(state.subject);
  }, [state?.copy, state?.copyTypeLabel, state?.copyTypeId, state?.subject]);

  useEffect(() => {
    return () => {
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
    };
  }, []);

  const handleCopy = () => {
    try {
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
      const textToCopy = subject ? `Subject: ${subject}\n\n${generatedText}` : generatedText;
      navigator.clipboard.writeText(textToCopy);
      setCopied(true);
      toast.success(messages.copy.copied);
      // Track copy event for analytics
      trackCopyEvent({ eventType: 'copy', copyTypeId }).catch(() => {});
      copyTimeoutRef.current = setTimeout(() => {
        copyTimeoutRef.current = null;
        setCopied(false);
      }, 3000);
    } catch {
      toast.error(messages.errors.generic);
    }
  };

  const handleCheckSpam = async () => {
    setCheckingSpam(true);
    try {
      const result = await checkSpamScore(subject, generatedText);
      setSpamCheck(result);
      if (result.rating === 'Good') {
        toast.success('Your copy looks great!');
      } else if (result.rating === 'Warning') {
        toast.info('Some improvements suggested');
      } else {
        toast.error('High spam risk detected');
      }
    } catch {
      toast.error('Failed to check spam score');
    } finally {
      setCheckingSpam(false);
    }
  };

  const handleRegenerate = () => {
    navigate('/dashboard', { state: { regenerateContext: generatedText.slice(0, 200) } });
  };

  const handleAdjust = async (adjustment: AdjustmentType) => {
    setAdjusting(adjustment);
    try {
      const result = await rewriteCopy(generatedText, adjustment);
      setGeneratedText(result);
      toast.success(`Copy adjusted: ${adjustment}`);
    } catch {
      toast.error('Failed to adjust copy');
    } finally {
      setAdjusting(null);
    }
  };

  const handleSendToCrm = () => {
    navigate('/send', { state: { copy: generatedText, copyTypeLabel, subject } });
  };

  // Gmail compose URL
  const handleOpenGmail = () => {
    const gmailUrl = new URL('https://mail.google.com/mail/');
    gmailUrl.searchParams.set('view', 'cm');
    gmailUrl.searchParams.set('fs', '1');
    if (subject) gmailUrl.searchParams.set('su', subject);
    gmailUrl.searchParams.set('body', generatedText);
    window.open(gmailUrl.toString(), '_blank');
    toast.success('Opening Gmail...');
  };

  // Outlook compose URL
  const handleOpenOutlook = () => {
    const outlookUrl = new URL('https://outlook.office.com/mail/deeplink/compose');
    if (subject) outlookUrl.searchParams.set('subject', subject);
    outlookUrl.searchParams.set('body', generatedText);
    window.open(outlookUrl.toString(), '_blank');
    toast.success('Opening Outlook...');
  };

  // LinkedIn message URL (limited - just opens LinkedIn messaging)
  const handleOpenLinkedIn = () => {
    window.open('https://www.linkedin.com/messaging/', '_blank');
    toast.info('Copy your message and paste it in LinkedIn');
  };

  // SMS URL (mobile only)
  const handleOpenSms = () => {
    const smsUrl = `sms:?body=${encodeURIComponent(generatedText)}`;
    window.open(smsUrl, '_self');
    toast.success('Opening SMS...');
  };

  // Send via SMTP directly
  const handleSendDirect = async () => {
    if (!recipientEmail) {
      toast.error('Please enter recipient email');
      return;
    }
    
    setSendingEmail(true);
    try {
      const result = await sendEmail({
        to: recipientEmail,
        toName: recipientName || undefined,
        subject: subject || 'Message from CRM',
        body: generatedText,
        copyTypeId,
      });
      
      if (result.success) {
        toast.success('Email sent successfully!');
        trackCopyEvent({ eventType: 'send', copyTypeId, recipientEmail }).catch(() => {});
        setShowSendModal(false);
        setRecipientEmail('');
        setRecipientName('');
      } else {
        toast.error(result.error || 'Failed to send email');
      }
    } catch {
      toast.error('Failed to send email');
    } finally {
      setSendingEmail(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Intelligent Sales Writer</h1>
            <p className="text-slate-600">Review and adjust your AI-generated sales content</p>
          </div>

          <div className="grid md:grid-cols-3 gap-6 lg:gap-8">
            <div className="md:col-span-2">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
                {/* Subject line for emails */}
                {isEmailType && (
                  <div className="mb-4">
                    <label htmlFor="subject-input" className="block text-sm font-medium text-slate-700 mb-2">
                      Subject Line
                    </label>
                    <input
                      id="subject-input"
                      type="text"
                      value={subject}
                      onChange={(e) => setSubject(e.target.value)}
                      placeholder="Enter email subject..."
                      className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                    />
                  </div>
                )}
                
                <div className="flex justify-between items-center mb-4">
                  <label htmlFor="copy-textarea" className="block text-sm font-medium text-slate-700">
                    Your Copy
                  </label>
                  <div className="flex items-center gap-4">
                    {/* Character count */}
                    {charLimit && (
                      <span className={`text-sm ${isOverLimit ? 'text-red-600 font-medium' : 'text-slate-500'}`}>
                        {charCount}/{charLimit}
                        {isOverLimit && ' (over limit)'}
                      </span>
                    )}
                    <button
                      type="button"
                      onClick={handleCopy}
                      className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors focus-visible:rounded-md"
                      aria-label={copied ? 'Copied to clipboard' : 'Copy to clipboard'}
                    >
                      <Copy className="w-4 h-4" aria-hidden />
                      {copied ? 'Copied!' : 'Copy'}
                    </button>
                  </div>
                </div>
                <textarea
                  id="copy-textarea"
                  value={generatedText}
                  onChange={(e) => setGeneratedText(e.target.value)}
                  rows={16}
                  className={`w-full px-4 py-3 border rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors resize-none font-mono text-sm ${
                    isOverLimit ? 'border-red-300 bg-red-50' : 'border-slate-300'
                  }`}
                  aria-label="Generated copy text"
                />
              </div>

              {/* Action buttons */}
              <div className="space-y-3">
                <button
                  type="button"
                  onClick={handleSendToCrm}
                  className="w-full h-12 bg-orange-600 hover:bg-orange-500 text-white font-semibold rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  <Send className="w-5 h-5" aria-hidden />
                  Send to CRM
                </button>

                {/* Email integration buttons */}
                {isEmailType && (
                  <>
                    <button
                      type="button"
                      onClick={() => setShowSendModal(true)}
                      className="w-full h-11 bg-green-600 hover:bg-green-500 text-white font-medium rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-500 focus-visible:ring-offset-2"
                    >
                      <SendHorizontal className="w-4 h-4" aria-hidden />
                      Send Email Directly
                    </button>
                    <div className="grid grid-cols-2 gap-3">
                      <button
                        type="button"
                        onClick={handleOpenGmail}
                        className="h-11 bg-white border-2 border-slate-200 hover:border-red-300 hover:bg-red-50 text-slate-700 hover:text-red-700 font-medium rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500 focus-visible:ring-offset-2"
                      >
                        <Mail className="w-4 h-4" aria-hidden />
                        Open in Gmail
                      </button>
                      <button
                        type="button"
                        onClick={handleOpenOutlook}
                        className="h-11 bg-white border-2 border-slate-200 hover:border-blue-300 hover:bg-blue-50 text-slate-700 hover:text-blue-700 font-medium rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2"
                      >
                        <Mail className="w-4 h-4" aria-hidden />
                        Open in Outlook
                      </button>
                    </div>
                  </>
                )}

                {/* LinkedIn button */}
                {isLinkedIn && (
                  <button
                    type="button"
                    onClick={handleOpenLinkedIn}
                    className="w-full h-11 bg-white border-2 border-slate-200 hover:border-blue-400 hover:bg-blue-50 text-slate-700 hover:text-blue-700 font-medium rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2"
                  >
                    <Linkedin className="w-4 h-4" aria-hidden />
                    Open LinkedIn Messaging
                  </button>
                )}

                {/* SMS button */}
                {isSms && (
                  <button
                    type="button"
                    onClick={handleOpenSms}
                    className="w-full h-11 bg-white border-2 border-slate-200 hover:border-green-300 hover:bg-green-50 text-slate-700 hover:text-green-700 font-medium rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-500 focus-visible:ring-offset-2"
                  >
                    <MessageSquare className="w-4 h-4" aria-hidden />
                    Open in Messages
                  </button>
                )}
              </div>
            </div>

            <div className="space-y-4">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                <h2 className="font-semibold text-slate-900 mb-1">Adjust Copy</h2>
                <p className="text-xs text-slate-500 mb-4">Intelligent adjustments to refine your message.</p>
                <div className="space-y-3">
                  {([
                    { label: 'Make shorter', adjustment: 'shorter' as const },
                    { label: 'Make friendlier', adjustment: 'friendlier' as const },
                    { label: 'Make more persuasive', adjustment: 'persuasive' as const },
                  ]).map(({ label, adjustment }) => (
                    <button
                      key={adjustment}
                      type="button"
                      onClick={() => handleAdjust(adjustment)}
                      disabled={adjusting !== null}
                      className="w-full py-3 px-4 border-2 border-slate-200 rounded-xl hover:border-orange-300 hover:bg-orange-50 transition-colors text-sm font-medium text-slate-700 hover:text-orange-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                    >
                      {adjusting === adjustment && <Loader2 className="w-4 h-4 animate-spin" aria-hidden />}
                      {label}
                    </button>
                  ))}
                  <button
                    type="button"
                    onClick={handleRegenerate}
                    disabled={adjusting !== null}
                    className="w-full py-3 px-4 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors text-sm font-medium text-slate-700 flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <RotateCcw className="w-4 h-4" aria-hidden />
                    Regenerate
                  </button>
                </div>
              </div>

              {/* Spam Score Check */}
              {isEmailType && (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                  <h2 className="font-semibold text-slate-900 mb-1">Spam Check</h2>
                  <p className="text-xs text-slate-500 mb-4">Check your copy for deliverability issues.</p>
                  
                  <button
                    type="button"
                    onClick={handleCheckSpam}
                    disabled={checkingSpam}
                    className="w-full py-3 px-4 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors text-sm font-medium text-slate-700 flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {checkingSpam ? (
                      <Loader2 className="w-4 h-4 animate-spin" aria-hidden />
                    ) : (
                      <ShieldCheck className="w-4 h-4" aria-hidden />
                    )}
                    {checkingSpam ? 'Checking...' : 'Check Spam Score'}
                  </button>

                  {spamCheck && (
                    <div className="mt-4 space-y-3">
                      {/* Score display */}
                      <div className={`p-3 rounded-lg flex items-center gap-3 ${
                        spamCheck.rating === 'Good' ? 'bg-green-50 border border-green-200' :
                        spamCheck.rating === 'Warning' ? 'bg-yellow-50 border border-yellow-200' :
                        'bg-red-50 border border-red-200'
                      }`}>
                        {spamCheck.rating === 'Good' ? (
                          <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0" />
                        ) : spamCheck.rating === 'Warning' ? (
                          <AlertTriangle className="w-5 h-5 text-yellow-600 flex-shrink-0" />
                        ) : (
                          <AlertTriangle className="w-5 h-5 text-red-600 flex-shrink-0" />
                        )}
                        <div>
                          <p className={`font-medium ${
                            spamCheck.rating === 'Good' ? 'text-green-800' :
                            spamCheck.rating === 'Warning' ? 'text-yellow-800' :
                            'text-red-800'
                          }`}>
                            {spamCheck.rating}
                          </p>
                          <p className="text-xs text-slate-600">
                            Score: {spamCheck.score}/100 (lower is better)
                          </p>
                        </div>
                      </div>

                      {/* Issues list */}
                      {spamCheck.issues.length > 0 && (
                        <div className="space-y-2">
                          <p className="text-xs font-medium text-slate-700">Issues found:</p>
                          {spamCheck.issues.map((issue, idx) => (
                            <div
                              key={idx}
                              className={`text-xs p-2 rounded ${
                                issue.severity === 'high' ? 'bg-red-50 text-red-700' :
                                issue.severity === 'medium' ? 'bg-yellow-50 text-yellow-700' :
                                'bg-slate-50 text-slate-600'
                              }`}
                            >
                              {issue.description}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )}

              {/* Tips based on copy type */}
              <div className="bg-sky-50 border border-sky-200 rounded-xl p-4">
                <p className="text-sm text-sky-800">
                  <strong>Tip:</strong>{' '}
                  {isLinkedIn && 'LinkedIn connection requests have a 300 character limit. Keep it concise!'}
                  {isSms && 'SMS messages are most effective under 160 characters to avoid splitting.'}
                  {isEmailType && 'Subject lines with 6-10 words have the highest open rates.'}
                  {!isLinkedIn && !isSms && !isEmailType && 'You can edit the text directly or use the adjustment buttons to refine your copy.'}
                </p>
              </div>

              {/* Copy type badge */}
              <div className="bg-white rounded-xl border border-slate-200 p-4">
                <p className="text-xs text-slate-500 mb-1">Copy Type</p>
                <p className="font-medium text-slate-900">{copyTypeLabel}</p>
              </div>
            </div>
          </div>
        </div>
      </main>

      {/* Send Email Modal */}
      {showSendModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <div className="bg-white rounded-2xl shadow-xl max-w-md w-full p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-4">Send Email Directly</h3>
            
            {!hasSmtpSettings && (
              <div className="mb-4 p-3 bg-yellow-50 border border-yellow-200 rounded-lg text-sm text-yellow-800">
                SMTP settings not configured. Please configure your email settings in Settings first.
              </div>
            )}
            
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Recipient Email *
                </label>
                <input
                  type="email"
                  value={recipientEmail}
                  onChange={(e) => setRecipientEmail(e.target.value)}
                  placeholder="recipient@example.com"
                  className="w-full px-4 py-2 border border-slate-300 rounded-lg focus:ring-2 focus:ring-green-500/20 focus:border-green-500 outline-none"
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Recipient Name (optional)
                </label>
                <input
                  type="text"
                  value={recipientName}
                  onChange={(e) => setRecipientName(e.target.value)}
                  placeholder="John Doe"
                  className="w-full px-4 py-2 border border-slate-300 rounded-lg focus:ring-2 focus:ring-green-500/20 focus:border-green-500 outline-none"
                />
              </div>
              
              <div className="p-3 bg-slate-50 rounded-lg">
                <p className="text-xs text-slate-500 mb-1">Subject:</p>
                <p className="text-sm text-slate-700">{subject || '(No subject)'}</p>
              </div>
            </div>
            
            <div className="flex gap-3 mt-6">
              <button
                type="button"
                onClick={() => setShowSendModal(false)}
                className="flex-1 py-2.5 px-4 border border-slate-300 text-slate-700 font-medium rounded-lg hover:bg-slate-50 transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleSendDirect}
                disabled={sendingEmail || !recipientEmail}
                className="flex-1 py-2.5 px-4 bg-green-600 hover:bg-green-500 text-white font-medium rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
              >
                {sendingEmail ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    Sending...
                  </>
                ) : (
                  <>
                    <SendHorizontal className="w-4 h-4" />
                    Send
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default GeneratedCopy;
