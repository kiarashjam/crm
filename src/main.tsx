import { createRoot } from "react-dom/client";
import App from "./app/App.tsx";
import { preloadRoute } from "./app/publicRoutes";
import { installClientErrorReporting } from "./app/lib/clientErrors";
import { installSiteAnalytics } from "./app/lib/siteAnalytics";
import "./styles/index.css";

// Uncaught errors and unhandled rejections reach the server's log, not only this console.
installClientErrorReporting();
// Cookieless visit counting (page views come from SiteAnalyticsPageViews in App); off under
// Do Not Track or Global Privacy Control, in the demo and without an API.
installSiteAnalytics();

// A public page's code is fetched before the first render, so the app replaces the
// prerendered HTML with the same page in one step (no loading screen in between).
const root = createRoot(document.getElementById("root")!);
preloadRoute(window.location.pathname)
  .catch(() => undefined)
  .finally(() => root.render(<App />));
