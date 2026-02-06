import { 
  Sparkles, 
  Users, 
  BarChart3, 
  Workflow, 
  Mail, 
  Shield,
} from 'lucide-react';
import type { Feature, PricingTier } from './types';

export const FEATURES: Feature[] = [
  {
    id: 'ai-copy',
    title: 'AI-Powered Copy',
    description: 'Generate personalized sales emails, follow-ups, and messages in seconds.',
    icon: Sparkles,
    color: 'from-orange-500 to-amber-500',
  },
  {
    id: 'lead-management',
    title: 'Lead Management',
    description: 'Track and nurture leads through your entire sales funnel.',
    icon: Users,
    color: 'from-blue-500 to-cyan-500',
  },
  {
    id: 'analytics',
    title: 'Sales Analytics',
    description: 'Get insights into your pipeline and team performance.',
    icon: BarChart3,
    color: 'from-violet-500 to-purple-500',
  },
  {
    id: 'automation',
    title: 'Workflow Automation',
    description: 'Automate repetitive tasks and focus on closing deals.',
    icon: Workflow,
    color: 'from-emerald-500 to-teal-500',
  },
  {
    id: 'email-integration',
    title: 'Email Integration',
    description: 'Connect your email and track all communications.',
    icon: Mail,
    color: 'from-rose-500 to-pink-500',
  },
  {
    id: 'security',
    title: 'Enterprise Security',
    description: 'SOC 2 compliant with advanced security features.',
    icon: Shield,
    color: 'from-slate-600 to-slate-800',
  },
];

export const PRICING_TIERS: PricingTier[] = [
  {
    id: 'starter',
    name: 'Starter',
    price: '$29',
    description: 'Perfect for small teams',
    features: [
      'Up to 5 users',
      '1,000 leads',
      'Basic analytics',
      'Email support',
    ],
    cta: 'Start Free Trial',
  },
  {
    id: 'professional',
    name: 'Professional',
    price: '$79',
    description: 'For growing businesses',
    features: [
      'Up to 25 users',
      'Unlimited leads',
      'Advanced analytics',
      'AI copy generation',
      'Priority support',
    ],
    cta: 'Start Free Trial',
    popular: true,
  },
  {
    id: 'enterprise',
    name: 'Enterprise',
    price: 'Custom',
    description: 'For large organizations',
    features: [
      'Unlimited users',
      'Custom integrations',
      'Dedicated support',
      'SLA guarantee',
      'Custom training',
    ],
    cta: 'Contact Sales',
  },
];

export const HERO_STATS = [
  { value: '10K+', label: 'Active Users' },
  { value: '$2B+', label: 'Pipeline Managed' },
  { value: '98%', label: 'Customer Satisfaction' },
];
