import { observer } from "mobx-react-lite";
import { Box, Button, Typography } from "@mui/material";
import type { HistoryItem } from "../../api/types";
import { useRootStore } from "../../stores/StoreContext";
import { methodBadgeSx } from "../RequestLine/RequestLine.styles";
import { historyListStyles } from "./HistoryList.styles";

// Lists dispatched requests; loading one restores it into the editor without sending.
export const HistoryList = observer(function HistoryList() {
  const root = useRootStore();
  const { history, tracer } = root;

  function load(item: HistoryItem) {
    root.restoreRequest({
      url: item.url,
      method: item.method,
      body: item.body ?? null,
      headers: item.headers,
      redactedQueryParameters: item.redactedQueryParameters,
    });
  }

  if (history.loading) return <Typography role="status" variant="body2">Loading History…</Typography>;
  if (history.error) return <Typography role="alert" color="error" variant="body2">{history.error}</Typography>;
  if (history.items.length === 0) return <Typography variant="body2">No dispatched requests yet.</Typography>;

  return (
    <Box component="ul" sx={historyListStyles.list}>
      {history.items.map(item => (
        <Box component="li" key={item.id}>
          <Button
            variant="outlined"
            disabled={tracer.loading}
            onClick={() => load(item)}
            aria-label={`Load ${item.method} ${new URL(item.url).pathname}`}
            sx={historyListStyles.button}
          >
            <Box component="span" sx={methodBadgeSx(item.method)}>{item.method}</Box>
            <Box sx={historyListStyles.text}>
              <Box component="span" sx={historyListStyles.title}>{new URL(item.url).pathname}</Box>
              <Box component="small" sx={historyListStyles.meta}>
                {item.statusCode ? `HTTP ${item.statusCode}` : item.failure} · {new Date(item.createdAt).toLocaleString()}
              </Box>
            </Box>
          </Button>
        </Box>
      ))}
    </Box>
  );
});
