import { panelCardStyles } from "../../theme/panelStyles";

export const requestEditorStyles = {
  root: panelCardStyles,
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 1.5,
  },
  saveButton: {
    alignSelf: "flex-start",
  },
  alert: {
    mt: 0.5,
  },
} as const;
