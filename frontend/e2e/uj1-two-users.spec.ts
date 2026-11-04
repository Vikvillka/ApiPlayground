import { expect, test } from "@playwright/test";
import { logIn, logOut, registerAndSignIn } from "./auth-helpers";

// UJ-1: Vika and Petya share one local instance. Each signs in, saves a request,
// and must not see the other's Collections. Vika's data must survive a log-out and log-in.
test("UJ-1 two users on one instance stay isolated", async ({ page }) => {
  const openCollections = async () => {
    await page.getByRole("button", { name: "Show History & Collections" }).click();
    await page.getByRole("button", { name: /^Collections/ }).click();
  };
  const saveRequest = async (title: string, collection: string, url: string) => {
    await page.getByLabel("Request URL").fill(url);
    await page.getByLabel("Request method").selectOption("GET");
    await page.getByRole("tab", { name: "Params" }).click();
    await page.getByRole("button", { name: "Save to collection" }).click();
    await page.getByLabel("Request title").fill(title);
    await page.getByLabel("New collection name").fill(collection);
    await page.getByRole("button", { name: "Save request" }).click();
    await expect(page.getByText("Request saved to Collections.")).toBeVisible();
  };

  await page.goto("/");

  // Vika registers, creates data, and logs out.
  await registerAndSignIn(page, "vika@example.test");
  await saveRequest("Vika request", "Vika collection", "https://example.com/vika");
  await logOut(page);

  // Petya registers and must see none of Vika's data.
  await registerAndSignIn(page, "petya@example.test");
  await openCollections();
  await expect(page.getByRole("button", { name: "Load saved GET Vika request", exact: true })).toHaveCount(0);
  await saveRequest("Petya request", "Petya collection", "https://example.com/petya");
  await expect(page.getByRole("button", { name: "Load saved GET Petya request", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Load saved GET Vika request", exact: true })).toHaveCount(0);
  await logOut(page);

  // Vika logs back in and sees her data, but not Petya's.
  await logIn(page, "vika@example.test");
  await openCollections();
  await expect(page.getByRole("button", { name: "Load saved GET Vika request", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Load saved GET Petya request", exact: true })).toHaveCount(0);
});
