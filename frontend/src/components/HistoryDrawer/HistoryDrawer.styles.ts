export const historyDrawerStyles = {
  root: {
    display: "flex",
    flexDirection: "column",
    gap: 1.5,
    minWidth: 0,
  },
  accordion: {
    border: 1,
    borderColor: "divider",
    borderRadius: "10px !important",
    boxShadow: "none",
    "&::before": { display: "none" },
    "&.Mui-expanded": { margin: 0 },
  },
  summary: {
    minHeight: 44,
    px: 1.5,
    "& .MuiAccordionSummary-content": { my: 0.5, alignItems: "center", gap: 1 },
    "&.Mui-expanded": { minHeight: 44 },
  },
  count: {
    minWidth: 22,
    px: 0.75,
    borderRadius: 999,
    bgcolor: "#f3f4f6",
    color: "text.secondary",
    textAlign: "center",
    lineHeight: "22px",
  },
  details: {
    px: 1,
    pt: 0,
    pb: 1,
    maxHeight: "60vh",
    overflowY: "auto",
  },
} as const;
