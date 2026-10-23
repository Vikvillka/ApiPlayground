import { platformName } from "./platform";
import { useState, type FormEvent, type KeyboardEvent } from "react";
import { sendTracerRequest, type TracerResponse } from "./tracer";
import "./App.css";

type RequestRow = { id: number; key: string; value: string; enabled: boolean };
type RequestTab = "Params" | "Headers" | "Body";
type ResponseTab = "Body" | "Headers";
const methods = ["GET", "POST", "PUT", "PATCH", "DELETE"] as const;
const makeRow = (id: number): RequestRow => ({ id, key: "", value: "", enabled: true });

export function App() {
  const [url, setUrl] = useState("");
  const [method, setMethod] = useState<(typeof methods)[number]>("GET");
  const [tab, setTab] = useState<RequestTab>("Params");
  const [responseTab, setResponseTab] = useState<ResponseTab>("Body");
  const [params, setParams] = useState<RequestRow[]>([makeRow(1)]);
  const [headers, setHeaders] = useState<RequestRow[]>([makeRow(2)]);
  const [body, setBody] = useState("");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<TracerResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previousResult, setPreviousResult] = useState(false);
  const [nextRowId, setNextRowId] = useState(3);

  const enabledHeaders = headers.filter(row => row.enabled && row.key.trim());
  const enabledParameters = params.filter(row => row.enabled && row.key.trim());
  const duplicateHeader = enabledHeaders.some((row, index) => enabledHeaders
    .slice(0, index).some(previous => previous.key.trim().toLowerCase() === row.key.trim().toLowerCase()));
  const urlError = url.trim() && !isValidUrl(url) ? "Enter a valid absolute HTTP or HTTPS URL." : null;
  const finalUrl = url.trim() && !urlError ? addEnabledParameters(url, enabledParameters) : url;
  const finalUrlError = url.trim() && !urlError && finalUrl.length > 2048 ? "The request URL including parameters must be 2048 characters or fewer." : null;
  let bodyError: string | null = null;
  if (body.trim() && !(method === "POST" || method === "PUT" || method === "PATCH"))
    bodyError = "JSON bodies are only available for POST, PUT, and PATCH.";
  else if (body.trim()) {
    try { JSON.parse(body); } catch { bodyError = "Enter valid JSON."; }
  }
  const headerError = duplicateHeader ? "Enabled header names must be unique." : null;
  const validationError = urlError ?? finalUrlError ?? bodyError ?? headerError;
  const hiddenValidationError = tab !== "Body" && bodyError
    ? "Fix the Body input before sending."
    : tab !== "Headers" && headerError
      ? "Fix the Headers input before sending."
      : null;

  function updateRows(setter: (rows: RequestRow[]) => void, rows: RequestRow[], id: number, change: Partial<RequestRow>) {
    setter(rows.map(row => row.id === id ? { ...row, ...change } : row));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (loading || validationError || !url.trim()) return;
    setPreviousResult(result !== null);
    setLoading(true);
    setError(null);
    try {
      const requestHeaders = Object.fromEntries(enabledHeaders.map(row => [row.key.trim(), row.value]));
      const response = await sendTracerRequest({
        url: finalUrl, method, headers: requestHeaders,
        ...(body.trim() && (method === "POST" || method === "PUT" || method === "PATCH") ? { body } : {}),
      });
      setResult(response);
      setResponseTab("Body");
      setPreviousResult(false);
      if (response.error && response.failure !== "PolicyRejected")
        setError(response.error);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The request could not be completed.");
    } finally {
      setLoading(false);
    }
  }

  function handleResponseTabKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const tabs: ResponseTab[] = ["Body", "Headers"];
    const currentIndex = tabs.indexOf(responseTab);
    const nextIndex = event.key === "ArrowRight"
      ? (currentIndex + 1) % tabs.length
      : event.key === "ArrowLeft"
        ? (currentIndex - 1 + tabs.length) % tabs.length
        : event.key === "Home"
          ? 0
          : event.key === "End"
            ? tabs.length - 1
            : -1;
    if (nextIndex < 0) return;
    event.preventDefault();
    setResponseTab(tabs[nextIndex]);
    document.getElementById(`response-tab-${tabs[nextIndex].toLowerCase()}`)?.focus();
  }

  return (
    <main className="workspace">
      <header><h1>{platformName}</h1></header>
      <div className="panes">
        <section aria-labelledby="request-title">
          <h2 id="request-title">Request</h2>
          <form onSubmit={handleSubmit}>
            <div className="request-line">
              <label className="visually-hidden" htmlFor="request-method">Request method</label>
              <select id="request-method" value={method} onChange={event => setMethod(event.target.value as (typeof methods)[number])} disabled={loading}>
                {methods.map(value => <option key={value}>{value}</option>)}
              </select>
              <label className="visually-hidden" htmlFor="request-url">Request URL</label>
              <input id="request-url" aria-label="Request URL" type="url" placeholder="Enter URL" value={url} onChange={event => setUrl(event.target.value)} disabled={loading} aria-invalid={Boolean(urlError || finalUrlError)} aria-describedby={urlError || finalUrlError ? "url-error" : undefined} />
              <button type="submit" aria-describedby={hiddenValidationError ? "send-validation-error" : undefined} disabled={!url.trim() || Boolean(validationError) || loading}>{loading ? "Sending…" : "Send"}</button>
            </div>
            {(urlError || finalUrlError) && <p id="url-error" className="field-error" role="alert">{urlError || finalUrlError}</p>}
            {hiddenValidationError && <p id="send-validation-error" className="field-error" role="alert">{hiddenValidationError}</p>}
            <div className="tabs" role="tablist" aria-label="Request inputs">
              {(["Params", "Headers", "Body"] as const).map(value => <button key={value} type="button" role="tab" aria-selected={tab === value} onClick={() => setTab(value)}>{value}</button>)}
            </div>
            {tab === "Params" && <div role="tabpanel" aria-label="Params" className="rows-editor">
              {params.map(row => <div className="key-value-row" key={row.id}>
                <input aria-label="Parameter key" placeholder="Key" value={row.key} disabled={loading} onChange={event => updateRows(setParams, params, row.id, { key: event.target.value })} />
                <input aria-label="Parameter value" placeholder="Value" value={row.value} disabled={loading} onChange={event => updateRows(setParams, params, row.id, { value: event.target.value })} />
                <label><input type="checkbox" checked={row.enabled} disabled={loading} onChange={event => updateRows(setParams, params, row.id, { enabled: event.target.checked })} />Enabled</label>
                <button type="button" aria-label="Remove parameter" disabled={loading} onClick={() => setParams(params.filter(item => item.id !== row.id))}>Remove</button>
              </div>)}
              <button type="button" onClick={() => { setParams([...params, makeRow(nextRowId)]); setNextRowId(nextRowId + 1); }} disabled={loading}>Add parameter</button>
            </div>}
            {tab === "Headers" && <div role="tabpanel" aria-label="Headers" className="rows-editor">
              {headers.map(row => <div className="key-value-row" key={row.id}>
                <input aria-label="Header key" placeholder="Key" value={row.key} disabled={loading} aria-invalid={Boolean(headerError)} aria-describedby={headerError ? "header-error" : undefined} onChange={event => updateRows(setHeaders, headers, row.id, { key: event.target.value })} />
                <input aria-label="Header value" placeholder="Value" value={row.value} disabled={loading} onChange={event => updateRows(setHeaders, headers, row.id, { value: event.target.value })} />
                <label><input type="checkbox" checked={row.enabled} disabled={loading} onChange={event => updateRows(setHeaders, headers, row.id, { enabled: event.target.checked })} />Enabled</label>
                <button type="button" aria-label="Remove header" disabled={loading} onClick={() => setHeaders(headers.filter(item => item.id !== row.id))}>Remove</button>
              </div>)}
              {headerError && <p id="header-error" className="field-error" role="alert">{headerError}</p>}
              <button type="button" onClick={() => { setHeaders([...headers, makeRow(nextRowId)]); setNextRowId(nextRowId + 1); }} disabled={loading}>Add header</button>
            </div>}
            {tab === "Body" && <div role="tabpanel" aria-label="Body" className="body-editor">
              <label htmlFor="json-body">JSON body</label>
              <textarea id="json-body" value={body} disabled={loading} onChange={event => setBody(event.target.value)} aria-invalid={Boolean(bodyError)} aria-describedby={bodyError ? "body-error" : "body-hint"} />
              <p id="body-hint">Optional JSON; available for POST, PUT, and PATCH.</p>
              {bodyError && <p id="body-error" className="field-error" role="alert">{bodyError}</p>}
            </div>}
          </form>
        </section>
        <section aria-labelledby="response-title">
          <h2 id="response-title">Response</h2>
          {loading && <p role="status">Sending request…</p>}
          {error && <p role="alert">{error}</p>}
          {!result && !error && !loading && <p>Enter a URL to send a request.</p>}
          {result && <div>
            {previousResult && <p className="previous-result" role="status">This result is for the previous request.</p>}
            {result.requestSent && result.statusCode !== null && <p className="response-summary" role="status"><strong>HTTP {result.statusCode}</strong><span>{result.elapsedMilliseconds.toFixed(0)} ms</span></p>}
            {result.error && <p role={result.requestSent ? "status" : "alert"}>{result.error}</p>}
            {result.requestSent && result.statusCode !== null && <>
              <div className="tabs response-tabs" role="tablist" aria-label="Response details">
                {(["Body", "Headers"] as const).map(value => <button key={value} id={`response-tab-${value.toLowerCase()}`} type="button" role="tab" aria-controls={`response-panel-${value.toLowerCase()}`} aria-selected={responseTab === value} tabIndex={responseTab === value ? 0 : -1} onKeyDown={handleResponseTabKeyDown} onClick={() => setResponseTab(value)}>{value}</button>)}
              </div>
              {responseTab === "Body" && <div id="response-panel-body" role="tabpanel" aria-labelledby="response-tab-body" tabIndex={0}>
                <pre>{formatResponseBody(result.body) || "The response body is empty."}</pre>
              </div>}
              {responseTab === "Headers" && <div id="response-panel-headers" role="tabpanel" aria-labelledby="response-tab-headers" tabIndex={0} className="response-headers">
                {Object.entries(result.headers ?? {}).length === 0
                  ? <p>No response headers.</p>
                  : <dl>{Object.entries(result.headers ?? {}).map(([name, values]) => <div className="response-header" key={name}>
                    <dt>{name}</dt>
                    <dd>{values.map((value, index) => <div key={`${name}-${index}`}>{value}</div>)}</dd>
                  </div>)}</dl>}
              </div>}
            </>}
          </div>}
        </section>
      </div>
    </main>
  );
}

function isValidUrl(value: string): boolean {
  try {
    const parsed = new URL(value);
    return (parsed.protocol === "http:" || parsed.protocol === "https:") && !parsed.username && !parsed.password && !parsed.hash;
  } catch { return false; }
}

function formatResponseBody(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

function addEnabledParameters(value: string, parameters: RequestRow[]): string {
  const parsed = new URL(value);
  for (const parameter of parameters) parsed.searchParams.append(parameter.key, parameter.value);
  return parsed.toString();
}