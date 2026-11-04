import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { AppProviders } from "./app/AppProviders";
import { SessionGate } from "./components/SessionGate/SessionGate";

const rootElement = document.getElementById("root");

if (!rootElement) {
  throw new Error("Root element '#root' was not found.");
}

createRoot(rootElement).render(
  <StrictMode>
    <AppProviders>
      <SessionGate>
        <App />
      </SessionGate>
    </AppProviders>
  </StrictMode>,
);