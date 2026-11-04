import { useState } from "react";
import { Box, Button, Typography } from "@mui/material";
import BoltIcon from "@mui/icons-material/Bolt";
import HistoryIcon from "@mui/icons-material/History";
import { logOut } from "../../api/authApi";
import { errorMessage } from "../../request/requestHelpers";
import { platformName } from "../../platform";
import { AuthDialog } from "../AuthDialog/AuthDialog";
import { topBarStyles } from "./TopBar.styles";

type TopBarProps = {
  historyOpen: boolean;
  onToggleHistory: () => void;
};

export function TopBar({ historyOpen, onToggleHistory }: TopBarProps) {
  const [authOpen, setAuthOpen] = useState(false);
  const [logoutNotice, setLogoutNotice] = useState<string | null>(null);
  const [logoutError, setLogoutError] = useState<string | null>(null);

  async function handleLogOut() {
    setLogoutNotice(null);
    setLogoutError(null);
    try {
      await logOut();
      setLogoutNotice("Logged out.");
    } catch (cause) {
      setLogoutError(errorMessage(cause, "Log out failed. Try again."));
    }
  }

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
      <Box sx={topBarStyles.actions}>
        {logoutNotice && <Typography role="status" variant="body2">{logoutNotice}</Typography>}
        {logoutError && <Typography role="alert" color="error" variant="body2">{logoutError}</Typography>}
        <Button variant="outlined" onClick={() => setAuthOpen(true)}>Log in</Button>
        <Button variant="outlined" onClick={() => void handleLogOut()}>Log out</Button>
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
      {authOpen && <AuthDialog onClose={() => setAuthOpen(false)} />}
    </Box>
  );
}
