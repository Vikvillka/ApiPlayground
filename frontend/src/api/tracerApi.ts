import type {
  HistoryItem,
  RequestCollection,
  SaveRequestInput,
  SavedRequest,
  TracerRequest,
  TracerResponse,
} from "../tracer";
import { apiFetch, messageFrom } from "./apiClient";

// The tracer endpoints: same URLs, methods, and headers as the previous tracer.ts client.
// Types are imported from tracer.ts until the migration close-out story moves them here.

function isTracerResponse(body: unknown): body is TracerResponse {
  return typeof body === "object" && body !== null && "failure" in body;
}

export async function sendTracerRequest(request: TracerRequest): Promise<TracerResponse> {
  const result = await apiFetch("/api/tracer/get", "POST", request);
  // Rich outcomes (including non-2xx HTTP results and policy rejections) are returned as observed results.
  if (isTracerResponse(result.body)) return result.body;
  if (result.ok) throw new Error("The tracer response could not be read.");
  throw new Error(messageFrom(result.body, `Tracer request failed with HTTP ${result.status}.`));
}

export async function getTracerHistory(): Promise<HistoryItem[]> {
  const result = await apiFetch("/api/tracer/history", "GET");
  const fallback = `History could not be loaded (HTTP ${result.status}).`;
  if (!result.ok) throw new Error(messageFrom(result.body, fallback));
  if (!Array.isArray(result.body)) throw new Error(fallback);
  return result.body as HistoryItem[];
}

export async function getTracerCollections(): Promise<RequestCollection[]> {
  const result = await apiFetch("/api/tracer/collections", "GET");
  const fallback = `Collections could not be loaded (HTTP ${result.status}).`;
  if (!result.ok) throw new Error(messageFrom(result.body, fallback));
  if (!Array.isArray(result.body)) throw new Error(fallback);
  return result.body as RequestCollection[];
}

export async function saveTracerRequest(request: SaveRequestInput): Promise<SavedRequest> {
  const result = await apiFetch("/api/tracer/collections/requests", "POST", request);
  if (!result.ok) {
    throw new Error(messageFrom(result.body, `Request could not be saved (HTTP ${result.status}).`));
  }
  if (typeof result.body !== "object" || result.body === null) {
    throw new Error("The saved request response could not be read.");
  }
  return result.body as SavedRequest;
}
