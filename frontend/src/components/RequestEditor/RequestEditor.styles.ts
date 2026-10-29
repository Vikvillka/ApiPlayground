export const requestEditorStyles = {
  root: {
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
  },
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
