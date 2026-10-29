export const keyValueEditorStyles = {
  root: {
    display: "flex",
    flexDirection: "column",
    gap: 1.5,
    pt: 0.5,
  },
  row: {
    display: "grid",
    gridTemplateColumns: "minmax(0, 1fr) minmax(0, 2fr) auto auto",
    gap: 1,
    alignItems: "center",
  },
  remove: {
    color: "text.secondary",
    "&:hover": { color: "error.main" },
  },
  add: {
    alignSelf: "flex-start",
    color: "primary.main",
  },
} as const;
