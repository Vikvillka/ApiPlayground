import { useState } from "react";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch } from "../../api/apiClient";
import { TopBar } from "../TopBar/TopBar";
import { SessionGate } from "./SessionGate";

afterEach(() => vi.unstubAllGlobals());

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function renderGate() {
  return render(
    <SessionGate>
      <TopBar historyOpen={false} onToggleHistory={() => {}} />
      <p>Workspace content</p>
    </SessionGate>,
  );
}

const signedOut = () => json({ detail: "Authentication is required." }, 401);

// Stands in for workspace state that lives under the signed-in tree.
function Draft() {
  const [value, setValue] = useState("");
  return <input aria-label="Draft" value={value} onChange={event => setValue(event.target.value)} />;
}

describe("session gate", () => {
  it("shows the auth screen and no workspace when there is no session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(signedOut()));
    renderGate();

    expect(await screen.findByRole("heading", { name: "Create account" })).toBeInTheDocument();
    expect(screen.queryByText("Workspace content")).not.toBeInTheDocument();
  });

  it("shows the workspace and the current user for a valid session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(json({ id: "user-1", email: "user1@example.test" })));
    renderGate();

    expect(await screen.findByText("Workspace content")).toBeInTheDocument();
    expect(screen.getByText("Signed in as user1@example.test")).toBeInTheDocument();
  });

  it("returns to the auth screen with a notice when a protected call answers 401", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(json({ id: "user-1", email: "user1@example.test" }))
      .mockResolvedValueOnce(signedOut()));
    renderGate();
    await screen.findByText("Workspace content");

    await act(async () => { await apiFetch("/api/tracer/history", "GET"); });

    expect(await screen.findByRole("heading", { name: "Create account" })).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Please sign in again.");
    expect(screen.queryByText("Workspace content")).not.toBeInTheDocument();
  });

  it("logs out from the top bar and returns to the auth screen", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(json({ id: "user-1", email: "user1@example.test" }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    renderGate();
    await screen.findByText("Signed in as user1@example.test");

    fireEvent.click(screen.getByRole("button", { name: "Log out" }));

    expect(await screen.findByRole("heading", { name: "Create account" })).toBeInTheDocument();
    expect(fetchMock.mock.calls[1][0]).toBe("/api/auth/logout");
    expect(screen.queryByText("Workspace content")).not.toBeInTheDocument();
  });

  it("keeps the session and shows an alert when log out fails", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(json({ id: "user-1", email: "user1@example.test" }))
      .mockResolvedValueOnce(json({ detail: "Log out could not be completed." }, 500)));
    renderGate();
    await screen.findByText("Signed in as user1@example.test");

    fireEvent.click(screen.getByRole("button", { name: "Log out" }));

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByText("Workspace content")).toBeInTheDocument();
  });

  it("logs in from the auth screen and opens the workspace", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(signedOut())
      .mockResolvedValueOnce(json({ id: "user-7", email: "visitor@example.test" })));
    renderGate();
    await screen.findByRole("heading", { name: "Create account" });

    fireEvent.click(screen.getByRole("button", { name: "I already have an account" }));
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "visitor@example.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "secret1" } });
    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByText("Workspace content")).toBeInTheDocument();
    expect(screen.getByText("Signed in as visitor@example.test")).toBeInTheDocument();
  });

  it("clears workspace state on log out, so the next sign-in starts fresh", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(json({ id: "user-1", email: "user1@example.test" }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(json({ id: "user-2", email: "user2@example.test" })));
    render(
      <SessionGate>
        <TopBar historyOpen={false} onToggleHistory={() => {}} />
        <Draft />
      </SessionGate>,
    );
    fireEvent.change(await screen.findByLabelText("Draft"), { target: { value: "unsaved text" } });

    fireEvent.click(screen.getByRole("button", { name: "Log out" }));
    await screen.findByRole("heading", { name: "Create account" });
    fireEvent.click(screen.getByRole("button", { name: "I already have an account" }));
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "user2@example.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "secret1" } });
    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByText("Signed in as user2@example.test")).toBeInTheDocument();
    expect(screen.getByLabelText("Draft")).toHaveValue("");
  });
});
