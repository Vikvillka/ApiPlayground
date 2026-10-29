import { observer } from "mobx-react-lite";
import { Box, Typography } from "@mui/material";
import { CollectionList } from "../CollectionList/CollectionList";
import { HistoryList } from "../HistoryList/HistoryList";
import { historyDrawerStyles } from "./HistoryDrawer.styles";

// Sidebar that composes the dispatched-request History and saved Collections.
export const HistoryDrawer = observer(function HistoryDrawer() {
  return (
    <Box component="section" id="request-history" aria-label="Request History" sx={historyDrawerStyles.root}>
      <Typography component="h2" variant="h6">History</Typography>
      <HistoryList />
      <CollectionList />
    </Box>
  );
});
