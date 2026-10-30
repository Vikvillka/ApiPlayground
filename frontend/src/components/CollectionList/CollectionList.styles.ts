export const collectionListStyles = {
  collection: {
    p: 1,
    border: 1,
    borderColor: "divider",
    borderRadius: "10px",
    bgcolor: "#f9fafb",
    "&:not(:first-of-type)": { mt: 1 },
  },
  name: {
    display: "block",
    mb: 0.75,
    px: 0.5,
    color: "text.primary",
  },
} as const;
