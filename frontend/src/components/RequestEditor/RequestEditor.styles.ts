import { panelCardStyles } from "../../theme/panelStyles";

export const requestEditorStyles = {
  root: panelCardStyles,
  header: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 2,
  },
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 2,
  },
  tabs: {
    minHeight: 40,
    borderBottom: 1,
    borderColor: "divider",
  },
  alert: {
    mt: 0.5,
  },
} as const;
