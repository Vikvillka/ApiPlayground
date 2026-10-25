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

  it("links request tabs to their panels and prevents History loading during a send", async () => {
    let completeSend!: (response: Response) => void;
    const fetchMock = vi.fn()
      .mockImplementationOnce(() => new Promise<Response>(resolve => { completeSend = resolve; }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: "history-1", createdAt: "2026-10-23T12:00:00Z", method: "GET", url: "https://example.test/previous",
        redactedQueryParameters: [], headers: [], redactedHeaders: [], body: null, failure: "None", statusCode: 200, elapsedMilliseconds: 2,
      }]), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);

    const selectedTab = screen.getByRole("tab", { name: "Params" });
    expect(document.getElementById(selectedTab.getAttribute("aria-controls")!)).toHaveAttribute("aria-labelledby", selectedTab.id);

    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/current" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    fireEvent.click(screen.getByRole("button", { name: "Show History & Collections" }));
    const item = await screen.findByRole("button", { name: "Load GET /previous" });
    expect(item).toBeDisabled();

    completeSend(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 1, headers: {}, body: "current",
      persistenceVerified: true, error: null, requestSent: true, historySaved: true,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    expect(await screen.findByText("current")).toBeInTheDocument();
  });

  it("loads History items without sending and clears the previous response", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        failure: "None", statusCode: 200, elapsedMilliseconds: 1, headers: {}, body: "prior",
        persistenceVerified: true, error: null, requestSent: true, historySaved: true,
      }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: "history-1", createdAt: "2026-10-23T12:00:00Z", method: "POST",
        url: "https://example.test/path?tag=one&access_token=secret",
        redactedQueryParameters: ["access_token"],
        headers: [{ name: "Authorization", value: "", redacted: true }, { name: "X-Visible", value: "shown", redacted: false }],
        redactedHeaders: ["Authorization"], body: "{\"saved\":true}", failure: "None", statusCode: 201, elapsedMilliseconds: 2,
      }]), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        failure: "None", statusCode: 201, elapsedMilliseconds: 3, headers: {}, body: "replayed",
        persistenceVerified: true, error: null, requestSent: true, historySaved: true,
      }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: "history-2", createdAt: "2026-10-23T12:01:00Z", method: "POST",
        url: "https://example.test/path", redactedQueryParameters: [], headers: [], redactedHeaders: [],
        body: null, failure: "None", statusCode: 201, elapsedMilliseconds: 3,
      }]), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/first" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    await waitFor(() => expect(screen.getByText("prior")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Show History & Collections" }));
    expect(await screen.findByRole("button", { name: "Load POST /path" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Load POST /path" }));

    expect(screen.getByLabelText("Request URL")).toHaveValue("https://example.test/path");
    expect(screen.getByLabelText("Request method")).toHaveValue("POST");
    expect(screen.getAllByLabelText("Parameter key").map(input => (input as HTMLInputElement).value)).toEqual(["tag", "access_token"]);
    expect(screen.getAllByLabelText("Parameter value").map(input => (input as HTMLInputElement).value)).toEqual(["one", ""]);
    expect(screen.getAllByLabelText("Enabled")[1]).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/tracer/get");
    expect(fetchMock.mock.calls[1][0]).toBe("/api/tracer/history");
    expect(fetchMock.mock.calls[2][0]).toBe("/api/tracer/collections");
    expect(screen.queryByText("prior")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    expect(screen.getAllByLabelText("Header value")[0]).not.toBeDisabled();
    expect(screen.getAllByLabelText("Header value")[0]).toHaveAttribute("placeholder", "Value not saved — enter it again");
    expect(screen.getAllByLabelText("Header value")[1]).toHaveValue("shown");
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    expect(await screen.findByText("replayed")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(5);
    expect(fetchMock.mock.calls[3][0]).toBe("/api/tracer/get");
    const replayed = JSON.parse(fetchMock.mock.calls[3][1].body as string);
    expect(fetchMock.mock.calls[4][0]).toBe("/api/tracer/history");
    expect(replayed.url).toBe("https://example.test/path?tag=one");
  });

  it("reports that a dispatched request's History entry could not be saved", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      failure: "None", statusCode: 200, elapsedMilliseconds: 1, headers: {}, body: "ok",
      persistenceVerified: true, error: null, requestSent: true, historySaved: false,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    expect(await screen.findByText("The request was sent, but its History entry was not saved.")).toBeInTheDocument();
    expect(screen.getByText("ok")).toBeInTheDocument();
  });

  it("saves explicitly to a new collection and preserves the editor without sending", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: "saved-1", createdAt: "2026-10-25T12:00:00Z", collectionId: "collection-1", collectionName: "Team",
        title: "PATCH /items", method: "PATCH", url: "https://example.test/items?keep=yes",
        headers: [{ name: "Authorization", value: "", redacted: true }, { name: "X-Keep", value: "visible", redacted: false }],
        redactedHeaders: ["Authorization"], body: "{\"ok\":true}",
      }), { status: 201, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: "collection-1", name: "Team", requests: [{
          id: "saved-1", createdAt: "2026-10-25T12:00:00Z", collectionId: "collection-1", collectionName: "Team",
          title: "PATCH /items", method: "PATCH", url: "https://example.test/items?keep=yes",
          headers: [{ name: "Authorization", value: "", redacted: true }, { name: "X-Keep", value: "visible", redacted: false }],
          redactedHeaders: ["Authorization"], body: "{\"ok\":true}",
        }],
      }]), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: "collection-1", name: "Team", requests: [{
          id: "saved-1", createdAt: "2026-10-25T12:00:00Z", collectionId: "collection-1", collectionName: "Team",
          title: "PATCH /items", method: "PATCH", url: "https://example.test/items?keep=yes",
          headers: [{ name: "Authorization", value: "", redacted: true }, { name: "X-Keep", value: "visible", redacted: false }],
          redactedHeaders: ["Authorization"], body: "{\"ok\":true}",
        }],
      }]), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/items?keep=yes" } });
    fireEvent.change(screen.getByLabelText("Request method"), { target: { value: "PATCH" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "added" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "value" } });
    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    fireEvent.change(screen.getByLabelText("Header key"), { target: { value: "Authorization" } });
    fireEvent.change(screen.getByLabelText("Header value"), { target: { value: "Bearer private" } });
    fireEvent.click(screen.getByRole("button", { name: "Save to collection" }));

    const dialog = await screen.findByRole("dialog", { name: "Save to collection" });
    expect(screen.getByLabelText("Request title")).toHaveValue("PATCH /items");
    expect(screen.getByLabelText("Request title")).toHaveFocus();
    fireEvent.keyDown(screen.getByLabelText("Request title"), { key: "Tab", shiftKey: true });
    expect(within(dialog).getByRole("button", { name: "Cancel" })).toHaveFocus();
    fireEvent.keyDown(within(dialog).getByRole("button", { name: "Cancel" }), { key: "Tab" });
    expect(screen.getByLabelText("Request title")).toHaveFocus();
    fireEvent.keyDown(screen.getByLabelText("Request title"), { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("button", { name: "Save to collection" })).toHaveFocus());
    fireEvent.click(screen.getByRole("button", { name: "Save to collection" }));
    const reopenedDialog = await screen.findByRole("dialog", { name: "Save to collection" });
    expect(screen.getByLabelText("Request title")).toHaveValue("PATCH /items");
    fireEvent.change(screen.getByLabelText("Request title"), { target: { value: "Update item" } });
    fireEvent.change(screen.getByLabelText("New collection name"), { target: { value: "Team" } });
    fireEvent.click(within(reopenedDialog).getByRole("button", { name: "Save request" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/tracer/collections");
    expect(fetchMock.mock.calls[1][0]).toBe("/api/tracer/collections/requests");
    const saved = JSON.parse(fetchMock.mock.calls[1][1].body as string);
    expect(saved).toMatchObject({ collectionId: null, newCollectionName: "Team", title: "Update item", method: "PATCH" });
    expect(saved.headers).toEqual({ Authorization: "Bearer private" });
    expect(fetchMock.mock.calls[2][0]).toBe("/api/tracer/collections");
    expect(screen.getByLabelText("Request URL")).toHaveValue("https://example.test/items?keep=yes");
    expect(screen.getByText("Request saved to Collections.")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("button", { name: "Save to collection" })).toHaveFocus());

    fireEvent.click(screen.getByRole("button", { name: "Show History & Collections" }));
    expect(await screen.findByRole("button", { name: "Load saved PATCH PATCH /items" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Load saved PATCH PATCH /items" }));
    expect(screen.getByLabelText("Request URL")).toHaveValue("https://example.test/items");
    expect(screen.getByLabelText("Request method")).toHaveValue("PATCH");
    expect(screen.queryByText("Request saved to Collections.")).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(5);
    expect(fetchMock.mock.calls[3][0]).toBe("/api/tracer/history");
    fireEvent.click(screen.getByRole("tab", { name: "Headers" }));
    const requestPanel = screen.getByRole("tabpanel", { name: "Headers" });
    expect(within(requestPanel).getAllByLabelText("Header value")[0]).toHaveValue("");
    expect(within(requestPanel).getAllByLabelText("Header value")[0]).toHaveAttribute("placeholder", "Value not saved — enter it again");
    expect(within(requestPanel).getAllByLabelText("Enabled")[0]).toBeDisabled();
  });

  it("keeps save dialog values and request editor intact after failure for retry", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ error: "The request could not be saved. Your request is still available to retry." }), { status: 503, headers: { "Content-Type": "application/problem+json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: "saved-2", createdAt: "2026-10-25T12:00:00Z", collectionId: "collection-2", collectionName: "Retry",
        title: "GET /retry", method: "GET", url: "https://example.test/retry", headers: [], redactedHeaders: [], body: null,
      }), { status: 201, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: "collection-2", name: "Retry", requests: [] }]), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/retry" } });
    fireEvent.change(screen.getByLabelText("Parameter key"), { target: { value: "kept" } });
    fireEvent.change(screen.getByLabelText("Parameter value"), { target: { value: "value" } });
    fireEvent.click(screen.getByRole("button", { name: "Save to collection" }));
    await screen.findByRole("dialog", { name: "Save to collection" });
    fireEvent.change(screen.getByLabelText("Request title"), { target: { value: "Retry title" } });
    fireEvent.change(screen.getByLabelText("New collection name"), { target: { value: "Retry" } });
    fireEvent.click(screen.getByRole("button", { name: "Save request" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("still available to retry");
    expect(screen.getByLabelText("Request title")).toHaveValue("Retry title");
    expect(screen.getByLabelText("New collection name")).toHaveValue("Retry");
    expect(screen.getByLabelText("Parameter key")).toHaveValue("kept");
    expect(screen.getByLabelText("Parameter value")).toHaveValue("value");
    fireEvent.click(screen.getByRole("button", { name: "Save request" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledTimes(4);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/tracer/collections");
    expect(fetchMock.mock.calls[1][0]).toBe("/api/tracer/collections/requests");
    expect(fetchMock.mock.calls[2][0]).toBe("/api/tracer/collections/requests");
    expect(fetchMock.mock.calls[1][1].body).toBe(fetchMock.mock.calls[2][1].body);
  });

  it("saves into an existing collection using the editable title", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: "collection-1", name: "Existing", requests: [] }]), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: "saved-3" }), { status: 201, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: "collection-1", name: "Existing", requests: [] }]), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.click(screen.getByRole("button", { name: "Show History & Collections" }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/path" } });
    fireEvent.click(screen.getByRole("button", { name: "Save to collection" }));
    fireEvent.change(screen.getByLabelText("Request title"), { target: { value: "Custom title" } });
    fireEvent.click(screen.getByRole("button", { name: "Save request" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(JSON.parse(fetchMock.mock.calls[2][1].body as string)).toMatchObject({
      collectionId: "collection-1", newCollectionName: null, title: "Custom title", url: "https://example.test/path",
    });
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