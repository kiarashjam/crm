import { Users, Mail, Phone, Sparkles, Tag, Building2 } from 'lucide-react';

export const FIELD_INFO: Record<string, { label: string; icon: React.ReactNode; required: boolean; color: string }> = {
  name: { label: 'Full Name', icon: <Users className="w-3.5 h-3.5" />, required: true, color: 'orange' },
  email: { label: 'Email Address', icon: <Mail className="w-3.5 h-3.5" />, required: true, color: 'blue' },
  phone: { label: 'Phone Number', icon: <Phone className="w-3.5 h-3.5" />, required: false, color: 'emerald' },
  source: { label: 'Lead Source', icon: <Sparkles className="w-3.5 h-3.5" />, required: false, color: 'amber' },
  status: { label: 'Lead Status', icon: <Tag className="w-3.5 h-3.5" />, required: false, color: 'cyan' },
  company: { label: 'Company', icon: <Building2 className="w-3.5 h-3.5" />, required: false, color: 'purple' },
};

export const SAMPLE_CSV_CONTENT = `name,email,phone,company,source,status
John Smith,john@example.com,+1 555-123-4567,Acme Corp,Website,New
Jane Doe,jane@example.com,+1 555-987-6543,Tech Solutions,Referral,Contacted
Bob Wilson,bob@example.com,,StartupXYZ,LinkedIn,Qualified`;
