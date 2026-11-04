import { expect, type Page } from "@playwright/test";

// Test-only credentials for disposable local databases. Not real secrets.
export const testPassword = "e2e-password-1";

// Creates an account on the auth screen and signs in with it.
export async function registerAndSignIn(page: Page, email: string, password = testPassword): Promise<void> {
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "Account created" })).toBeVisible();
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Log in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Log out", exact: true })).toBeVisible();
}

// Signs in with an existing account. The auth screen opens in register mode, so switch first.
export async function logIn(page: Page, email: string, password = testPassword): Promise<void> {
  await page.getByRole("button", { name: "I already have an account", exact: true }).click();
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Log in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Log out", exact: true })).toBeVisible();
}

export async function logOut(page: Page): Promise<void> {
  await page.getByRole("button", { name: "Log out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Create account", exact: true })).toBeVisible();
}
