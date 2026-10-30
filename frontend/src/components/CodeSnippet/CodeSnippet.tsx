import { useState } from "react";
import { observer } from "mobx-react-lite";
import { Box, Button, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { ContentCopyOutlined } from "@mui/icons-material";
import { buildSnippet, snippetLabels, snippetLanguages, type SnippetLanguage } from "../../snippets/requestSnippets";
import { useRootStore } from "../../stores/StoreContext";
import { responsePanelStyles } from "../ResponsePanel/ResponsePanel.styles";

type CopyStatus = "idle" | "copied" | "failed";

// Shows the snippet for the current request in the editor. The snippet itself is built by the pure helper.
export const CodeSnippet = observer(function CodeSnippet() {
  const { editor } = useRootStore();
  const [language, setLanguage] = useState<SnippetLanguage>("curl");
  const [copyStatus, setCopyStatus] = useState<CopyStatus>("idle");
  const snippet = buildSnippet(language, editor.tracerRequest);

  const copySnippet = async () => {
    try {
      await navigator.clipboard.writeText(snippet);
      setCopyStatus("copied");
    } catch {
      setCopyStatus("failed");
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, mb: 1.5, flexWrap: "wrap" }}>
        <ToggleButtonGroup
          exclusive
          size="small"
          aria-label="Snippet language"
          value={language}
          onChange={(_, value: SnippetLanguage | null) => {
            if (value) {
              setLanguage(value);
              setCopyStatus("idle");
            }
          }}
        >
          {snippetLanguages.map(value => (
            <ToggleButton key={value} value={value} aria-pressed={language === value}>
              {snippetLabels[value]}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          {copyStatus === "copied" && <Typography variant="caption" role="status">Copied</Typography>}
          {copyStatus === "failed" && (
            <Typography variant="caption" role="alert" color="error">Could not copy snippet</Typography>
          )}
          <Button size="small" variant="outlined" startIcon={<ContentCopyOutlined />} onClick={copySnippet}>
            Copy
          </Button>
        </Box>
      </Box>
      <Box component="pre" sx={responsePanelStyles.body}>
        {snippet}
      </Box>
    </Box>
  );
});
