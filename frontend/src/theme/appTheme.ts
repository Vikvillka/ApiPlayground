import { createTheme } from "@mui/material/styles";

// Palette and radii follow the approved reference screenshot: neutral surfaces, near-black primary, green status.
export const appTheme = createTheme({
  palette: {
    mode: "light",
    primary: { main: "#09090b" },
    background: { default: "#f4f4f5", paper: "#ffffff" },
    success: { main: "#16a34a" },
    error: { main: "#dc2626" },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", system-ui, sans-serif',
  },
});
