import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { CheckCircle, Search } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';

export default function SendToHubSpot() {
  const [objectType, setObjectType] = useState('contact');
  const [selectedRecord, setSelectedRecord] = useState('');
  const [isSent, setIsSent] = useState(false);
  const navigate = useNavigate();

  const records = {
    contact: [
      { id: '1', name: 'John Smith', email: 'john@example.com' },
      { id: '2', name: 'Sarah Johnson', email: 'sarah@example.com' },
      { id: '3', name: 'Mike Williams', email: 'mike@example.com' },
    ],
    deal: [
      { id: '1', name: 'Acme Corp - Enterprise Plan', value: '$50,000' },
      { id: '2', name: 'TechStart Inc - Pro Package', value: '$25,000' },
      { id: '3', name: 'Global Solutions - Custom', value: '$75,000' },
    ],
  };

  const handleSend = () => {
    if (selectedRecord) {
      setIsSent(true);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <AppHeader />

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          {!isSent ? (
            <>
              <div className="mb-8">
                <h1 className="text-3xl font-bold text-gray-900 mb-2">Send to HubSpot</h1>
                <p className="text-gray-600">Choose where to save your generated copy</p>
              </div>

              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8">
                {/* Select Object Type */}
                <div className="mb-6">
                  <label className="block text-sm font-medium text-gray-700 mb-3">
                    Select HubSpot object
                  </label>
                  <div className="grid grid-cols-2 gap-3">
                    <button
                      onClick={() => setObjectType('contact')}
                      className={`p-4 rounded-lg border-2 transition-colors ${
                        objectType === 'contact'
                          ? 'border-orange-500 bg-orange-50'
                          : 'border-gray-300 hover:border-orange-300'
                      }`}
                    >
                      <p className="font-semibold">Contact</p>
                      <p className="text-xs text-gray-600">Individual person</p>
                    </button>
                    <button
                      onClick={() => setObjectType('deal')}
                      className={`p-4 rounded-lg border-2 transition-colors ${
                        objectType === 'deal'
                          ? 'border-orange-500 bg-orange-50'
                          : 'border-gray-300 hover:border-orange-300'
                      }`}
                    >
                      <p className="font-semibold">Deal</p>
                      <p className="text-xs text-gray-600">Sales opportunity</p>
                    </button>
                    <button
                      onClick={() => setObjectType('workflow')}
                      className={`p-4 rounded-lg border-2 transition-colors ${
                        objectType === 'workflow'
                          ? 'border-orange-500 bg-orange-50'
                          : 'border-gray-300 hover:border-orange-300'
                      }`}
                    >
                      <p className="font-semibold">Workflow</p>
                      <p className="text-xs text-gray-600">Automation sequence</p>
                    </button>
                    <button
                      onClick={() => setObjectType('email')}
                      className={`p-4 rounded-lg border-2 transition-colors ${
                        objectType === 'email'
                          ? 'border-orange-500 bg-orange-50'
                          : 'border-gray-300 hover:border-orange-300'
                      }`}
                    >
                      <p className="font-semibold">Email draft</p>
                      <p className="text-xs text-gray-600">Save as template</p>
                    </button>
                  </div>
                </div>

                {/* Select Specific Record */}
                <div className="mb-8">
                  <label className="block text-sm font-medium text-gray-700 mb-3">
                    Select specific record
                  </label>
                  <div className="relative mb-3">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                    <input
                      type="text"
                      placeholder={`Search ${objectType}s...`}
                      className="w-full pl-10 pr-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition-colors"
                    />
                  </div>
                  <div className="border border-gray-200 rounded-lg divide-y">
                    {(records[objectType as keyof typeof records] || []).map((record) => (
                      <label
                        key={record.id}
                        className="flex items-center p-4 hover:bg-gray-50 cursor-pointer transition-colors"
                      >
                        <input
                          type="radio"
                          name="record"
                          value={record.id}
                          checked={selectedRecord === record.id}
                          onChange={(e) => setSelectedRecord(e.target.value)}
                          className="w-4 h-4 text-orange-600 focus:ring-orange-500"
                        />
                        <div className="ml-3">
                          <p className="font-medium text-gray-900">{record.name}</p>
                          <p className="text-sm text-gray-600">
                            {'email' in record ? record.email : record.value}
                          </p>
                        </div>
                      </label>
                    ))}
                  </div>
                </div>

                {/* Confirm Button */}
                <button
                  onClick={handleSend}
                  disabled={!selectedRecord}
                  className="w-full bg-orange-600 hover:bg-orange-700 disabled:bg-gray-300 disabled:cursor-not-allowed text-white font-semibold py-4 px-6 rounded-lg transition-colors"
                >
                  Confirm & Send
                </button>
              </div>
            </>
          ) : (
            // Success Screen
            <div className="text-center">
              <div className="w-20 h-20 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-6">
                <CheckCircle className="w-12 h-12 text-green-600" />
              </div>
              <h1 className="text-3xl font-bold text-gray-900 mb-4">Successfully Sent!</h1>
              <p className="text-gray-600 mb-8">
                Your copy has been added to HubSpot and is ready to use.
              </p>

              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8 text-left">
                <p className="text-sm text-gray-600 mb-2">Sent to</p>
                <p className="font-semibold text-gray-900 mb-1">
                  {records[objectType as keyof typeof records]?.find(r => r.id === selectedRecord)?.name}
                </p>
                <p className="text-sm text-gray-600 capitalize">{objectType}</p>
              </div>

              <div className="flex gap-4">
                <button
                  onClick={() => navigate('/dashboard')}
                  className="flex-1 bg-orange-600 hover:bg-orange-700 text-white font-semibold py-3 px-6 rounded-lg transition-colors"
                >
                  Create Another
                </button>
                <button
                  onClick={() => navigate('/history')}
                  className="flex-1 bg-white hover:bg-gray-50 text-gray-900 border-2 border-gray-300 font-semibold py-3 px-6 rounded-lg transition-colors"
                >
                  View History
                </button>
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
