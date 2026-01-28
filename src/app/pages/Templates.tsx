import { Link, useNavigate } from 'react-router-dom';
import { Mail, MessageSquare, Calendar, Handshake, RotateCcw } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';

export default function Templates() {
  const navigate = useNavigate();

  const templates = [
    {
      id: '1',
      icon: Mail,
      title: 'First contact email',
      description: 'Initial outreach to new prospects',
      category: 'Sales',
      useCount: 245,
    },
    {
      id: '2',
      icon: MessageSquare,
      title: 'Follow-up after meeting',
      description: 'Thank you and next steps',
      category: 'Follow-up',
      useCount: 189,
    },
    {
      id: '3',
      icon: Calendar,
      title: 'Demo reminder',
      description: 'Confirm upcoming product demo',
      category: 'Meetings',
      useCount: 156,
    },
    {
      id: '4',
      icon: Handshake,
      title: 'Closing deal message',
      description: 'Final push to close the sale',
      category: 'Sales',
      useCount: 134,
    },
    {
      id: '5',
      icon: RotateCcw,
      title: 'Re-engagement email',
      description: 'Reconnect with cold leads',
      category: 'Re-engagement',
      useCount: 98,
    },
  ];

  const handleUseTemplate = (templateId: string) => {
    // In a real app, this would pre-fill the dashboard with template settings
    navigate('/dashboard');
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <AppHeader />

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Templates</h1>
            <p className="text-gray-600">Quick-start templates for common scenarios</p>
          </div>

          {/* Templates Grid */}
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {templates.map((template) => (
              <div
                key={template.id}
                className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow"
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                    <template.icon className="w-6 h-6 text-orange-600" />
                  </div>
                  <span className="text-xs bg-gray-100 text-gray-700 px-2 py-1 rounded-full">
                    {template.category}
                  </span>
                </div>

                <h3 className="font-semibold text-lg text-gray-900 mb-2">{template.title}</h3>
                <p className="text-sm text-gray-600 mb-4">{template.description}</p>

                <div className="flex items-center justify-between">
                  <p className="text-xs text-gray-500">Used {template.useCount} times</p>
                  <button
                    onClick={() => handleUseTemplate(template.id)}
                    className="text-orange-600 hover:text-orange-700 font-medium text-sm transition-colors"
                  >
                    Use template →
                  </button>
                </div>
              </div>
            ))}
          </div>

          {/* Info Box */}
          <div className="mt-12 bg-blue-50 border border-blue-200 rounded-xl p-6">
            <h3 className="font-semibold text-blue-900 mb-2">How Templates Work</h3>
            <p className="text-sm text-blue-800 mb-4">
              Templates pre-fill the copy generator with common settings and goals. You can still customize everything before generating.
            </p>
            <Link to="/help" className="text-blue-600 hover:text-blue-700 font-medium text-sm">
              Learn more about templates →
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
