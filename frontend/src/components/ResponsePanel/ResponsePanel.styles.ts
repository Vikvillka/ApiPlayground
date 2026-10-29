import { panelCardStyles } from "../../theme/panelStyles";

export const responsePanelStyles = {
  root: panelCardStyles,
  tabs: {
    minHeight: 40,
    borderBottom: 1,
    borderColor: "divider",
  },
  summary: {
    display: "flex",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 1.5,
    color: "text.secondary",
    fontSize: "0.875rem",
  },
  previous: {
    p: 1.5,
    borderLeft: "3px solid",
    borderColor: "secondary.main",
    borderRadius: "8px",
    bgcolor: "#f3f4f6",
    fontSize: "0.875rem",
  },
  body: {
    m: 0,
    p: 2,
    maxHeight: 520,
    overflow: "auto",
    borderRadius: "10px",
    bgcolor: "#0f172a",
    color: "#e2e8f0",
    whiteSpace: "pre-wrap",
    overflowWrap: "anywhere",
    fontFamily: 'ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace',
    fontSize: "0.8125rem",
    lineHeight: 1.6,
  },
} as const;
