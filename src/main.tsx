import { createRoot } from "react-dom/client";
import App from "./app/App.tsx";
import { installClientErrorReporting } from "./app/lib/clientErrors";
import "./styles/index.css";

// Uncaught errors and unhandled rejections reach the server's log, not only this console.
installClientErrorReporting();

createRoot(document.getElementById("root")!).render(<App />);
