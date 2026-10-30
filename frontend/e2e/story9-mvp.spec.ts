import { expect, test } from "@playwright/test";

test("local MVP dispatch, History replay, policy rejection, and Collection reuse across API restart", async ({ page }) => {
  const targetUrl = process.env.APG_STORY9_TARGET_URL;
  const runnerDir = process.env.APG_STORY9_RUNNER_DIR;
  if (!targetUrl || !runnerDir) throw new Error("The Story 9 runner must provide its owned fixture URL and control directory.");
  const fs = await import("node:fs/promises");
  const path = await import("node:path");
  const hitsPath = path.join(runnerDir, "target-hits.txt");
  const readHits = async () => Number((await fs.readFile(hitsPath, "utf8")).trim());

  await page.goto("/");
  await page.getByLabel("Request URL").fill(targetUrl);
  await page.getByLabel("Request method").selectOption("POST");
  await page.getByRole("tab", { name: "Headers" }).click();
  await page.getByLabel("Header key").fill("X-Story9-Request");
  await page.getByLabel("Header value").fill("browser-e2e");
  await page.getByRole("tab", { name: "Body" }).click();
  await page.getByLabel("JSON body").fill('{"payload":"story9"}');
  await page.getByRole("button", { name: "Send", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "HTTP 200" })).toBeVisible();
  await expect(page.getByRole("status").filter({ hasText: /\d+ ms/ })).toBeVisible();
  await expect(page.getByText('"ok": true,\n  "marker": "story9"')).toBeVisible();
  await page.getByRole("tab", { name: "Headers", exact: true }).last().click();
  await expect(page.getByText("story9-loopback", { exact: true })).toBeVisible();
  await page.getByRole("tab", { name: "Body", exact: true }).last().click();

  const expectedUrl = new URL(targetUrl).toString();
  const dispatchedCount = await readHits();
  expect(dispatchedCount).toBe(1);
  const readTargetRequest = async () => JSON.parse(await fs.readFile(path.join(runnerDir, "last-request.json"), "utf8")) as {
    method: string; url: string; header: string; body: string;
  };
  expect(await readTargetRequest()).toMatchObject({ method: "POST", url: expectedUrl, header: "browser-e2e", body: '{"payload":"story9"}' });

  await page.getByRole("button", { name: "Show History & Collections" }).click();
  await expect(page.getByRole("button", { name: new RegExp("Load POST /") })).toBeVisible();
  const beforeSelectHits = await readHits();
  const historyItems = page.getByRole("button", { name: new RegExp("Load POST /") });
  await historyItems.first().click();
  expect(await readHits()).toBe(beforeSelectHits);
  await expect(page.getByLabel("Request URL")).toHaveValue(expectedUrl);
  await expect(page.getByLabel("Request method")).toHaveValue("POST");
  await page.getByRole("tab", { name: "Headers" }).click();
  await expect(page.getByLabel("Header key")).toHaveValue("X-Story9-Request");
  await expect(page.getByLabel("Header value")).toHaveValue("browser-e2e");
  await page.getByRole("tab", { name: "Body" }).click();
  await expect(page.getByLabel("JSON body")).toHaveValue('{"payload":"story9"}');
  await expect(page.getByRole("tablist", { name: "Response details" })).toHaveCount(0);

  const historyCountBeforeReplay = await page.getByRole("button", { name: new RegExp("Load POST /") }).count();
  await page.getByRole("button", { name: "Send", exact: true }).click();
  await expect(page.getByText('"ok": true,\n  "marker": "story9"')).toBeVisible();
  await expect.poll(readHits).toBe(beforeSelectHits + 1);
  expect(await readTargetRequest()).toMatchObject({ method: "POST", url: expectedUrl, header: "browser-e2e", body: '{"payload":"story9"}' });
  await expect.poll(async () => page.getByRole("button", { name: new RegExp("Load POST /") }).count()).toBe(historyCountBeforeReplay + 1);

  const rejectedUrl = new URL(targetUrl);
  rejectedUrl.protocol = "https:";
  await page.getByLabel("Request URL").fill(rejectedUrl.toString());
  await page.getByRole("button", { name: "Send", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Request not sent");
  expect(await readHits()).toBe(beforeSelectHits + 1);
  await expect(page.getByRole("button", { name: new RegExp("Load POST /") })).toHaveCount(historyCountBeforeReplay + 1);

  await page.getByLabel("Request URL").fill(targetUrl);
  await page.getByLabel("Request method").selectOption("GET");
  await page.getByRole("tab", { name: "Headers" }).click();
  await page.getByRole("button", { name: "Remove header" }).click();
  await page.getByRole("tab", { name: "Body" }).click();
  await page.getByLabel("JSON body").fill("");
  await page.getByRole("tab", { name: "Params" }).click();
  await page.getByRole("button", { name: "Save to collection" }).click();
  await page.getByLabel("Request title").fill("Story 9 fixture request");
  await page.getByLabel("New collection name").fill("Story 9 collection");
  await page.getByRole("button", { name: "Save request" }).click();
  await expect(page.getByText("Request saved to Collections.")).toBeVisible();

  await fs.writeFile(path.join(runnerDir, "restart-request"), "restart");
  await expect.poll(async () => fs.access(path.join(runnerDir, "restart-complete")).then(() => true, () => false)).toBe(true);
  await fs.rm(path.join(runnerDir, "restart-complete"), { force: true });
  await page.reload();
  await page.getByLabel("Request URL").fill(targetUrl);
  await page.getByRole("button", { name: "Send", exact: true }).click();
  await expect(page.getByText('"ok": true,\n  "marker": "story9"')).toBeVisible();
  await page.getByRole("button", { name: "Show History & Collections" }).click();
  await expect(page.getByRole("button", { name: "Load saved GET Story 9 fixture request" })).toBeVisible();
  const hitsBeforeCollectionLoad = await readHits();
  await page.getByRole("button", { name: "Load saved GET Story 9 fixture request" }).click();
  expect(await readHits()).toBe(hitsBeforeCollectionLoad);
  await expect(page.getByLabel("Request URL")).toHaveValue(expectedUrl);
  await expect(page.getByRole("tablist", { name: "Response details" })).toHaveCount(0);

  // Code snippet, Copy, and Export JSON. Export takes the response already received
  // and does not send the target again, so hit counts are not affected by it.
  await page.getByRole("button", { name: "Send", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "HTTP 200" })).toBeVisible();
  await page.getByRole("tab", { name: "Code", exact: true }).click();
  const snippet = page.getByRole("tabpanel").locator("pre");
  await expect(snippet).toContainText(expectedUrl);
  await page.getByRole("button", { name: "JavaScript fetch", exact: true }).click();
  await expect(snippet).toContainText("fetch(");
  await page.getByRole("button", { name: "curl", exact: true }).click();
  await expect(snippet).toContainText("curl");

  await page.context().grantPermissions(["clipboard-read", "clipboard-write"], { origin: new URL(page.url()).origin });
  await page.getByRole("button", { name: "Copy", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "Copied" })).toBeVisible();
  const copiedText = await page.evaluate(() => navigator.clipboard.readText());
  expect(copiedText).toBe(await snippet.textContent());

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export JSON", exact: true }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe("apiplayground-export.json");
  const exported = JSON.parse(await fs.readFile((await download.path())!, "utf8")) as { format?: string; request?: { url?: string } };
  expect(exported.format).toBe("apiplayground.export.v1");
  expect(exported.request?.url).toBe(expectedUrl);
});