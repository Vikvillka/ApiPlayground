export const topBarStyles = {
  root: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 2,
    mb: 3,
    pb: 2,
    borderBottom: 1,
    borderColor: "divider",
  },
  brand: {
    display: "flex",
    alignItems: "center",
    gap: 1.25,
  },
  logo: {
    display: "grid",
    placeItems: "center",
    width: 32,
    height: 32,
    borderRadius: "8px",
    bgcolor: "primary.main",
    color: "primary.contrastText",
  },
} as const;
