import { useState } from "react";
import { observer } from "mobx-react-lite";
import { Box, Button, Typography } from "@mui/material";
import { FileDownloadOutlined } from "@mui/icons-material";
import { exportTracerRequest } from "../../api/tracerApi";
import { errorMessage } from "../../request/requestHelpers";
import { useRootStore } from "../../stores/StoreContext";

export const exportFileName = "apiplayground-export.json";

// Saves text as a file through a temporary object URL. The link is removed and the URL released right after the click.
export function downloadJson(fileName: string, text: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: "application/json" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

// Exports the response already received as a JSON document from the backend and saves it as a file.
export const RequestExport = observer(function RequestExport() {
  const { tracer } = useRootStore();
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const exportJson = async () => {
    const request = tracer.resultRequest;
    const response = tracer.result;
    if (!request || !response) return;
    setExporting(true);
    setError(null);
    try {
      const document = await exportTracerRequest(request, response);
      downloadJson(exportFileName, JSON.stringify(document, null, 2));
    } catch (cause) {
      setError(errorMessage(cause, "The export could not be completed."));
    } finally {
      setExporting(false);
    }
  };

  return (
    <Box sx={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 1, mb: 1.5, flexWrap: "wrap" }}>
      {error && <Typography variant="caption" role="alert" color="error">{error}</Typography>}
      <Button
        size="small"
        variant="outlined"
        startIcon={<FileDownloadOutlined />}
        disabled={tracer.result === null || exporting}
        onClick={exportJson}
      >
        Export JSON
      </Button>
    </Box>
  );
});
