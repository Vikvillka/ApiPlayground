import type { ReactNode } from "react";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { appTheme } from "../theme/appTheme";
import { RootStoreProvider } from "../stores/StoreContext";

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <RootStoreProvider>{children}</RootStoreProvider>
    </ThemeProvider>
  );
}
