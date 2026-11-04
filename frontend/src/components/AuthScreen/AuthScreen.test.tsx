import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthScreen } from "./AuthScreen";

afterEach(() => vi.unstubAllGlobals());

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function fill(email: string, password: string) {
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: email } });
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: password } });
}

describe("auth screen", () => {
  it("starts on account creation and shows the notice it was given", () => {
    render(<AuthScreen notice="Please sign in again." onSignedIn={vi.fn()} />);

    expect(screen.getByRole("heading", { name: "Create account" })).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Please sign in again.");
  });

  it("requires at least 8 password characters to create an account", () => {
    render(<AuthScreen notice={null} onSignedIn={vi.fn()} />);

    fill("visitor@example.test", "short");
    expect(screen.getByRole("button", { name: "Create account" })).toBeDisabled();

    fill("visitor@example.test", "longenough1");
    expect(screen.getByRole("button", { name: "Create account" })).toBeEnabled();
  });

  it("registers and then asks the visitor to log in", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(json({ id: "user-1" }, 201));
    vi.stubGlobal("fetch", fetchMock);
    render(<AuthScreen notice={null} onSignedIn={vi.fn()} />);

    fill("visitor@example.test", "longenough1");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Account created. Log in to continue.");
    expect(screen.getByRole("heading", { name: "Log in" })).toBeInTheDocument();
    expect(fetchMock.mock.calls[0][0]).toBe("/api/auth/register");
  });

  it("shows a registration error in the form", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(json({ detail: "An account with this email already exists." }, 400)));
    render(<AuthScreen notice={null} onSignedIn={vi.fn()} />);

    fill("visitor@example.test", "longenough1");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("An account with this email already exists.");
  });

  it("logs in and hands the user id to the gate", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(json({ id: "user-42" })));
    const onSignedIn = vi.fn();
    render(<AuthScreen notice={null} onSignedIn={onSignedIn} />);

    fireEvent.click(screen.getByRole("button", { name: "I already have an account" }));
    fill("visitor@example.test", "secret1");
    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    await waitFor(() => expect(onSignedIn).toHaveBeenCalledWith("user-42"));
  });

  it("shows the server message for wrong credentials and stays on the form", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(json({ detail: "Invalid email or password." }, 401)));
    const onSignedIn = vi.fn();
    render(<AuthScreen notice={null} onSignedIn={onSignedIn} />);

    fireEvent.click(screen.getByRole("button", { name: "I already have an account" }));
    fill("visitor@example.test", "wrong-password");
    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
    expect(onSignedIn).not.toHaveBeenCalled();
    expect(screen.getByLabelText("Email")).toHaveValue("visitor@example.test");
  });
});
