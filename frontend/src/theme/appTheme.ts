import { createTheme } from "@mui/material/styles";

// Neutral, modern palette: near-black primary actions, indigo focus accent, green/red status.
export const appTheme = createTheme({
  palette: {
    mode: "light",
    primary: { main: "#111827", contrastText: "#ffffff" },
    secondary: { main: "#4f46e5" },
    background: { default: "#f5f6f8", paper: "#ffffff" },
    text: { primary: "#111827", secondary: "#6b7280" },
    divider: "#e5e7eb",
    success: { main: "#16a34a" },
    error: { main: "#dc2626" },
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", system-ui, sans-serif',
    h5: { fontSize: "1.125rem", fontWeight: 600, letterSpacing: "-0.01em" },
    h6: { fontSize: "1rem", fontWeight: 600, letterSpacing: "-0.01em" },
    button: { textTransform: "none", fontWeight: 600 },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { minHeight: 40, borderRadius: 8, boxShadow: "none", "&:hover": { boxShadow: "none" } },
      },
    },
    MuiTab: {
      styleOverrides: {
        root: { textTransform: "none", minHeight: 40, fontWeight: 600 },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: { borderRadius: 8, backgroundColor: "#ffffff" },
      },
    },
  },
});
