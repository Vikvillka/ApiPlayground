export type TracerResponse = {
  failure: string;
  statusCode: number | null;
  elapsedMilliseconds: number;
  headers: Record<string, string[]>;
  body: string;
  persistenceVerified: boolean;
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

const localApiHeaders = {
  "Content-Type": "application/json",
  "X-ApiPlayground-Request": "1",
};

export async function sendTracerRequest(request: TracerRequest): Promise<TracerResponse> {
  const response = await fetch("/api/tracer/get", {
    method: "POST",
    headers: localApiHeaders,
    body: JSON.stringify(request),
  });
  const result = (await response.json()) as TracerResponse | { error?: string };
  if (!response.ok && "failure" in result) return result as TracerResponse;
  if (!response.ok && !("failure" in result)) {
    throw new Error(result.error ?? `Tracer request failed with HTTP ${response.status}.`);
  }
  return result as TracerResponse;
}

export async function getTracerHistory(): Promise<HistoryItem[]> {
  const response = await fetch("/api/tracer/history", { method: "GET", headers: localApiHeaders });
  if (!response.ok) throw new Error(`History could not be loaded (HTTP ${response.status}).`);
  return await response.json() as HistoryItem[];
}
