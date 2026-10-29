import { Box, Button, NativeSelect, TextField } from "@mui/material";
import { observer } from "mobx-react-lite";
import { requestMethods, type RequestMethod } from "../../request/requestHelpers";
import { useRootStore } from "../../stores/StoreContext";
import { requestLineStyles } from "./RequestLine.styles";

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
        inputProps={{ "aria-label": "Request method" }}
        sx={requestLineStyles.method}
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
        disabled={!editor.canSend || busy}
        aria-describedby={editor.hiddenValidationError ? "send-validation-error" : undefined}
      >
        {busy ? "Sending…" : "Send"}
      </Button>
    </Box>
  );
});
