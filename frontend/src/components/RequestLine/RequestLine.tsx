import { Box, Button, NativeSelect, OutlinedInput, TextField } from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import { observer } from "mobx-react-lite";
import { requestMethods, type RequestMethod } from "../../request/requestHelpers";
import { useRootStore } from "../../stores/StoreContext";
import { methodSelectSx, requestLineStyles } from "./RequestLine.styles";

export const RequestLine = observer(function RequestLine() {
  const { editor, tracer } = useRootStore();
  const urlError = editor.validation.urlError ?? editor.validation.finalUrlError;
  const busy = tracer.loading;

  return (
    <Box sx={requestLineStyles.root}>
      <NativeSelect
        value={editor.method}
        disabled={busy}
        onChange={event => editor.setMethod(event.target.value as RequestMethod)}
        input={<OutlinedInput />}
        inputProps={{ "aria-label": "Request method" }}
        sx={methodSelectSx(editor.method)}
      >
        {requestMethods.map(method => (
          <option key={method} value={method}>
            {method}
          </option>
        ))}
      </NativeSelect>
      <TextField
        size="small"
        type="url"
        placeholder="Enter URL"
        value={editor.url}
        disabled={busy}
        onChange={event => editor.setUrl(event.target.value)}
        error={Boolean(urlError)}
        sx={requestLineStyles.url}
        slotProps={{
          htmlInput: {
            "aria-label": "Request URL",
            "aria-describedby": urlError ? "url-error" : undefined,
          },
        }}
      />
      <Button
        type="submit"
        variant="contained"
        startIcon={<PlayArrowIcon />}
        disabled={!editor.canSend || busy}
        aria-describedby={editor.hiddenValidationError ? "send-validation-error" : undefined}
      >
        {busy ? "Sending…" : "Send"}
      </Button>
    </Box>
  );
});
