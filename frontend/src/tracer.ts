export type TracerResponse = {
  failure: string;
  statusCode: number | null;
  elapsedMilliseconds: number;
  body: string;
  persistenceVerified: boolean;
  error: string | null;
  requestSent: boolean;
};

export async function sendTracerGet(url: string): Promise<TracerResponse> {
  const response = await fetch("/api/tracer/get", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-ApiPlayground-Request": "1",
    },
    body: JSON.stringify({ url }),
  });
  const result = (await response.json()) as TracerResponse | { error?: string };
  if (!response.ok && "failure" in result && result.failure !== "PolicyRejected")
    throw new Error(result.error ?? `The request failed (${result.failure}).`);
  if (!response.ok && !("failure" in result)) {
    throw new Error(result.error ?? `Tracer request failed with HTTP ${response.status}.`);
  }
  return result as TracerResponse;
}
