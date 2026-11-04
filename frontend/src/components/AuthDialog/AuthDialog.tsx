import { useState, type FormEvent } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Typography,
} from "@mui/material";
import { logIn, registerAccount } from "../../api/authApi";
import { errorMessage } from "../../request/requestHelpers";
import { authDialogStyles } from "./AuthDialog.styles";

type AuthMode = "login" | "register";

const minimumPasswordLength = 8;

type AuthDialogProps = {
  onClose: () => void;
};

// Rendered only while open. Errors stay in the form so the user can correct the input and retry.
export function AuthDialog({ onClose }: AuthDialogProps) {
  const [mode, setMode] = useState<AuthMode>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const isRegister = mode === "register";
  const passwordValid = isRegister ? password.length >= minimumPasswordLength : password.length > 0;
  const canSubmit = email.trim().length > 0 && passwordValid && !busy;

  function switchMode() {
    setMode(isRegister ? "login" : "register");
    setError(null);
    setNotice(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSubmit) return;

    setBusy(true);
    setError(null);
    setNotice(null);
    const credentials = { email: email.trim(), password };
    try {
      if (isRegister) {
        await registerAccount(credentials);
        setPassword("");
        setMode("login");
        setNotice("Account created. Log in to continue.");
      } else {
        await logIn(credentials);
        onClose();
      }
    } catch (cause) {
      setError(errorMessage(cause, "Something went wrong. Try again."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open fullWidth maxWidth="xs" onClose={() => { if (!busy) onClose(); }} aria-labelledby="auth-dialog-title">
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogTitle id="auth-dialog-title">{isRegister ? "Create account" : "Log in"}</DialogTitle>
        <DialogContent sx={authDialogStyles.content}>
          <TextField
            id="auth-email"
            label="Email"
            type="email"
            value={email}
            disabled={busy}
            autoComplete="email"
            autoFocus
            fullWidth
            onChange={event => setEmail(event.target.value)}
          />
          <TextField
            id="auth-password"
            label="Password"
            type="password"
            value={password}
            disabled={busy}
            autoComplete={isRegister ? "new-password" : "current-password"}
            helperText={isRegister ? `At least ${minimumPasswordLength} characters.` : undefined}
            fullWidth
            onChange={event => setPassword(event.target.value)}
          />
          {error && (
            <Typography role="alert" color="error" variant="body2">{error}</Typography>
          )}
          {notice && (
            <Typography role="status" variant="body2">{notice}</Typography>
          )}
          <Button type="button" sx={authDialogStyles.switchButton} onClick={switchMode} disabled={busy}>
            {isRegister ? "I already have an account" : "Create an account"}
          </Button>
        </DialogContent>
        <DialogActions>
          <Button type="button" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={!canSubmit}>
            {busy ? "Working…" : isRegister ? "Create account" : "Log in"}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  );
}
