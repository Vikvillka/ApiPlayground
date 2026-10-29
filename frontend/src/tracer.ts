export type TracerResponse = {
  failure: string;
  statusCode: number | null;
  elapsedMilliseconds: number;
  headers: Record<string, string[]>;
  body: string;
  error: string | null;
  requestSent: boolean;
  historySaved?: boolean;
};

export type TracerRequest = {
  url: string;
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  headers: Record<string, string>;
  body?: string;
};

export type HistoryItem = {
  id: string;
  createdAt: string;
  method: TracerRequest["method"];
  url: string;
  redactedQueryParameters: string[];
  headers: { name: string; value: string; redacted: boolean }[];
  redactedHeaders: string[];
  body: string | null;
  failure: string;
  statusCode: number | null;
  elapsedMilliseconds: number;
};

export type SavedRequest = {
  id: string;
  createdAt: string;
  collectionId: string;
  collectionName: string;
  title: string;
  method: TracerRequest["method"];
  url: string;
  headers: { name: string; value: string; redacted: boolean }[];
  redactedHeaders: string[];
  body: string | null;
};

export type RequestCollection = {
  id: string;
  name: string;
  requests: SavedRequest[];
};

export type SaveRequestInput = {
  collectionId: string | null;
  newCollectionName: string | null;
  title: string;
  method: TracerRequest["method"];
  url: string;
  headers: Record<string, string>;
  body?: string;
};

const localApiHeaders = {
  "Content-Type": "application/json",
  "X-ApiPlayground-Request": "1",
};

// Reads the body as text first so empty bodies (bodyless 400/404/413) and non-JSON bodies do not throw.
async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text.length === 0) return null;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}

// Prefers Problem Details `detail`, then the legacy `error` envelope, then the caller's fallback.
function messageFrom(body: unknown, fallback: string): string {
  if (typeof body === "object" && body !== null) {
    const { detail, error } = body as { detail?: unknown; error?: unknown };
    if (typeof detail === "string" && detail.length > 0) return detail;
    if (typeof error === "string" && error.length > 0) return error;
  }
  return fallback;
}

function isTracerResponse(body: unknown): body is TracerResponse {
  return typeof body === "object" && body !== null && "failure" in body;
}

export async function sendTracerRequest(request: TracerRequest): Promise<TracerResponse> {
  const response = await fetch("/api/tracer/get", {
    method: "POST",
    headers: localApiHeaders,
    body: JSON.stringify(request),
  });
  const body = await readBody(response);
  // Rich outcomes (including non-2xx HTTP results and policy rejections) are returned as observed results.
  if (isTracerResponse(body)) return body;
  if (response.ok) throw new Error("The tracer response could not be read.");
  throw new Error(messageFrom(body, `Tracer request failed with HTTP ${response.status}.`));
}

export async function getTracerHistory(): Promise<HistoryItem[]> {
  const response = await fetch("/api/tracer/history", { method: "GET", headers: localApiHeaders });
  const body = await readBody(response);
  const fallback = `History could not be loaded (HTTP ${response.status}).`;
  if (!response.ok) throw new Error(messageFrom(body, fallback));
  if (!Array.isArray(body)) throw new Error(fallback);
  return body as HistoryItem[];
}

export async function getTracerCollections(): Promise<RequestCollection[]> {
  const response = await fetch("/api/tracer/collections", { method: "GET", headers: localApiHeaders });
  const body = await readBody(response);
  const fallback = `Collections could not be loaded (HTTP ${response.status}).`;
  if (!response.ok) throw new Error(messageFrom(body, fallback));
  if (!Array.isArray(body)) throw new Error(fallback);
  return body as RequestCollection[];
}

export async function saveTracerRequest(request: SaveRequestInput): Promise<SavedRequest> {
  const response = await fetch("/api/tracer/collections/requests", {
    method: "POST", headers: localApiHeaders, body: JSON.stringify(request),
  });
  const body = await readBody(response);
  if (!response.ok) {
    throw new Error(messageFrom(body, `Request could not be saved (HTTP ${response.status}).`));
  }
  if (typeof body !== "object" || body === null) {
    throw new Error("The saved request response could not be read.");
  }
  return body as SavedRequest;
}
