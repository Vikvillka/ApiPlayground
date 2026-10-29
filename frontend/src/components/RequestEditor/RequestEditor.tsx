import type { FormEvent, RefObject } from "react";
import { observer } from "mobx-react-lite";
import { Box, Button, Tab, Tabs, Typography } from "@mui/material";
import BookmarkBorderIcon from "@mui/icons-material/BookmarkBorder";
import { BodyEditor } from "../BodyEditor/BodyEditor";
import { KeyValueEditor } from "../KeyValueEditor/KeyValueEditor";
import { RequestLine } from "../RequestLine/RequestLine";
import type { RequestTab } from "../../request/requestHelpers";
import { useRootStore } from "../../stores/StoreContext";
import { requestEditorStyles } from "./RequestEditor.styles";

const requestTabs: readonly RequestTab[] = ["Params", "Headers", "Body"];

const tabId = (value: RequestTab) => `request-tab-${value.toLowerCase()}`;
const panelId = (value: RequestTab) => `request-panel-${value.toLowerCase()}`;

type RequestEditorProps = {
  saveButtonRef: RefObject<HTMLButtonElement | null>;
};

export const RequestEditor = observer(function RequestEditor({ saveButtonRef }: RequestEditorProps) {
  const { editor, tracer, saveRequest } = useRootStore();
  const { validation } = editor;
  const urlError = validation.urlError ?? validation.finalUrlError;
  const busy = tracer.loading;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void tracer.send();
  }

  return (
    <Box component="section" aria-labelledby="request-title" sx={requestEditorStyles.root}>
      <Box sx={requestEditorStyles.header}>
        <Typography id="request-title" component="h2" variant="h6">
          Request
        </Typography>
        <Button
          ref={saveButtonRef}
          variant="outlined"
          size="small"
          startIcon={<BookmarkBorderIcon />}
          disabled={!editor.canSave || busy}
          onClick={() => void saveRequest.openDialog()}
        >
          Save to collection
        </Button>
      </Box>
      <Box component="form" onSubmit={handleSubmit} sx={requestEditorStyles.form}>
        <RequestLine />
        {urlError && (
          <Typography id="url-error" role="alert" color="error" variant="body2">
            {urlError}
          </Typography>
        )}
        {editor.hiddenValidationError && (
          <Typography id="send-validation-error" role="alert" color="error" variant="body2">
            {editor.hiddenValidationError}
          </Typography>
        )}
        <Tabs
          value={editor.tab}
          onChange={(_, value: RequestTab) => editor.setTab(value)}
          aria-label="Request inputs"
          selectionFollowsFocus
          sx={requestEditorStyles.tabs}
        >
          {requestTabs.map(value => (
            <Tab
              key={value}
              value={value}
              label={value}
              id={tabId(value)}
              aria-controls={panelId(value)}
            />
          ))}
        </Tabs>
        {editor.tab === "Params" && (
          <Box role="tabpanel" id={panelId("Params")} aria-labelledby={tabId("Params")}>
            <KeyValueEditor
              noun="Parameter"
              rows={editor.params}
              disabled={busy}
              keyError={null}
              onChange={(id, change) => editor.updateParam(id, change)}
              onAdd={() => editor.addParam()}
              onRemove={id => editor.removeParam(id)}
            />
          </Box>
        )}
        {editor.tab === "Headers" && (
          <Box role="tabpanel" id={panelId("Headers")} aria-labelledby={tabId("Headers")}>
            <KeyValueEditor
              noun="Header"
              rows={editor.headers}
              disabled={busy}
              keyError={validation.headerError}
              onChange={(id, change) => editor.updateHeader(id, change)}
              onAdd={() => editor.addHeader()}
              onRemove={id => editor.removeHeader(id)}
            />
          </Box>
        )}
        {editor.tab === "Body" && (
          <Box role="tabpanel" id={panelId("Body")} aria-labelledby={tabId("Body")}>
            <BodyEditor
              value={editor.body}
              disabled={busy}
              error={validation.bodyError}
              onChange={value => editor.setBody(value)}
            />
          </Box>
        )}
      </Box>
    </Box>
  );
});
