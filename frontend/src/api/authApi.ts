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

export type AccountProfile = {
  id: string;
  email: string;
};

// Reads { id, email } from a response body; returns null when either field is missing.
function profileFrom(body: unknown): AccountProfile | null {
  const { id, email } = (body ?? {}) as { id?: unknown; email?: unknown };
  return typeof id === "string" && typeof email === "string" ? { id, email } : null;
}

// On success the server sets the httpOnly session cookie; nothing is stored in the browser.
export async function logIn(credentials: AuthCredentials): Promise<AccountProfile> {
  const result = await apiFetch("/api/auth/login", "POST", credentials);
  if (!result.ok) throw new Error(messageFrom(result.body, `Log in failed (HTTP ${result.status}).`));
  const profile = profileFrom(result.body);
  if (!profile) throw new Error("The log in response could not be read.");
  return profile;
}

// Returns the signed-in user's profile, or null when the cookie is missing or expired.
export async function fetchCurrentUser(): Promise<AccountProfile | null> {
  const result = await apiFetch("/api/auth/me", "GET");
  if (result.status === 401) return null;
  if (!result.ok) throw new Error(messageFrom(result.body, `Session check failed (HTTP ${result.status}).`));
  return profileFrom(result.body);
}

export async function logOut(): Promise<void> {
  const result = await apiFetch("/api/auth/logout", "POST");
  if (!result.ok) throw new Error(messageFrom(result.body, `Log out failed (HTTP ${result.status}).`));
}
