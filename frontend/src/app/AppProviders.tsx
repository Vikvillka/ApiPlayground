import type { ReactNode } from "react";
import { CssBaseline, GlobalStyles, ThemeProvider } from "@mui/material";
import { appTheme } from "../theme/appTheme";

const globalStyles = {
  "button, input, select, textarea": { font: "inherit" },
  "button:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible": {
    outline: "2px solid #4f46e5",
    outlineOffset: 2,
  },
};

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <GlobalStyles styles={globalStyles} />
      {children}
    </ThemeProvider>
  );
}
