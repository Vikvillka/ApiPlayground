import { useRef, type RefObject } from "react";
import { observer } from "mobx-react-lite";
import { Box } from "@mui/material";
import { AppShell } from "./components/AppShell/AppShell";
import { HistoryDrawer } from "./components/HistoryDrawer/HistoryDrawer";
import { RequestEditor } from "./components/RequestEditor/RequestEditor";
import { ResponsePanel } from "./components/ResponsePanel/ResponsePanel";
import { SaveRequestDialog } from "./components/SaveRequestDialog/SaveRequestDialog";
import { TopBar } from "./components/TopBar/TopBar";
import { RootStoreProvider, useRootStore } from "./stores/StoreContext";

export function App() {
  return (
    <RootStoreProvider>
      <Workspace />
    </RootStoreProvider>
  );
}

// Composes the workspace: the shell, the top bar, the request and response panes, and the History sidebar and dialogs.
const Workspace = observer(function Workspace() {
  const { history } = useRootStore();
  const saveTriggerRef: RefObject<HTMLButtonElement | null> = useRef<HTMLButtonElement>(null);

  return (
    <AppShell sidebar={history.open ? <HistoryDrawer /> : undefined}>
      <TopBar historyOpen={history.open} onToggleHistory={() => void history.toggle()} />
      <Box sx={{
        display: "grid",
        gridTemplateColumns: { xs: "minmax(0, 1fr)", md: "repeat(2, minmax(0, 1fr))" },
        gap: 3,
        maxWidth: 1440,
        mx: "auto",
      }}>
        <RequestEditor saveButtonRef={saveTriggerRef} />
        <ResponsePanel />
      </Box>
      <SaveRequestDialog returnFocusRef={saveTriggerRef} />
    </AppShell>
  );
});
