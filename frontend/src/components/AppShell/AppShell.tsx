import type { ReactNode } from "react";
import { Box } from "@mui/material";
import { appShellStyles } from "./AppShell.styles";

type AppShellProps = {
  sidebar?: ReactNode;
  children: ReactNode;
};

export function AppShell({ sidebar, children }: AppShellProps) {
  return (
    <Box sx={appShellStyles.root}>
      <Box component="aside" sx={appShellStyles.sidebar}>
        {sidebar}
      </Box>
      <Box component="main" sx={appShellStyles.main}>
        {children}
      </Box>
    </Box>
  );
}
