import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

afterEach(() => vi.unstubAllGlobals());

describe("request workspace", () => {
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
      headers: { "Content-Type": ["application/json"], "X-Observed": ["first", "second"] },
      body: "{\"hello\":\"world\",\"count\":2}",
      persistenceVerified: true,
      error: null,
      requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);

    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(screen.getByText(/HTTP 200/)).toBeInTheDocument());
    expect(screen.getByRole("status")).toHaveTextContent("HTTP 20012 ms");
    expect(screen.getByText("12 ms")).toBeInTheDocument();
    expect(within(screen.getByRole("tablist", { name: "Response details" })).getByRole("tab", { name: "Body" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText(/"hello": "world"/)).toBeInTheDocument();
    expect(screen.queryByText("first")).not.toBeInTheDocument();
    const responseTabs = within(screen.getByRole("tablist", { name: "Response details" }));
    fireEvent.click(responseTabs.getByRole("tab", { name: "Headers" }));
    expect(responseTabs.getByRole("tab", { name: "Headers" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getAllByText("first")).toHaveLength(1);
    expect(screen.getByText("second")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Params" })).toHaveAttribute("aria-selected", "true");
    fireEvent.click(responseTabs.getByRole("tab", { name: "Body" }));
    expect(responseTabs.getByRole("tab", { name: "Body" })).toHaveAttribute("aria-selected", "true");
    expect(fetchMock).toHaveBeenCalledWith("/api/tracer/get", expect.objectContaining({ method: "POST" }));
    expect(responseTabs.getByRole("tab", { name: "Body" })).toHaveAttribute("aria-controls", "response-panel-body");
    expect(screen.getByRole("tabpanel", { name: "Body" })).toHaveAttribute("aria-labelledby", "response-tab-body");
    const headersTab = responseTabs.getByRole("tab", { name: "Headers" });
    fireEvent.keyDown(responseTabs.getByRole("tab", { name: "Body" }), { key: "ArrowRight" });
    expect(headersTab).toHaveFocus();
    expect(headersTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel", { name: "Headers" })).toHaveAttribute("aria-labelledby", "response-tab-headers");
    expect(screen.getAllByText("first")).toHaveLength(1);
    expect(screen.getByText("second")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Params" })).toHaveAttribute("aria-selected", "true");
    fireEvent.click(responseTabs.getByRole("tab", { name: "Body" }));
    expect(responseTabs.getByRole("tab", { name: "Body" })).toHaveAttribute("aria-selected", "true");
  });

  it("shows HTTP error statuses as completed responses with headers and body", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 404, elapsedMilliseconds: 8,
      headers: { "X-Reason": ["not found"] }, body: "{\"message\":\"missing\"}",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/missing" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(screen.getByText(/HTTP 404/)).toBeInTheDocument());
    expect(screen.getByText(/"message": "missing"/)).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    fireEvent.click(within(screen.getByRole("tablist", { name: "Response details" })).getByRole("tab", { name: "Headers" }));
    expect(screen.getByText("not found")).toBeInTheDocument();
  });

  it("keeps policy rejection distinct and does not fabricate HTTP response details", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "PolicyRejected", statusCode: null, elapsedMilliseconds: 0, headers: {}, body: "",
      persistenceVerified: false, error: "Blocked by security policy. Request not sent.", requestSent: false,
    }), { status: 403, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Request not sent");
    expect(screen.queryByRole("tablist", { name: "Response details" })).not.toBeInTheDocument();
    expect(screen.queryByText(/HTTP \d/)).not.toBeInTheDocument();
  });

  it("shows transport failures accessibly without inventing response details", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new Error("The total request deadline expired."));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("The total request deadline expired.");
    expect(screen.queryByRole("tablist", { name: "Response details" })).not.toBeInTheDocument();
    expect(screen.queryByText(/HTTP \d/)).not.toBeInTheDocument();
  });

  it("retains and labels an earlier result during a later send and failure", async () => {
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
    expect(screen.getByText("old response")).toBeInTheDocument();
    expect(screen.getByText("This result is for the previous request.")).toBeInTheDocument();
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

  it("serializes the selected method, enabled repeated query rows, headers, and JSON body", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 4, body: "{}",
      persistenceVerified: true, error: null, requestSent: true, headers: {},
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/path?existing=one" } });
    fireEvent.change(screen.getByLabelText("Request method"), { target: { value: "PATCH" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "tag" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "new value" } });
    fireEvent.click(screen.getByRole("button", { name: "Add parameter" }));
    const paramKeys = screen.getAllByLabelText("Parameter key");
    const paramValues = screen.getAllByLabelText("Parameter value");
    fireEvent.change(paramKeys[1], { target: { value: "tag" } });
    fireEvent.change(paramValues[1], { target: { value: "second" } });
    fireEvent.click(screen.getAllByLabelText("Enabled")[1]);
    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    fireEvent.change(screen.getByLabelText("Header key"), { target: { value: "X-Example" } });
    fireEvent.change(screen.getByLabelText("Header value"), { target: { value: "header-value" } });
    fireEvent.click(screen.getByRole("tab", { name: "Body" }));
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "{\"updated\":true}" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const sent = JSON.parse(fetchMock.mock.calls[0][1].body as string);
    expect(sent.method).toBe("PATCH");
    expect(sent.url).toBe("https://example.test/path?existing=one&tag=new+value");
    expect(sent.headers).toEqual({ "X-Example": "header-value" });
    expect(sent.body).toBe("{\"updated\":true}");
    expect(sent).not.toHaveProperty("parameters");
  });

  it("preserves URL, row, and body values when switching tabs", () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "keep" } });
    fireEvent.click(screen.getByRole("tab", { name: "Body" }));
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "{\"kept\":1}" } });
    fireEvent.click(screen.getByRole("tab", { name: "Params" }));
    expect(screen.getByLabelText("Request URL")).toHaveValue("https://example.test");
    expect(screen.getByLabelText("Parameter key")).toHaveValue("keep");
    fireEvent.click(screen.getByRole("tab", { name: "Body" }));
    expect(screen.getByLabelText("JSON body")).toHaveValue("{\"kept\":1}");
  });

  it("blocks invalid URL, invalid JSON, disallowed body method, and duplicate enabled headers", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "ftp://example.test" } });
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("tab", { name: "Body" }));
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "{bad}" } });
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "{\"ok\":true}" } });
    fireEvent.change(screen.getByLabelText("Request method"), { target: { value: "GET" } });
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "" } });
    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    fireEvent.change(screen.getByLabelText("Header key"), { target: { value: "X-Duplicate" } });
    fireEvent.click(screen.getByRole("button", { name: "Add header" }));
    fireEvent.change(screen.getAllByLabelText("Header key")[1], { target: { value: "x-duplicate" } });
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("explains a disabled Send action when validation is in another tab", () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("tab", { name: "Body" }));
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "{broken" } });
    fireEvent.click(screen.getByRole("tab", { name: "Params" }));

    const sendButton = screen.getByRole("button", { name: "Send" });
    expect(sendButton).toBeDisabled();
    expect(sendButton).toHaveAccessibleDescription("Fix the Body input before sending.");
    expect(screen.getByRole("alert")).toHaveTextContent("Fix the Body input before sending.");
  });

  it("keeps disabled row values out of the serialized request", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 204, elapsedMilliseconds: 1, body: "",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "skip" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "param" } });
    fireEvent.click(screen.getByLabelText("Enabled"));
    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    fireEvent.change(screen.getByLabelText("Header key"), { target: { value: "X-Skip" } });
    fireEvent.change(screen.getByLabelText("Header value"), { target: { value: "header" } });
    fireEvent.click(screen.getAllByLabelText("Enabled")[0]);
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const sent = JSON.parse(fetchMock.mock.calls[0][1].body as string);
    expect(sent.url).toBe("https://example.test/");
    expect(sent.url).not.toContain("skip");
    expect(sent.headers).toEqual({});
    expect(screen.getByLabelText("Header value")).toHaveValue("header");
  });

  it("preserves repeated enabled query parameter names", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 1, body: "",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/?existing=1" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "tag" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "first value" } });
    fireEvent.click(screen.getByRole("button", { name: "Add parameter" }));
    fireEvent.change(screen.getAllByLabelText("Parameter key")[1], { target: { value: "tag" } });
    fireEvent.change(screen.getAllByLabelText("Parameter value")[1], { target: { value: "second" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const sent = JSON.parse(fetchMock.mock.calls[0][1].body as string);
    expect(sent.url).toBe("https://example.test/?existing=1&tag=first+value&tag=second");
  });

  it("blocks a final URL longer than 2048 characters after Params are added", () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "p" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "x".repeat(2048) } });

    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    expect(screen.getByRole("alert")).toHaveTextContent("2048 characters or fewer");
  });

  it("keeps the previous response visible and labels it while a send is pending", async () => {
    let finishRequest!: (response: Response) => void;
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        failure: "None", statusCode: 200, elapsedMilliseconds: 12, body: "prior",
        persistenceVerified: true, error: null, requestSent: true,
      }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockReturnValueOnce(new Promise<Response>(resolve => { finishRequest = resolve; }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    await waitFor(() => expect(screen.getByText("prior")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    expect(screen.getByText("prior")).toBeInTheDocument();
    expect(screen.getByText("This result is for the previous request.")).toBeInTheDocument();
    finishRequest(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 2, body: "current",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    await waitFor(() => expect(screen.getByText("current")).toBeInTheDocument());
  });

  it("explains a disabled Send action when validation is in another tab", () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("tab", { name: "Body" }));
    fireEvent.change(screen.getByLabelText("JSON body"), { target: { value: "{broken" } });
    fireEvent.click(screen.getByRole("tab", { name: "Params" }));

    const sendButton = screen.getByRole("button", { name: "Send" });
    expect(sendButton).toBeDisabled();
    expect(sendButton).toHaveAccessibleDescription("Fix the Body input before sending.");
    expect(screen.getByRole("alert")).toHaveTextContent("Fix the Body input before sending.");
  });

  it("keeps disabled row values out of the serialized request", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 204, elapsedMilliseconds: 1, body: "",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "skip" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "param" } });
    fireEvent.click(screen.getByLabelText("Enabled"));
    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    fireEvent.change(screen.getByLabelText("Header key"), { target: { value: "X-Skip" } });
    fireEvent.change(screen.getByLabelText("Header value"), { target: { value: "header" } });
    fireEvent.click(screen.getAllByLabelText("Enabled")[0]);
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const sent = JSON.parse(fetchMock.mock.calls[0][1].body as string);
    expect(sent.url).toBe("https://example.test/");
    expect(sent.url).not.toContain("skip");
    expect(sent.headers).toEqual({});
    expect(screen.getByLabelText("Header value")).toHaveValue("header");
  });

  it("preserves repeated enabled query parameter names", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 1, body: "",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/?existing=1" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "tag" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "first value" } });
    fireEvent.click(screen.getByRole("button", { name: "Add parameter" }));
    fireEvent.change(screen.getAllByLabelText("Parameter key")[1], { target: { value: "tag" } });
    fireEvent.change(screen.getAllByLabelText("Parameter value")[1], { target: { value: "second" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const sent = JSON.parse(fetchMock.mock.calls[0][1].body as string);
    expect(sent.url).toBe("https://example.test/?existing=1&tag=first+value&tag=second");
  });

  it("blocks a final URL longer than 2048 characters after Params are added", () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "p" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "x".repeat(2048) } });

    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
    expect(screen.getByRole("alert")).toHaveTextContent("2048 characters or fewer");
  });

  it("keeps the previous response visible and labels it while a send is pending", async () => {
    let finishRequest!: (response: Response) => void;
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        failure: "None", statusCode: 200, elapsedMilliseconds: 12, body: "prior",
        persistenceVerified: true, error: null, requestSent: true,
      }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockReturnValueOnce(new Promise<Response>(resolve => { finishRequest = resolve; }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    await waitFor(() => expect(screen.getByText("prior")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    expect(screen.getByText("prior")).toBeInTheDocument();
    expect(screen.getByText("This result is for the previous request.")).toBeInTheDocument();
    finishRequest(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 2, body: "current",
      persistenceVerified: true, error: null, requestSent: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    await waitFor(() => expect(screen.getByText("current")).toBeInTheDocument());
  });
});