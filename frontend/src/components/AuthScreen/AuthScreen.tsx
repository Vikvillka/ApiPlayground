import { useState, type FormEvent } from "react";
import { Box, Button, Paper, TextField, Typography } from "@mui/material";
import { logIn, registerAccount } from "../../api/authApi";
import { errorMessage } from "../../request/requestHelpers";
import { authScreenStyles } from "./AuthScreen.styles";

type AuthMode = "login" | "register";

const minimumPasswordLength = 8;

type AuthScreenProps = {
  // Shown above the form, for example "Please sign in again." after the session expired.
  notice: string | null;
  onSignedIn: (userId: string) => void;
};

// Full-page gate for signed-out visitors. Starts on registration; errors stay in the form so the user can retry.
export function AuthScreen({ notice, onSignedIn }: AuthScreenProps) {
  const [mode, setMode] = useState<AuthMode>("register");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [registeredNotice, setRegisteredNotice] = useState<string | null>(null);

  const isRegister = mode === "register";
  const passwordValid = isRegister ? password.length >= minimumPasswordLength : password.length > 0;
  const canSubmit = email.trim().length > 0 && passwordValid && !busy;

  function switchMode() {
    setMode(isRegister ? "login" : "register");
    setError(null);
    setRegisteredNotice(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSubmit) return;

    setBusy(true);
    setError(null);
    setRegisteredNotice(null);
    const credentials = { email: email.trim(), password };
    try {
      if (isRegister) {
        await registerAccount(credentials);
        setPassword("");
        setMode("login");
        setRegisteredNotice("Account created. Log in to continue.");
        setBusy(false);
      } else {
        const userId = await logIn(credentials);
        onSignedIn(userId);
      }
    } catch (cause) {
      setError(errorMessage(cause, "Something went wrong. Try again."));
      setBusy(false);
    }
  }

  const shownNotice = registeredNotice ?? notice;

  return (
    <Box sx={authScreenStyles.root}>
      <Paper variant="outlined" component="form" onSubmit={handleSubmit} noValidate aria-labelledby="auth-title" sx={authScreenStyles.card}>
        <Typography component="p" variant="overline" color="text.secondary">ApiPlayground</Typography>
        <Typography id="auth-title" component="h1" variant="h5">{isRegister ? "Create account" : "Log in"}</Typography>
        {shownNotice && (
          <Typography role="status" variant="body2">{shownNotice}</Typography>
        )}
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
        <Button type="submit" variant="contained" disabled={!canSubmit}>
          {busy ? "Working…" : isRegister ? "Create account" : "Log in"}
        </Button>
        <Button type="button" sx={authScreenStyles.switchButton} onClick={switchMode} disabled={busy}>
          {isRegister ? "I already have an account" : "Create an account"}
        </Button>
      </Paper>
    </Box>
  );
}
