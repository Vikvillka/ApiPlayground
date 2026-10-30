import { observer } from "mobx-react-lite";
import { Box, Button, Typography } from "@mui/material";
import type { RequestCollection } from "../../api/types";
import { useRootStore } from "../../stores/StoreContext";
import { methodBadgeSx } from "../RequestLine/RequestLine.styles";
import { historyListStyles } from "../HistoryList/HistoryList.styles";
import { collectionListStyles } from "./CollectionList.styles";

// Lists saved collections; loading a saved request restores it into the editor without sending.
export const CollectionList = observer(function CollectionList() {
  const root = useRootStore();
  const { history, tracer } = root;

  function load(item: RequestCollection["requests"][number]) {
    root.restoreRequest({
      url: item.url,
      method: item.method,
      body: item.body ?? null,
      headers: item.headers,
    });
  }

  return (
    <>
      {history.collectionError && <Typography role="alert" color="error" variant="body2">{history.collectionError}</Typography>}
      {!history.collectionError && history.collections.length === 0 && (
        <Typography variant="body2">No saved collections yet. Save a configured request to keep it for later.</Typography>
      )}
      {history.collections.map(collection => (
        <Box component="section" key={collection.id} aria-label={collection.name} sx={collectionListStyles.collection}>
          <Typography component="h3" variant="subtitle2" sx={collectionListStyles.name}>{collection.name}</Typography>
          {collection.requests.length === 0 && <Typography variant="body2">No saved requests in this collection.</Typography>}
          <Box component="ul" sx={historyListStyles.list}>
            {collection.requests.map(item => (
              <Box component="li" key={item.id}>
                <Button
                  variant="outlined"
                  disabled={tracer.loading}
                  onClick={() => load(item)}
                  aria-label={`Load saved ${item.method} ${item.title}`}
                  sx={historyListStyles.button}
                >
                  <Box component="span" sx={methodBadgeSx(item.method)}>{item.method}</Box>
                  <Box sx={historyListStyles.text}>
                    <Box component="span" sx={historyListStyles.title}>{item.title}</Box>
                    <Box component="small" sx={historyListStyles.meta}>{new URL(item.url).pathname}</Box>
                  </Box>
                </Button>
              </Box>
            ))}
          </Box>
        </Box>
      ))}
    </>
  );
});
