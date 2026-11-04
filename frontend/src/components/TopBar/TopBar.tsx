import { Box, Button, Typography } from "@mui/material";
import BoltIcon from "@mui/icons-material/Bolt";
import HistoryIcon from "@mui/icons-material/History";
import { observer } from "mobx-react-lite";
import { platformName } from "../../platform";
import { useSession } from "../SessionGate/SessionGate";
import { topBarStyles } from "./TopBar.styles";

type TopBarProps = {
  historyOpen: boolean;
  onToggleHistory: () => void;
};

export const TopBar = observer(function TopBar({ historyOpen, onToggleHistory }: TopBarProps) {
  const session = useSession();

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
        {session.logOutError && <Typography role="alert" color="error" variant="body2">{session.logOutError}</Typography>}
        {session.email && <Typography variant="body2" color="text.secondary">Signed in as {session.email}</Typography>}
        <Button variant="outlined" onClick={() => void session.logOut()}>Log out</Button>
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
    </Box>
  );
});
