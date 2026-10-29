import type { RequestMethod } from "../../request/requestHelpers";

// Soft pastel tones per HTTP method: background, text, and border.
const methodTones: Record<RequestMethod, { bg: string; fg: string; border: string }> = {
  GET: { bg: "#ecfdf5", fg: "#047857", border: "#a7f3d0" },
  POST: { bg: "#eff6ff", fg: "#1d4ed8", border: "#bfdbfe" },
  PUT: { bg: "#fffbeb", fg: "#b45309", border: "#fde68a" },
  PATCH: { bg: "#f5f3ff", fg: "#6d28d9", border: "#ddd6fe" },
  DELETE: { bg: "#fff1f2", fg: "#be123c", border: "#fecdd3" },
};

export const requestLineStyles = {
  root: {
    display: "flex",
    alignItems: "center",
    gap: 1,
  },
  method: {
    minWidth: 128,
    "& .MuiNativeSelect-select": {
      fontWeight: 700,
      letterSpacing: "0.02em",
      borderRadius: 8,
      paddingTop: "8.5px",
      paddingBottom: "8.5px",
      paddingRight: 32,
    },
    "& .MuiNativeSelect-icon": {
      right: 8,
    },
    "& option": {
      color: "#111827",
      backgroundColor: "#ffffff",
      fontWeight: 600,
    },
  },
  url: {
    flex: 1,
  },
} as const;

export function methodSelectSx(method: RequestMethod) {
  const tone = methodTones[method];
  return {
    ...requestLineStyles.method,
    // Fill and radius live on the outer root so the rounded border clips the color cleanly.
    backgroundColor: tone.bg,
    borderRadius: "8px",
    overflow: "hidden",
    "& .MuiNativeSelect-select": {
      ...requestLineStyles.method["& .MuiNativeSelect-select"],
      backgroundColor: "transparent",
      borderRadius: "8px",
      color: tone.fg,
    },
    "& .MuiNativeSelect-icon": {
      ...requestLineStyles.method["& .MuiNativeSelect-icon"],
      color: tone.fg,
    },
    "& .MuiOutlinedInput-notchedOutline": {
      borderColor: tone.border,
      borderRadius: "8px",
    },
    "&:hover .MuiOutlinedInput-notchedOutline": {
      borderColor: tone.fg,
    },
    "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
      borderColor: tone.fg,
      borderWidth: 1,
    },
  } as const;
}

