export const responseHeadersStyles = {
  list: {
    display: "grid",
    gap: 1.25,
    m: 0,
  },
  item: {
    display: "grid",
    gridTemplateColumns: "minmax(120px, 1fr) minmax(0, 2fr)",
    gap: 1.5,
    p: 1.25,
    border: "1px solid",
    borderColor: "#9BA9B5",
    borderRadius: 1,
  },
  name: {
    m: 0,
    fontFamily: "monospace",
    fontWeight: 600,
    overflowWrap: "anywhere",
  },
  values: {
    display: "grid",
    gap: 0.5,
    m: 0,
    minWidth: 0,
    fontFamily: "monospace",
    overflowWrap: "anywhere",
  },
} as const;
