export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type ApiResult = {
  ok: boolean;
  status: number;
  body: unknown;
};

const localApiHeaders: Record<string, string> = {
  "Content-Type": "application/json",
  "X-ApiPlayground-Request": "1",
};

// Reads the body as text first so empty bodies and non-JSON bodies do not throw.
function parseBody(text: string): unknown {
  if (text.length === 0) return null;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}

// The single place that calls fetch for the local API. Callers decide what each status means.
export async function apiFetch(path: string, method: HttpMethod, payload?: unknown): Promise<ApiResult> {
  const response = await fetch(path, {
    method,
    headers: localApiHeaders,
    body: payload === undefined ? undefined : JSON.stringify(payload),
  });
  const text = await response.text();
  return { ok: response.ok, status: response.status, body: parseBody(text) };
}

// Prefers Problem Details `detail`, then the legacy `error` envelope, then the caller's fallback.
export function messageFrom(body: unknown, fallback: string): string {
  if (typeof body === "object" && body !== null) {
    const { detail, error } = body as { detail?: unknown; error?: unknown };
    if (typeof detail === "string" && detail.length > 0) return detail;
    if (typeof error === "string" && error.length > 0) return error;
  }
  return fallback;
}
