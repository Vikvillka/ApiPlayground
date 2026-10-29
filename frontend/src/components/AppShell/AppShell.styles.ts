export const appShellStyles = {
  root: {
    display: "flex",
    minHeight: "100vh",
    bgcolor: "background.default",
  },
  sidebar: {
    width: 240,
    flexShrink: 0,
    borderRight: 1,
    borderColor: "divider",
    bgcolor: "background.paper",
  },
  main: {
    flex: 1,
    minWidth: 0,
    p: 3,
  },
} as const;
