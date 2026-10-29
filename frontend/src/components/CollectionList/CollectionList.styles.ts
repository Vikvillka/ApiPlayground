export const collectionListStyles = {
  heading: {
    mt: 2.5,
    mb: 1.5,
  },
  collection: {
    mt: 1.5,
    p: 1.5,
    border: "1px solid",
    borderColor: "#D9E0E6",
    borderRadius: 1,
  },
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
