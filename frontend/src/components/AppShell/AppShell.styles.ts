export const appShellStyles = {
  root: {
    display: "flex",
    minHeight: "100vh",
    bgcolor: "background.default",
  },
  // Side panel: sits beside the workspace and pushes the content right, so nothing is covered.
  sidebar: {
    flex: "0 0 320px",
    position: "sticky",
    top: 0,
    alignSelf: "flex-start",
    height: "100vh",
    overflowY: "auto",
    p: 2,
    bgcolor: "background.paper",
    borderRight: 1,
    borderColor: "divider",
  },
  main: {
    flex: 1,
    minWidth: 0,
    p: { xs: 2, md: 3 },
  },
} as const;
