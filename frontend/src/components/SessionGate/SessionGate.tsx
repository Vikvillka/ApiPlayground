import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { observer } from "mobx-react-lite";
import { Typography } from "@mui/material";
import { setUnauthorizedHandler } from "../../api/apiClient";
import { SessionStore } from "../../stores/SessionStore";
import { AuthScreen } from "../AuthScreen/AuthScreen";

const SessionContext = createContext<SessionStore | null>(null);

export function SessionProvider({ store, children }: { store: SessionStore; children: ReactNode }) {
  return <SessionContext.Provider value={store}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionStore {
  const session = useContext(SessionContext);
  if (!session) {
    throw new Error("useSession must be used inside SessionProvider.");
  }
  return session;
}

// Renders children only for a signed-in session. Signed-out visitors see the auth screen and nothing else.
export function SessionGate({ children }: { children: ReactNode }) {
  const [store] = useState(() => new SessionStore());
  return (
    <SessionProvider store={store}>
      <SessionView>{children}</SessionView>
    </SessionProvider>
  );
}

const SessionView = observer(function SessionView({ children }: { children: ReactNode }) {
  const session = useSession();

  useEffect(() => {
    setUnauthorizedHandler(() => session.expire());
    void session.restore();
    return () => setUnauthorizedHandler(null);
  }, [session]);

  if (session.status === "checking") {
    return <Typography role="status" sx={{ p: 4, textAlign: "center" }}>Checking session…</Typography>;
  }
  if (session.status === "signedOut") {
    return <AuthScreen notice={session.notice} onSignedIn={profile => session.signIn(profile)} />;
  }
  return <>{children}</>;
});
