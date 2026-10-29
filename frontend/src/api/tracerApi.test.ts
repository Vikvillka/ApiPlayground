import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getTracerHistory, saveTracerRequest, sendTracerRequest } from "./tracerApi";

const expectedHeaders = {
  "Content-Type": "application/json",
  "X-ApiPlayground-Request": "1",
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("tracer API request layer", () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends a tracer request with the same URL, method, and headers", async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ failure: "", statusCode: 200, elapsedMilliseconds: 5, headers: {}, body: "", error: null, requestSent: true }),
    );
    const request = { url: "https://example.com", method: "GET" as const, headers: {} };

    await sendTracerRequest(request);

    expect(fetchMock).toHaveBeenCalledWith("/api/tracer/get", {
      method: "POST",
      headers: expectedHeaders,
      body: JSON.stringify(request),
    });
  });

  it("returns an observed non-2xx tracer result instead of throwing", async () => {
    const observed = { failure: "policy", statusCode: null, elapsedMilliseconds: 0, headers: {}, body: "", error: "blocked", requestSent: false };
    fetchMock.mockResolvedValue(jsonResponse(observed, 422));

    await expect(
      sendTracerRequest({ url: "http://localhost", method: "GET", headers: {} }),
    ).resolves.toEqual(observed);
  });

  it("loads history with GET and the same headers", async () => {
    fetchMock.mockResolvedValue(jsonResponse([]));

    await expect(getTracerHistory()).resolves.toEqual([]);

    expect(fetchMock).toHaveBeenCalledWith("/api/tracer/history", {
      method: "GET",
      headers: expectedHeaders,
    });
  });

  it("throws the Problem Details message when history fails", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ detail: "Database offline." }, 500));

    await expect(getTracerHistory()).rejects.toThrow("Database offline.");
  });

  it("saves a request with POST and the same headers", async () => {
    const input = { collectionId: "c1", newCollectionName: null, title: "t", method: "GET" as const, url: "https://example.com", headers: {} };
    fetchMock.mockResolvedValue(jsonResponse({ id: "r1" }));

    await saveTracerRequest(input);

    expect(fetchMock).toHaveBeenCalledWith("/api/tracer/collections/requests", {
      method: "POST",
      headers: expectedHeaders,
      body: JSON.stringify(input),
    });
  });
});
