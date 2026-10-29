import { Box, Button, Typography } from "@mui/material";
import BoltIcon from "@mui/icons-material/Bolt";
import HistoryIcon from "@mui/icons-material/History";
import { platformName } from "../../platform";
import { topBarStyles } from "./TopBar.styles";

type TopBarProps = {
  historyOpen: boolean;
  onToggleHistory: () => void;
};

export function TopBar({ historyOpen, onToggleHistory }: TopBarProps) {
  return (
    <Box component="header" sx={topBarStyles.root}>
      <Box sx={topBarStyles.brand}>
        <Box sx={topBarStyles.logo} aria-hidden="true">
          <BoltIcon fontSize="small" />
        </Box>
        <Typography component="h1" variant="h5">
          {platformName}
        </Typography>
      </Box>
      <Button
        variant="outlined"
        startIcon={<HistoryIcon />}
        aria-controls="request-history"
        aria-expanded={historyOpen}
        onClick={onToggleHistory}
      >
        {historyOpen ? "Hide History" : "Show History"} &amp; Collections
      </Button>
    </Box>
  );
}
