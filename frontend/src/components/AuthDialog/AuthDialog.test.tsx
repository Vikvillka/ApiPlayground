import { fireEvent, render, screen, waitFor, waitForElementToBeRemoved } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TopBar } from "../TopBar/TopBar";

afterEach(() => vi.unstubAllGlobals());

function renderTopBar() {
  render(<TopBar historyOpen={false} onToggleHistory={() => undefined} />);
}

function jsonResponse(body: unknown, status: number) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function fillForm(email: string, password: string) {
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: email } });
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: password } });
}

describe("auth screens", () => {
  it("registers an account, then returns to the log in form with a notice", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(jsonResponse({ id: "11111111-1111-1111-1111-111111111111" }, 201));
    vi.stubGlobal("fetch", fetchMock);
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log in" }));
    fireEvent.click(screen.getByRole("button", { name: "Create an account" }));
    fillForm("  user@example.test ", "longenough");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Account created. Log in to continue.");
    expect(fetchMock.mock.calls[0][0]).toBe("/api/auth/register");
    expect(fetchMock.mock.calls[0][1].method).toBe("POST");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ email: "user@example.test", password: "longenough" });
    expect(screen.getByRole("heading", { name: "Log in" })).toBeInTheDocument();
  });

  it("keeps a duplicate email error inside the registration form", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(jsonResponse({
      type: "about:blank", title: "Conflict", status: 409, detail: "An account with this email already exists.",
    }, 409)));
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log in" }));
    fireEvent.click(screen.getByRole("button", { name: "Create an account" }));
    fillForm("taken@example.test", "longenough");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("An account with this email already exists.");
    expect(screen.getByRole("heading", { name: "Create account" })).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveValue("taken@example.test");
  });

  it("shows the generic invalid credentials message and keeps the dialog open", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(jsonResponse({
      type: "about:blank", title: "Unauthorized", status: 401, detail: "Invalid email or password.",
    }, 401)));
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log in" }));
    fillForm("user@example.test", "wrong-password");
    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
    expect(screen.getByRole("heading", { name: "Log in" })).toBeInTheDocument();
  });

  it("closes the dialog after a successful log in", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(jsonResponse({ id: "11111111-1111-1111-1111-111111111111" }, 200));
    vi.stubGlobal("fetch", fetchMock);
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log in" }));
    fillForm("user@example.test", "longenough");
    const dialog = screen.getByRole("dialog");
    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    await waitForElementToBeRemoved(dialog);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/auth/login");
  });

  it("keeps the submit disabled while the registration password is shorter than 8 characters", () => {
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log in" }));
    fireEvent.click(screen.getByRole("button", { name: "Create an account" }));
    fillForm("user@example.test", "short");

    expect(screen.getByRole("button", { name: "Create account" })).toBeDisabled();
  });

  it("posts log out and reports the result", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log out" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Logged out.");
    expect(fetchMock.mock.calls[0][0]).toBe("/api/auth/logout");
    expect(fetchMock.mock.calls[0][1].method).toBe("POST");
  });

  it("shows a log out failure without breaking the top bar", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValueOnce(new Error("Network down.")));
    renderTopBar();

    fireEvent.click(screen.getByRole("button", { name: "Log out" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Network down."));
    expect(screen.getByRole("button", { name: "Log in" })).toBeEnabled();
  });
});
