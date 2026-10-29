import { spawn, type ChildProcess } from "node:child_process";
import { mkdtemp, rm } from "node:fs/promises";
import net from "node:net";
import { setTimeout as delay } from "node:timers/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(frontendRoot, "..");
const runnerDirectory = await mkdtemp(path.join(process.env.TEMP ?? process.env.TMP ?? ".", "apg-story9-"));
const children: ChildProcess[] = [];
let apiHostProcess: ChildProcess | undefined;
let cleanupPromise: Promise<void> | undefined;
let shuttingDown = false;

function start(command: string, args: string[], cwd: string, env: NodeJS.ProcessEnv, stripTestAdmin = false): ChildProcess {
  const childEnvironment = { ...process.env, ...env };
  if (stripTestAdmin) delete childEnvironment.APIPLAYGROUND_TEST_ADMIN_CONNECTION;
  const child = spawn(command, args, { cwd, env: childEnvironment, stdio: "inherit", windowsHide: true });
  children.push(child);
  return child;
}

function waitExit(child: ChildProcess): Promise<number> {
  if (child.exitCode !== null || child.signalCode !== null)
    return child.exitCode === 0 ? Promise.resolve(0) : Promise.reject(new Error(`Child exited with code ${child.exitCode}.`));
  return new Promise((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", (code, signal) => code === 0 ? resolve(0) : reject(new Error(`Child exited code=${code} signal=${signal}.`)));
  });
}

async function assertPortAvailable(port: number): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const socket = net.createConnection({ host: "127.0.0.1", port });
    socket.once("connect", () => {
      socket.destroy();
      reject(new Error(`Story 9 requires localhost:${port}, but that port is already in use.`));
    });
    socket.once("error", () => resolve());
  });
}

async function waitFor(predicate: () => Promise<boolean>, description: string, timeoutMs = 30_000): Promise<void> {
  const end = Date.now() + timeoutMs;
  let lastError: unknown;
  while (Date.now() < end) {
    if (shuttingDown) throw new Error("Story 9 runner was interrupted during startup.");
    try { if (await predicate()) return; } catch (error) { lastError = error; }
    await delay(150);
  }
  throw new Error(`Timed out waiting for ${description}.${lastError instanceof Error ? ` ${lastError.message}` : ""}`);
}

async function stopChildren(): Promise<void> {
  for (const child of [...children].reverse()) {
    if (!child.pid || child.exitCode !== null || child.signalCode !== null) continue;
    if (process.platform === "win32") {
      await new Promise<void>(resolve => {
        const killer = spawn("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore", windowsHide: true });
        killer.once("exit", () => resolve());
        killer.once("error", () => resolve());
      });
    } else {
      child.kill("SIGTERM");
      await new Promise<void>(resolve => {
        const timer = setTimeout(() => { child.kill("SIGKILL"); resolve(); }, 3_000);
        child.once("exit", () => { clearTimeout(timer); resolve(); });
      });
    }
  }
}

function cleanup(): Promise<void> {
  if (!cleanupPromise) {
    shuttingDown = true;
    cleanupPromise = (async () => {
      if (apiHostProcess && apiHostProcess.exitCode === null && apiHostProcess.signalCode === null) {
        await (await import("node:fs/promises")).writeFile(path.join(runnerDirectory, "shutdown"), "stop").catch(() => undefined);
        await Promise.race([waitExit(apiHostProcess).catch(() => undefined), delay(15_000)]);
      }
      await stopChildren();
      await rm(runnerDirectory, { recursive: true, force: true });
    })();
  }
  return cleanupPromise;
}

process.once("SIGINT", () => { void cleanup().finally(() => process.exit(130)); });
process.once("SIGTERM", () => { void cleanup().finally(() => process.exit(143)); });
process.once("uncaughtException", error => { console.error(error.message); void cleanup().finally(() => process.exit(1)); });
process.once("unhandledRejection", error => { console.error(error instanceof Error ? error.message : "Unhandled Story 9 runner failure."); void cleanup().finally(() => process.exit(1)); });

try {
  const adminConnection = process.env.APIPLAYGROUND_TEST_ADMIN_CONNECTION;
  if (!adminConnection) throw new Error("Set APIPLAYGROUND_TEST_ADMIN_CONNECTION to a disposable-role-capable PostgreSQL admin on database postgres.");
  await assertPortAvailable(5000);
  await assertPortAvailable(5173);
  const apiHost = apiHostProcess = start("dotnet", ["test", "backend/ApiPlayground.sln", "--configuration", "Release", "--filter", "FullyQualifiedName~Story9BrowserHostRunnerTests", "--logger", "console;verbosity=quiet"], repoRoot, {
      APIPLAYGROUND_TEST_ADMIN_CONNECTION: adminConnection,
      APG_STORY9_RUNNER_DIR: runnerDirectory,
      ASPNETCORE_ENVIRONMENT: "Production",
    });
    await waitFor(async () => {
      try { await (await import("node:fs/promises")).access(path.join(runnerDirectory, "ready.json")); return true; }
      catch { if (apiHost.exitCode !== null) await waitExit(apiHost); return false; }
    }, "Story 9 API host readiness", 120_000);
    const fs = await import("node:fs/promises");
    const ready = JSON.parse(await fs.readFile(path.join(runnerDirectory, "ready.json"), "utf8")) as { targetUrl: string };

    const vite = start(process.execPath, [path.join(frontendRoot, "node_modules", "vite", "bin", "vite.js"), "--host", "127.0.0.1", "--port", "5173", "--strictPort"], frontendRoot, {
      APIPLAYGROUND_API_ORIGIN: "http://localhost:5000",
      APIPLAYGROUND_VITE_HOST: "127.0.0.1",
    }, true);
    await waitFor(async () => {
      try { return (await fetch("http://127.0.0.1:5173/")).ok; }
      catch { if (vite.exitCode !== null) await waitExit(vite); return false; }
    }, "Vite readiness");

    const spec = path.join(frontendRoot, "e2e", "story9-mvp.spec.ts");
    const playwrightConfig = path.join(runnerDirectory, "playwright.config.ts");
    await fs.writeFile(playwrightConfig, `export default { testDir: ${JSON.stringify(path.dirname(spec))}, testMatch: ${JSON.stringify(path.basename(spec))}, use: { baseURL: 'http://127.0.0.1:5173', browserName: 'chromium', headless: true } };\n`);
    const testProcess = start(process.execPath, [path.join(frontendRoot, "node_modules", "@playwright", "test", "cli.js"), "test", "--config", playwrightConfig], frontendRoot, {
      APG_STORY9_TARGET_URL: ready.targetUrl,
      APG_STORY9_RUNNER_DIR: runnerDirectory,
    }, true);
    const testExit = waitExit(testProcess);
    await waitFor(async () => {
      try { return (await fetch("http://127.0.0.1:5173/")).ok; }
      catch { if (testProcess.exitCode !== null) await testExit; return false; }
    }, "Playwright's browser-facing Vite server");
    if (await testExit !== 0) throw new Error("Story 9 Playwright journey failed.");
    await fs.writeFile(path.join(runnerDirectory, "shutdown"), "stop");
    if (await waitExit(apiHost) !== 0) throw new Error("Story 9 API host runner failed.");
} finally {
  await cleanup();
}
