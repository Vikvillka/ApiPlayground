import { useState } from "react";
import { observer } from "mobx-react-lite";
import { Accordion, AccordionDetails, AccordionSummary, Box, Typography } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { CollectionList } from "../CollectionList/CollectionList";
import { HistoryList } from "../HistoryList/HistoryList";
import { useRootStore } from "../../stores/StoreContext";
import { historyDrawerStyles } from "./HistoryDrawer.styles";

// Overlay panel with two dropdowns, History and Collections; both are collapsed until opened.
export const HistoryDrawer = observer(function HistoryDrawer() {
  const { history } = useRootStore();
  const [historyExpanded, setHistoryExpanded] = useState(false);
  const [collectionsExpanded, setCollectionsExpanded] = useState(false);

  return (
    <Box component="section" id="request-history" aria-label="Request History" sx={historyDrawerStyles.root}>
      <Accordion
        disableGutters
        elevation={0}
        expanded={historyExpanded}
        onChange={(_event, expanded) => setHistoryExpanded(expanded)}
        sx={historyDrawerStyles.accordion}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={historyDrawerStyles.summary}>
          <Typography component="span" variant="subtitle2">History</Typography>
          <Typography component="span" variant="caption" sx={historyDrawerStyles.count}>{history.items.length}</Typography>
        </AccordionSummary>
        <AccordionDetails sx={historyDrawerStyles.details}>
          <HistoryList />
        </AccordionDetails>
      </Accordion>
      <Accordion
        disableGutters
        elevation={0}
        expanded={collectionsExpanded}
        onChange={(_event, expanded) => setCollectionsExpanded(expanded)}
        sx={historyDrawerStyles.accordion}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={historyDrawerStyles.summary}>
          <Typography component="span" variant="subtitle2">Collections</Typography>
          <Typography component="span" variant="caption" sx={historyDrawerStyles.count}>{history.collections.length}</Typography>
        </AccordionSummary>
        <AccordionDetails sx={historyDrawerStyles.details}>
          <CollectionList />
        </AccordionDetails>
      </Accordion>
    </Box>
  );
});
