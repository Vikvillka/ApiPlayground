import type { KeyboardEvent } from "react";
import { observer } from "mobx-react-lite";
import { Box, Chip, Tab, Tabs, Typography } from "@mui/material";
import { formatResponseBody, type ResponseTab } from "../../request/requestHelpers";
import { useRootStore } from "../../stores/StoreContext";
import { CodeSnippet } from "../CodeSnippet/CodeSnippet";
import { RequestExport } from "../RequestExport/RequestExport";
import { ResponseHeaders } from "../ResponseHeaders/ResponseHeaders";
import { responsePanelStyles } from "./ResponsePanel.styles";

const responseTabs: readonly ResponseTab[] = ["Body", "Headers", "Code"];

// Human-readable size of the response body in UTF-8 bytes, for example "512 B" or "2.3 KB".
function formatBodySize(body: string | null): string {
  const bytes = new TextEncoder().encode(body ?? "").length;
  return bytes < 1024 ? `${bytes} B` : `${(bytes / 1024).toFixed(1)} KB`;
}

const tabId = (value: ResponseTab) => `response-tab-${value.toLowerCase()}`;
const panelId = (value: ResponseTab) => `response-panel-${value.toLowerCase()}`;

export const ResponsePanel = observer(function ResponsePanel() {
  const { tracer } = useRootStore();
  const result = tracer.result;
  const hasDetails = result !== null && result.requestSent && result.statusCode !== null;
  const statusCode = result?.statusCode ?? null;
  const isSuccess = statusCode !== null && statusCode >= 200 && statusCode < 300;

  function handleTabKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const currentIndex = responseTabs.findIndex(value => tabId(value) === (event.target as HTMLElement).id);
    if (currentIndex < 0) return;
    const lastIndex = responseTabs.length - 1;
    const nextIndex = event.key === "ArrowRight"
      ? (currentIndex + 1) % responseTabs.length
      : event.key === "ArrowLeft"
        ? (currentIndex - 1 + responseTabs.length) % responseTabs.length
        : event.key === "Home"
          ? 0
          : event.key === "End"
            ? lastIndex
            : -1;
    if (nextIndex < 0) return;
    event.preventDefault();
    const nextValue = responseTabs[nextIndex];
    tracer.setResponseTab(nextValue);
    document.getElementById(tabId(nextValue))?.focus();
  }

  return (
    <Box component="section" aria-labelledby="response-title" sx={responsePanelStyles.root}>
      <Typography id="response-title" component="h2" variant="h6">
        Response
      </Typography>
      {tracer.loading && <Typography role="status" variant="body2">Sending request…</Typography>}
      {tracer.error && <Typography role="alert" color="error" variant="body2">{tracer.error}</Typography>}
      {tracer.notice && <Typography role="status" variant="body2">{tracer.notice}</Typography>}
      {!result && !tracer.error && !tracer.loading && (
        <Typography variant="body2" color="text.secondary">Enter a URL to send a request.</Typography>
      )}
      {result && (
        <>
          {tracer.previousResult && (
            <Box role="status" sx={responsePanelStyles.previous}>
              This result is for the previous request.
            </Box>
          )}
          {hasDetails && (
            <Box role="status" sx={responsePanelStyles.summary}>
              <Chip
                size="small"
                variant="outlined"
                color={isSuccess ? "success" : "error"}
                label={`HTTP ${result.statusCode}`}
              />
              <span>{result.elapsedMilliseconds.toFixed(0)} ms</span>
              <span>{formatBodySize(result.body)}</span>
            </Box>
          )}
          {result.error && (
            <Typography role={result.requestSent ? "status" : "alert"} color={result.requestSent ? "text.primary" : "error"} variant="body2">
              {result.error}
            </Typography>
          )}
          {hasDetails && (
            <>
              <RequestExport />
              <Box onKeyDown={handleTabKeyDown}>
                <Tabs
                  value={tracer.responseTab}
                  onChange={(_, value: ResponseTab) => tracer.setResponseTab(value)}
                  aria-label="Response details"
                  selectionFollowsFocus
                  sx={responsePanelStyles.tabs}
                >
                  {responseTabs.map(value => (
                    <Tab
                      key={value}
                      value={value}
                      label={value}
                      id={tabId(value)}
                      aria-controls={panelId(value)}
                    />
                  ))}
                </Tabs>
              </Box>
              {tracer.responseTab === "Body" && (
                <Box role="tabpanel" id={panelId("Body")} aria-labelledby={tabId("Body")} tabIndex={0}>
                  <Box component="pre" sx={responsePanelStyles.body}>
                    {formatResponseBody(result.body) || "The response body is empty."}
                  </Box>
                </Box>
              )}
              {tracer.responseTab === "Headers" && (
                <Box role="tabpanel" id={panelId("Headers")} aria-labelledby={tabId("Headers")} tabIndex={0}>
                  <ResponseHeaders headers={result.headers ?? {}} />
                </Box>
              )}
              {tracer.responseTab === "Code" && (
                <Box role="tabpanel" id={panelId("Code")} aria-labelledby={tabId("Code")} tabIndex={0}>
                  <CodeSnippet />
                </Box>
              )}
            </>
          )}
        </>
      )}
    </Box>
  );
});
