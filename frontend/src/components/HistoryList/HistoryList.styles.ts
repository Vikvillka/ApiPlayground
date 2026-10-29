export const historyListStyles = {
  list: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
    gap: 1,
    m: 0,
    p: 0,
    listStyle: "none",
  },
  button: {
    display: "block",
    minHeight: 58,
    px: 1.25,
    py: 1,
    textAlign: "left",
    textTransform: "none",
    color: "text.primary",
    borderColor: "#D9E0E6",
  },
  title: {
    display: "block",
  },
  meta: {
    display: "block",
    mt: 0.5,
    overflowWrap: "anywhere",
    color: "text.secondary",
  },
} as const;
