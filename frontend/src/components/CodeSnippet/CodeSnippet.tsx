import { useState } from "react";
import { observer } from "mobx-react-lite";
import { Box, ToggleButton, ToggleButtonGroup } from "@mui/material";
import { buildSnippet, snippetLabels, snippetLanguages, type SnippetLanguage } from "../../snippets/requestSnippets";
import { useRootStore } from "../../stores/StoreContext";
import { responsePanelStyles } from "../ResponsePanel/ResponsePanel.styles";

// Shows the snippet for the current request in the editor. The snippet itself is built by the pure helper.
export const CodeSnippet = observer(function CodeSnippet() {
  const { editor } = useRootStore();
  const [language, setLanguage] = useState<SnippetLanguage>("curl");

  return (
    <Box>
      <ToggleButtonGroup
        exclusive
        size="small"
        aria-label="Snippet language"
        value={language}
        onChange={(_, value: SnippetLanguage | null) => {
          if (value) setLanguage(value);
        }}
        sx={{ mb: 1.5 }}
      >
        {snippetLanguages.map(value => (
          <ToggleButton key={value} value={value} aria-pressed={language === value}>
            {snippetLabels[value]}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
      <Box component="pre" sx={responsePanelStyles.body}>
        {buildSnippet(language, editor.tracerRequest)}
      </Box>
    </Box>
  );
});
