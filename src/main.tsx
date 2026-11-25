import { createRoot } from "react-dom/client";
import App from "./app/App.tsx";
import { installClientErrorReporting } from "./app/lib/clientErrors";
import { installSiteAnalytics } from "./app/lib/siteAnalytics";
import "./styles/index.css";

// Uncaught errors and unhandled rejections reach the server's log, not only this console.
installClientErrorReporting();
// Cookieless visit counting (page views come from SiteAnalyticsPageViews in App); off under
// Do Not Track or Global Privacy Control, in the demo and without an API.
installSiteAnalytics();

createRoot(document.getElementById("root")!).render(<App />);
