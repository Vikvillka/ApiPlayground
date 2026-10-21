import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

afterEach(() => vi.unstubAllGlobals());

describe("GET tracer workspace", () => {
  it("starts with blank URL and disabled Send", () => {
    render(<App />);

    expect(screen.getByLabelText("Request URL")).toHaveValue("");
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    expect(screen.getByText("Enter a URL to send a request.")).toBeInTheDocument();
  });

  it("shows an observed response from the tracer API", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None",
      statusCode: 200,
      elapsedMilliseconds: 12,
      body: "hello",
      persistenceVerified: true,
      error: null,
      requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);

    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(screen.getByText("hello")).toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledWith("/api/tracer/get", expect.objectContaining({ method: "POST" }));
  });

  it("clears an earlier result before reporting a later send failure", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        failure: "None", statusCode: 200, elapsedMilliseconds: 12, body: "old response",
        persistenceVerified: true, error: null, requestSent: true,
      }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockRejectedValueOnce(new Error("network unavailable"));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    const urlInput = screen.getByLabelText("Request URL");
    fireEvent.change(urlInput, { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    expect(urlInput).toBeDisabled();
    await waitFor(() => expect(screen.getByText("old response")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("network unavailable"));
    expect(screen.queryByText("old response")).not.toBeInTheDocument();
  });

  it("does not allow another submission while a request is in flight", async () => {
    let finishRequest!: (response: Response) => void;
    const fetchMock = vi.fn().mockReturnValue(new Promise<Response>(resolve => { finishRequest = resolve; }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    const urlInput = screen.getByLabelText("Request URL");
    fireEvent.change(urlInput, { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(urlInput).toBeDisabled();
    fireEvent.submit(screen.getByRole("button", { name: "Sending…" }).closest("form")!);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    finishRequest(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 1, body: "done",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    await waitFor(() => expect(screen.getByText("done")).toBeInTheDocument());
  });
});