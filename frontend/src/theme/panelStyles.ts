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

// Segmented tab track used by the request and response panels: gray track, white selected pill.
export const segmentedTabsSx = {
  minHeight: 40,
  p: 0.5,
  borderRadius: "10px",
  bgcolor: "#f3f4f6",
  "& .MuiTabs-indicator": { display: "none" },
  "& .MuiTabs-flexContainer": { gap: 0.5 },
  "& .MuiTab-root": { flex: 1, minHeight: 36, borderRadius: "8px", color: "text.secondary" },
  "& .Mui-selected": { bgcolor: "#ffffff", color: "text.primary", boxShadow: "0 1px 2px rgba(15, 23, 42, 0.08)" },
} as const;
