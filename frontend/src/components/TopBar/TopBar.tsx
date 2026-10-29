import { Box, Button, Typography } from "@mui/material";
import { platformName } from "../../platform";
import { topBarStyles } from "./TopBar.styles";

type TopBarProps = {
  historyOpen: boolean;
  onToggleHistory: () => void;
};

export function TopBar({ historyOpen, onToggleHistory }: TopBarProps) {
  return (
    <Box component="header" sx={topBarStyles.root}>
      <Typography component="h1" variant="h5">
        {platformName}
      </Typography>
      <Button
        variant="outlined"
        aria-controls="request-history"
        aria-expanded={historyOpen}
        onClick={onToggleHistory}
      >
        {historyOpen ? "Hide History" : "Show History"} &amp; Collections
      </Button>
    </Box>
  );
}
