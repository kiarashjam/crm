import { useState, useEffect } from 'react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getAnalyticsSummary, CopyAnalyticsSummary } from '../api/copyGenerator';

export function CopyAnalytics() {
  const [analytics, setAnalytics] = useState<CopyAnalyticsSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [dateRange, setDateRange] = useState('30d');

  useEffect(() => {
    loadAnalytics();
  }, [dateRange]);

  async function loadAnalytics() {
    setLoading(true);
    try {
      // Calculate date range
      const endDate = new Date();
      const startDate = new Date();
      switch (dateRange) {
        case '7d':
          startDate.setDate(startDate.getDate() - 7);
          break;
        case '30d':
          startDate.setDate(startDate.getDate() - 30);
          break;
        case '90d':
          startDate.setDate(startDate.getDate() - 90);
          break;
      }
      
      const data = await getAnalyticsSummary(startDate, endDate);
      setAnalytics(data);
    } catch (error) {
      console.error('Failed to load analytics:', error);
    } finally {
      setLoading(false);
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)] flex items-center justify-center" tabIndex={-1}>
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-orange-600"></div>
        </main>
      </div>
    );
  }

  if (!analytics) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <div className="text-center py-12">
            <p className="text-slate-500">Failed to load analytics data.</p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
      {/* Enhanced Header Section with Dark Decorative Elements */}
      <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
        {/* Decorative blur elements */}
        <div className="absolute inset-0 overflow-hidden">
          <div className="absolute -top-20 -right-20 w-80 h-80 bg-orange-500/20 rounded-full blur-3xl animate-pulse" />
          <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl" />
          <div className="absolute inset-0 opacity-5" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.343 0L13.857 8.485 15.272 9.9l9.9-9.9h-2.83zM32 0l-3.486 3.485-1.414-1.414L30.586 0H32zM0 5.373l.828-.83 1.415 1.415L0 8.2V5.374zm0 5.656l.828-.829 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 8.485 8.485-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 11.314 11.314-1.414 1.414L0 22.343v-.001zM60 5.373l-.828-.83-1.415 1.415L60 8.2V5.374zm0 5.656l-.828-.829-5.657 5.657 1.414 1.414L60 11.03v-.001zm0 5.656l-.828-.828-8.485 8.485 1.414 1.414L60 16.686v-.001zm0 5.657l-.828-.828-11.314 11.314 1.414 1.414L60 22.343v-.001z' fill='%23fff' fill-opacity='1' fill-rule='evenodd'/%3E%3C/svg%3E")` }} />
        </div>
        
        <div className="relative px-6 lg:px-8 py-8 lg:py-10">
          <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
            <div className="flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-orange-500/30">
                <svg className="w-8 h-8 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Copy Analytics</h1>
                <p className="text-slate-400 mt-1">
                  Track the performance of your generated copy
                </p>
              </div>
            </div>
            
            <div className="flex items-center gap-3">
              <select
                value={dateRange}
                onChange={(e) => setDateRange(e.target.value)}
                className="h-10 rounded-xl border-white/20 bg-white/10 text-white px-4 py-2 focus:border-orange-500 focus:ring-orange-500 sm:text-sm"
              >
                <option value="7d" className="text-slate-900">Last 7 days</option>
                <option value="30d" className="text-slate-900">Last 30 days</option>
                <option value="90d" className="text-slate-900">Last 90 days</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      {/* Summary Stats */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-4 mb-8">
        {/* Total Generated */}
        <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
          <div className="relative">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
              <svg className="w-5 h-5 text-slate-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
              </svg>
            </div>
            <p className="text-3xl font-bold text-slate-900 tracking-tight">{analytics.totalGenerations}</p>
            <p className="text-xs font-medium text-slate-500 mt-1">Total Generated</p>
          </div>
        </div>

        {/* Total Rewrites */}
        <div className="group relative bg-white rounded-2xl border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300 overflow-hidden">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
          <div className="relative">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
              <svg className="w-5 h-5 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
            </div>
            <p className="text-3xl font-bold text-blue-600 tracking-tight">{analytics.totalRewrites}</p>
            <p className="text-xs font-medium text-blue-600/70 mt-1">Total Rewrites</p>
          </div>
        </div>

        {/* Total Copied */}
        <div className="group relative bg-white rounded-2xl border border-purple-100 p-5 shadow-sm hover:shadow-xl hover:shadow-purple-100 hover:border-purple-200 transition-all duration-300 overflow-hidden">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-purple-50 to-purple-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
          <div className="relative">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-100 to-purple-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
              <svg className="w-5 h-5 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
            </div>
            <p className="text-3xl font-bold text-purple-600 tracking-tight">{analytics.totalCopied}</p>
            <p className="text-xs font-medium text-purple-600/70 mt-1">Total Copied</p>
          </div>
        </div>

        {/* Total Sent */}
        <div className="group relative bg-gradient-to-br from-orange-500 to-amber-600 rounded-2xl p-5 shadow-lg shadow-orange-500/20 hover:shadow-xl hover:shadow-orange-500/30 transition-all duration-300 overflow-hidden">
          <div className="absolute top-0 right-0 w-20 h-20 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
          <div className="relative">
            <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-3">
              <svg className="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
              </svg>
            </div>
            <p className="text-3xl font-bold text-white tracking-tight">{analytics.totalSent}</p>
            <p className="text-xs font-medium text-white/80 mt-1">Total Sent</p>
          </div>
        </div>

        {/* Response Rate */}
        <div className="group relative bg-gradient-to-br from-emerald-500 to-teal-600 rounded-2xl p-5 shadow-lg shadow-emerald-500/20 hover:shadow-xl hover:shadow-emerald-500/30 transition-all duration-300 overflow-hidden">
          <div className="absolute top-0 right-0 w-20 h-20 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
          <div className="relative">
            <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-3">
              <svg className="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <p className="text-3xl font-bold text-white tracking-tight">{analytics.overallResponseRate.toFixed(1)}%</p>
            <p className="text-xs font-medium text-white/80 mt-1">Response Rate</p>
            <div className="mt-3">
              <div className="h-1.5 bg-white/20 rounded-full overflow-hidden">
                <div 
                  className="h-full bg-white rounded-full transition-all duration-500"
                  style={{ width: `${Math.min(analytics.overallResponseRate, 100)}%` }}
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
        {/* Daily Trend */}
        <div className="bg-white shadow rounded-lg p-6">
          <h3 className="text-lg font-medium text-gray-900 mb-4">Daily Generation Trend</h3>
          {analytics.dailyTrend.length > 0 ? (
            <div className="space-y-2">
              {analytics.dailyTrend.slice(-14).map((day) => (
                <div key={day.date} className="flex items-center">
                  <span className="w-20 text-xs text-slate-500">
                    {new Date(day.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}
                  </span>
                  <div className="flex-1 mx-2">
                    <div className="h-4 bg-gray-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-indigo-500 rounded-full"
                        style={{
                          width: `${Math.min(100, (day.generationCount / Math.max(...analytics.dailyTrend.map(d => d.generationCount), 1)) * 100)}%`,
                        }}
                      />
                    </div>
                  </div>
                  <span className="w-8 text-xs text-gray-700 text-right">{day.generationCount}</span>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-slate-500 text-center py-8">No data available for this period.</p>
          )}
        </div>

        {/* By Copy Type */}
        <div className="bg-white shadow rounded-lg p-6">
          <h3 className="text-lg font-medium text-gray-900 mb-4">By Copy Type</h3>
          {analytics.byType.length > 0 ? (
            <div className="space-y-4">
              {analytics.byType.map((type) => (
                <div key={type.copyTypeId} className="flex items-center justify-between">
                  <div className="flex-1">
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-sm font-medium text-gray-900 capitalize">
                        {type.copyTypeId.replace(/-/g, ' ')}
                      </span>
                      <div className="text-right">
                        <span className="text-sm text-slate-500">{type.generationCount} generated</span>
                        <span className="text-xs text-green-600 ml-2">({type.responseRate.toFixed(0)}% response)</span>
                      </div>
                    </div>
                    <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-gradient-to-r from-indigo-500 to-purple-500 rounded-full"
                        style={{
                          width: `${(type.generationCount / analytics.totalGenerations) * 100}%`,
                        }}
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-slate-500 text-center py-8">No data available.</p>
          )}
        </div>
      </div>

      {/* Response Funnel */}
      <div className="bg-white shadow rounded-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200">
          <h3 className="text-lg font-medium text-gray-900">Copy Funnel by Type</h3>
        </div>
        {analytics.byType.length > 0 ? (
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Copy Type
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Generated
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Sent
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Responses
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Response Rate
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {analytics.byType.map((type, index) => (
                <tr key={type.copyTypeId}>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex items-center">
                      <span className="text-sm font-medium text-gray-900 capitalize">
                        {type.copyTypeId.replace(/-/g, ' ')}
                      </span>
                      {index === 0 && (
                        <span className="ml-2 px-2 py-0.5 text-xs bg-yellow-100 text-yellow-800 rounded">
                          Top
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                    {type.generationCount}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                    {type.sendCount}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                    {type.responseCount}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex items-center">
                      <span className={`text-sm font-medium ${
                        type.responseRate >= 20 ? 'text-green-600' :
                        type.responseRate >= 10 ? 'text-yellow-600' : 'text-gray-600'
                      }`}>
                        {type.responseRate.toFixed(1)}%
                      </span>
                      <div className="ml-2 w-24 h-2 bg-gray-100 rounded-full overflow-hidden">
                        <div
                          className={`h-full rounded-full ${
                            type.responseRate >= 20 ? 'bg-green-500' :
                            type.responseRate >= 10 ? 'bg-yellow-500' : 'bg-gray-400'
                          }`}
                          style={{ width: `${Math.min(100, type.responseRate * 3)}%` }}
                        />
                      </div>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="text-center py-12">
            <svg className="mx-auto h-12 w-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            <h3 className="mt-2 text-sm font-medium text-gray-900">No data yet</h3>
            <p className="mt-1 text-sm text-slate-500">Start generating copy to see performance data.</p>
          </div>
        )}
      </div>

      {/* Additional Insights */}
      <div className="mt-8 bg-gradient-to-r from-indigo-500 to-purple-600 rounded-lg shadow-lg p-6 text-white">
        <h3 className="text-lg font-semibold mb-2">Quick Insights</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
          <div>
            <span className="opacity-80">Most used copy type:</span>
            <div className="font-medium capitalize">
              {analytics.byType.length > 0
                ? analytics.byType[0]?.copyTypeId.replace(/-/g, ' ') ?? 'N/A'
                : 'N/A'}
            </div>
          </div>
          <div>
            <span className="opacity-80">Best day for engagement:</span>
            <div className="font-medium">
              {analytics.dailyTrend.length > 0
                ? new Date(
                    analytics.dailyTrend.reduce((best, day) =>
                      day.sendCount > best.sendCount ? day : best
                    ).date
                  ).toLocaleDateString('en-US', { weekday: 'long' })
                : 'N/A'}
            </div>
          </div>
          <div>
            <span className="opacity-80">Total responses:</span>
            <div className="font-medium">{analytics.totalResponses}</div>
          </div>
        </div>
      </div>
      </main>
    </div>
  );
}
