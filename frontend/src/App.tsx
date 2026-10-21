import { platformName } from "./platform";
import { useState, type FormEvent } from "react";
import { sendTracerGet, type TracerResponse } from "./tracer";
import "./App.css";

export function App() {
  const [url, setUrl] = useState("");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<TracerResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (loading) return;
    setLoading(true);
    setResult(null);
    setError(null);
    try {
      const response = await sendTracerGet(url);
      setResult(response);
      if (response.error && response.failure !== "PolicyRejected")
        setError(response.error);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The request could not be completed.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="workspace">
      <header><h1>{platformName}</h1></header>
      <div className="panes">
        <section aria-labelledby="request-title">
          <h2 id="request-title">Request</h2>
          <form onSubmit={handleSubmit}>
            <label className="request-line">
              <span>GET</span>
              <input aria-label="Request URL" type="url" placeholder="Enter URL" value={url} onChange={event => setUrl(event.target.value)} disabled={loading} />
              <button type="submit" disabled={!url.trim() || loading}>{loading ? "Sending…" : "Send"}</button>
            </label>
          </form>
        </section>
        <section aria-labelledby="response-title" aria-live="polite">
          <h2 id="response-title">Response</h2>
          {loading && <p role="status">Sending request…</p>}
          {error && <p role="alert">{error}</p>}
          {!result && !error && !loading && <p>Enter a URL to send a request.</p>}
          {result && <div>
            {result.requestSent && result.statusCode !== null && <p><strong>HTTP {result.statusCode}</strong> · {result.elapsedMilliseconds.toFixed(0)} ms</p>}
            {result.error && <p role="alert">{result.error}</p>}
            <pre>{result.body || (result.requestSent ? "The response body is empty." : "Request not sent.")}</pre>
          </div>}
        </section>
      </div>
    </main>
  );
}