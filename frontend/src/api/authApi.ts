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
export async function logIn(credentials: AuthCredentials): Promise<string> {
  const result = await apiFetch("/api/auth/login", "POST", credentials);
  if (!result.ok) throw new Error(messageFrom(result.body, `Log in failed (HTTP ${result.status}).`));
  const userId = (result.body as { id?: unknown } | null)?.id;
  if (typeof userId !== "string") throw new Error("The log in response could not be read.");
  return userId;
}

// Returns the signed-in user's id, or null when the cookie is missing or expired.
export async function fetchCurrentUserId(): Promise<string | null> {
  const result = await apiFetch("/api/auth/me", "GET");
  if (result.status === 401) return null;
  if (!result.ok) throw new Error(messageFrom(result.body, `Session check failed (HTTP ${result.status}).`));
  const userId = (result.body as { id?: unknown } | null)?.id;
  return typeof userId === "string" ? userId : null;
}

export async function logOut(): Promise<void> {
  const result = await apiFetch("/api/auth/logout", "POST");
  if (!result.ok) throw new Error(messageFrom(result.body, `Log out failed (HTTP ${result.status}).`));
}
