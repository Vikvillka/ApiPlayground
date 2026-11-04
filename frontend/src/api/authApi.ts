import { apiFetch, messageFrom } from "./apiClient";

export type AuthCredentials = {
  email: string;
  password: string;
};

// Registration does not sign the user in; the caller logs in separately.
export async function registerAccount(credentials: AuthCredentials): Promise<void> {
  const result = await apiFetch("/api/auth/register", "POST", credentials);
  if (!result.ok) throw new Error(messageFrom(result.body, `Registration failed (HTTP ${result.status}).`));
}

// On success the server sets the httpOnly session cookie; nothing is stored in the browser.
export async function logIn(credentials: AuthCredentials): Promise<void> {
  const result = await apiFetch("/api/auth/login", "POST", credentials);
  if (!result.ok) throw new Error(messageFrom(result.body, `Log in failed (HTTP ${result.status}).`));
}

export async function logOut(): Promise<void> {
  const result = await apiFetch("/api/auth/logout", "POST");
  if (!result.ok) throw new Error(messageFrom(result.body, `Log out failed (HTTP ${result.status}).`));
}
