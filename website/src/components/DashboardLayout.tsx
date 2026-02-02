import { Outlet, Link, useNavigate } from 'react-router-dom'

export function DashboardLayout() {
  const navigate = useNavigate()

  return (
    <div className="min-h-screen bg-stone-50">
      <header className="sticky top-0 z-50 bg-white border-b border-stone-200 shadow-sm">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <Link to="/dashboard" className="font-display font-bold text-xl text-stone-800">
            Cadence
          </Link>
          <nav className="flex items-center gap-2">
            <Link to="/dashboard" className="px-3 py-2 text-stone-600 hover:text-orange-600 text-sm font-medium rounded-lg hover:bg-orange-50">
              Dashboard
            </Link>
            <Link to="/templates" className="px-3 py-2 text-stone-600 hover:text-orange-600 text-sm font-medium rounded-lg hover:bg-orange-50">
              Templates
            </Link>
            <Link to="/history" className="px-3 py-2 text-stone-600 hover:text-orange-600 text-sm font-medium rounded-lg hover:bg-orange-50">
              History
            </Link>
            <Link to="/settings" className="px-3 py-2 text-stone-600 hover:text-orange-600 text-sm font-medium rounded-lg hover:bg-orange-50">
              Settings
            </Link>
            <Link to="/help" className="px-3 py-2 text-stone-600 hover:text-orange-600 text-sm font-medium rounded-lg hover:bg-orange-50">
              Help
            </Link>
            <button
              type="button"
              onClick={() => navigate('/')}
              className="ml-2 px-3 py-2 text-stone-500 hover:text-stone-700 text-sm rounded-lg hover:bg-stone-100"
            >
              Logout
            </button>
            <div className="w-8 h-8 rounded-full bg-orange-500 text-white flex items-center justify-center text-sm font-semibold ml-2">
              U
            </div>
          </nav>
        </div>
      </header>
      <main className="max-w-6xl mx-auto px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
