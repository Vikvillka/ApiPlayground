import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  getTracerCollections,
  getTracerHistory,
  saveTracerRequest,
  sendTracerRequest,
} from "../api/tracerApi";
import type { HistoryItem, RequestCollection, TracerResponse } from "../tracer";
import { RootStore } from "./RootStore";

vi.mock("../api/tracerApi", () => ({
  sendTracerRequest: vi.fn(),
  getTracerHistory: vi.fn(),
  getTracerCollections: vi.fn(),
  saveTracerRequest: vi.fn(),
}));

const sendMock = vi.mocked(sendTracerRequest);
const historyMock = vi.mocked(getTracerHistory);
const collectionsMock = vi.mocked(getTracerCollections);
const saveMock = vi.mocked(saveTracerRequest);

const okResponse = (overrides: Partial<TracerResponse> = {}): TracerResponse => ({
  failure: "",
  statusCode: 200,
  elapsedMilliseconds: 12,
  headers: {},
  body: "{}",
  error: null,
  requestSent: true,
  historySaved: true,
  ...overrides,
});

const collection: RequestCollection = { id: "c1", name: "Main", requests: [] };

function filledStore(): RootStore {
  const root = new RootStore();
  root.editor.setUrl("https://api.example.com/users");
  return root;
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("RequestEditorStore", () => {
  it("cannot send an empty URL", () => {
    const root = new RootStore();
    expect(root.editor.canSend).toBe(false);
  });

  it("blocks sending when a hidden tab holds invalid input", () => {
    const root = filledStore();
    root.editor.setBody("{");
    root.editor.setMethod("POST");
    root.editor.setTab("Params");
    expect(root.editor.canSend).toBe(false);
    expect(root.editor.hiddenValidationError).toBe("Fix the Body input before sending.");
  });

  it("builds the tracer request from enabled rows and includes the body only for body methods", () => {
    const root = filledStore();
    root.editor.updateParam(1, { key: "page", value: "2" });
    root.editor.addParam();
    root.editor.updateParam(3, { key: "off", value: "x", enabled: false });
    root.editor.updateHeader(2, { key: "Accept", value: "application/json" });
    root.editor.setMethod("GET");
    root.editor.setBody('{"a":1}');

    expect(root.editor.tracerRequest).toEqual({
      url: "https://api.example.com/users?page=2",
      method: "GET",
      headers: { Accept: "application/json" },
    });

    root.editor.setMethod("POST");
    expect(root.editor.tracerRequest.body).toBe('{"a":1}');
  });

  it("restores a history item with redacted values blanked and disabled", () => {
    const root = new RootStore();
    root.restoreRequest({
      url: "https://example.com/a?token=secret&page=1",
      method: "POST",
      body: '{"x":1}',
      headers: [{ name: "Authorization", value: "", redacted: true }],
      redactedQueryParameters: ["token"],
    });

    expect(root.editor.url).toBe("https://example.com/a");
    expect(root.editor.params).toEqual([
      { id: 3, key: "token", value: "", enabled: false, redacted: true },
      { id: 4, key: "page", value: "1", enabled: true, redacted: false },
    ]);
    expect(root.editor.headers[0]).toMatchObject({ key: "Authorization", enabled: false, redacted: true });
    expect(root.editor.method).toBe("POST");
    expect(root.editor.body).toBe('{"x":1}');
    expect(root.editor.tab).toBe("Params");
  });
});

describe("TracerStore", () => {
  it("sends the built request and stores the result on the Body tab", async () => {
    sendMock.mockResolvedValue(okResponse());
    const root = filledStore();
    await root.tracer.send();

    expect(sendMock).toHaveBeenCalledWith({
      url: "https://api.example.com/users",
      method: "GET",
      headers: {},
    });
    expect(root.tracer.result?.statusCode).toBe(200);
    expect(root.tracer.responseTab).toBe("Body");
    expect(root.tracer.loading).toBe(false);
  });

  it("marks the previous result while a new request is in flight", async () => {
    sendMock.mockResolvedValueOnce(okResponse());
    const root = filledStore();
    await root.tracer.send();
    sendMock.mockRejectedValueOnce(new Error("Network down"));
    await root.tracer.send();

    expect(root.tracer.previousResult).toBe(true);
    expect(root.tracer.error).toBe("Network down");
    expect(root.tracer.loading).toBe(false);
  });

  it("does not show PolicyRejected as an error but shows other response errors", async () => {
    const root = filledStore();
    sendMock.mockResolvedValueOnce(okResponse({ failure: "PolicyRejected", error: "Blocked" }));
    await root.tracer.send();
    expect(root.tracer.error).toBeNull();

    sendMock.mockResolvedValueOnce(okResponse({ failure: "Timeout", error: "Timed out" }));
    await root.tracer.send();
    expect(root.tracer.error).toBe("Timed out");
  });

  it("shows a notice when the history entry was not saved", async () => {
    sendMock.mockResolvedValue(okResponse({ historySaved: false }));
    const root = filledStore();
    await root.tracer.send();
    expect(root.tracer.notice).toBe("The request was sent, but its History entry was not saved.");
  });

  it("refreshes history after a send only when History is open", async () => {
    sendMock.mockResolvedValue(okResponse());
    historyMock.mockResolvedValue([]);
    const root = filledStore();
    await root.tracer.send();
    expect(historyMock).not.toHaveBeenCalled();

    root.history.open = true;
    await root.tracer.send();
    expect(historyMock).toHaveBeenCalledTimes(1);
  });
});

describe("HistoryStore", () => {
  it("loads history and collections when opened and clears the open state when closed", async () => {
    const item = { id: "h1" } as HistoryItem;
    historyMock.mockResolvedValue([item]);
    collectionsMock.mockResolvedValue([collection]);
    const root = new RootStore();

    await root.history.toggle();
    expect(root.history.open).toBe(true);
    expect(root.history.items).toEqual([item]);
    expect(root.history.collections).toEqual([collection]);
    expect(root.history.loading).toBe(false);

    await root.history.toggle();
    expect(root.history.open).toBe(false);
  });

  it("keeps history and collection errors separate", async () => {
    historyMock.mockRejectedValue(new Error("History down"));
    collectionsMock.mockRejectedValue(new Error("Collections down"));
    const root = new RootStore();

    await root.history.toggle();
    expect(root.history.error).toBe("History down");
    expect(root.history.collectionError).toBe("Collections down");
  });
});

describe("SaveRequestStore", () => {
  it("opens with a default title and creates a collection when none exist", async () => {
    collectionsMock.mockResolvedValue([]);
    const root = filledStore();
    root.editor.setMethod("POST");
    await root.saveRequest.openDialog();

    expect(root.saveRequest.open).toBe(true);
    expect(root.saveRequest.title).toBe("POST /users");
    expect(root.saveRequest.createCollection).toBe(true);
    expect(root.saveRequest.canSubmit).toBe(false);
  });

  it("saves to the chosen collection, closes, sets a notice, and refreshes collections", async () => {
    collectionsMock.mockResolvedValue([collection]);
    saveMock.mockResolvedValue({} as never);
    const root = filledStore();
    await root.saveRequest.openDialog();
    root.saveRequest.setTitle("My call");
    await root.saveRequest.save();

    expect(saveMock).toHaveBeenCalledWith(expect.objectContaining({
      collectionId: "c1",
      newCollectionName: null,
      title: "My call",
      url: "https://api.example.com/users",
    }));
    expect(root.saveRequest.open).toBe(false);
    expect(root.tracer.notice).toBe("Request saved to Collections.");
    expect(collectionsMock).toHaveBeenCalledTimes(2);
  });

  it("stays open and shows the error when saving fails", async () => {
    collectionsMock.mockResolvedValue([collection]);
    saveMock.mockRejectedValue(new Error("Duplicate title"));
    const root = filledStore();
    await root.saveRequest.openDialog();
    await root.saveRequest.save();

    expect(root.saveRequest.open).toBe(true);
    expect(root.saveRequest.error).toBe("Duplicate title");
    expect(root.saveRequest.saving).toBe(false);
  });

  it("does not close while a save is in progress", async () => {
    collectionsMock.mockResolvedValue([collection]);
    const root = filledStore();
    await root.saveRequest.openDialog();
    root.saveRequest.saving = true;
    root.saveRequest.close();
    expect(root.saveRequest.open).toBe(true);
  });
});
