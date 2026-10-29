export const keyValueEditorStyles = {
  root: {
    display: "flex",
    flexDirection: "column",
    gap: 1.5,
    pt: 2,
  },
  row: {
    display: "grid",
    gridTemplateColumns: "1fr 1fr auto auto",
    gap: 1,
    alignItems: "center",
  },
} as const;
