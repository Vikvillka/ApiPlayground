// Shared card surface for the two main workspace panels (request editor and response panel).
export const panelCardStyles = {
  display: "flex",
  flexDirection: "column",
  gap: 2.5,
  minWidth: 0,
  minHeight: 480,
  p: 3,
  border: "1px solid",
  borderColor: "divider",
  borderRadius: "12px",
  bgcolor: "background.paper",
  boxShadow: "0 1px 2px rgba(15, 23, 42, 0.04)",
} as const;
