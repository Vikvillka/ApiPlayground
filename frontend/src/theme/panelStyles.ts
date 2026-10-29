// Shared card surface for the two main workspace panels (request editor and response panel).
export const panelCardStyles = {
  display: "flex",
  flexDirection: "column",
  gap: 2,
  minWidth: 0,
  minHeight: 440,
  p: 2.5,
  border: "1px solid",
  borderColor: "#D9E0E6",
  borderRadius: 1.5,
  bgcolor: "background.paper",
} as const;
