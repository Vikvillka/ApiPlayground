import { platformName } from "./platform";
import { useLayoutEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { getTracerCollections, getTracerHistory, saveTracerRequest, sendTracerRequest, type HistoryItem, type RequestCollection, type SavedRequest, type TracerResponse } from "./tracer";
import "./App.css";

type RequestRow = { id: number; key: string; value: string; enabled: boolean; redacted?: boolean };
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
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyItems, setHistoryItems] = useState<HistoryItem[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [historyNotice, setHistoryNotice] = useState<string | null>(null);
  const [collections, setCollections] = useState<RequestCollection[]>([]);
  const [collectionsLoaded, setCollectionsLoaded] = useState(false);
  const [collectionError, setCollectionError] = useState<string | null>(null);
  const [saveOpen, setSaveOpen] = useState(false);
  const [saveTitle, setSaveTitle] = useState("");
  const [saveCollectionId, setSaveCollectionId] = useState("");
  const [newCollectionName, setNewCollectionName] = useState("");
  const [createCollection, setCreateCollection] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const saveTitleRef = useRef<HTMLInputElement>(null);
  const saveTriggerRef = useRef<HTMLButtonElement>(null);

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

  useLayoutEffect(() => {
    if (saveOpen) saveTitleRef.current?.focus();
  }, [saveOpen]);

  function updateRows(setter: (rows: RequestRow[]) => void, rows: RequestRow[], id: number, change: Partial<RequestRow>) {
    setter(rows.map(row => row.id === id ? { ...row, ...change } : row));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (loading || validationError || !url.trim()) return;
    setPreviousResult(result !== null);
    setLoading(true);
    setError(null);
    setHistoryNotice(null);
    try {
      const requestHeaders = Object.fromEntries(enabledHeaders.map(row => [row.key.trim(), row.value]));
      const response = await sendTracerRequest({
        url: finalUrl, method, headers: requestHeaders,
        ...(body.trim() && (method === "POST" || method === "PUT" || method === "PATCH") ? { body } : {}),
      });
      setResult(response);
      setResponseTab("Body");
      setPreviousResult(false);
      if (response.historySaved === false) setHistoryNotice("The request was sent, but its History entry was not saved.");
      if (historyOpen) {
        try { setHistoryItems(await getTracerHistory()); } catch { /* Keep the completed dispatch result visible. */ }
      }
      if (response.error && response.failure !== "PolicyRejected")
        setError(response.error);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The request could not be completed.");
    } finally {
      setLoading(false);
    }
  }

  async function toggleHistory() {
    const nextOpen = !historyOpen;
    setHistoryOpen(nextOpen);
    if (!nextOpen) return;
    setHistoryLoading(true);
    setHistoryError(null);
    try { setHistoryItems(await getTracerHistory()); }
    catch (cause) { setHistoryError(cause instanceof Error ? cause.message : "History could not be loaded."); }
    try {
      setCollections(await getTracerCollections());
      setCollectionsLoaded(true);
      setCollectionError(null);
    } catch (cause) {
      setCollectionError(cause instanceof Error ? cause.message : "Collections could not be loaded.");
    }
    finally { setHistoryLoading(false); }
  }

  function loadHistoryItem(item: HistoryItem) {
    const parsed = new URL(item.url);
    setUrl(`${parsed.origin}${parsed.pathname}${parsed.hash}`);
    const restoredParams = rowsFromUrl(item.url, nextRowId, item.redactedQueryParameters);
    setParams(restoredParams.length ? restoredParams : [makeRow(nextRowId)]);
    const firstHeaderId = nextRowId + Math.max(1, restoredParams.length);
    const restoredHeaders = item.headers.map((header, index) => ({
      id: firstHeaderId + index, key: header.name, value: header.redacted ? "" : header.value, enabled: !header.redacted, redacted: header.redacted,
    }));
    setHeaders(restoredHeaders.length ? restoredHeaders : [makeRow(firstHeaderId)]);
    setNextRowId(firstHeaderId + Math.max(1, restoredHeaders.length));
    setMethod(item.method);
    setBody(item.body ?? "");
    setResult(null);
    setError(null);
    setHistoryNotice(null);
    setPreviousResult(false);
    setResponseTab("Body");
    setTab("Params");
  }

  function loadSavedRequest(item: SavedRequest) {
    const parsed = new URL(item.url);
    setUrl(`${parsed.origin}${parsed.pathname}${parsed.hash}`);
    const restoredParams = rowsFromUrl(item.url, nextRowId);
    setParams(restoredParams.length ? restoredParams : [makeRow(nextRowId)]);
    const firstHeaderId = nextRowId + Math.max(1, restoredParams.length);
    const restoredHeaders = item.headers.map((header, index) => ({
      id: firstHeaderId + index, key: header.name, value: header.redacted ? "" : header.value,
      enabled: !header.redacted, redacted: header.redacted,
    }));
    setHeaders(restoredHeaders.length ? restoredHeaders : [makeRow(firstHeaderId)]);
    setNextRowId(firstHeaderId + Math.max(1, restoredHeaders.length));
    setMethod(item.method);
    setBody(item.body ?? "");
    setResult(null);
    setError(null);
    setHistoryNotice(null);
    setPreviousResult(false);
    setResponseTab("Body");
    setTab("Params");
  }

  async function openSaveDialog() {
    let availableCollections = collections;
    if (!collectionsLoaded) {
      try {
        availableCollections = await getTracerCollections();
        setCollections(availableCollections);
        setCollectionsLoaded(true);
        setCollectionError(null);
      } catch (cause) {
        setCollectionError(cause instanceof Error ? cause.message : "Collections could not be loaded.");
      }
    }
    const parsed = new URL(finalUrl);
    setSaveTitle(`${method} ${parsed.pathname || "/"}`);
    setSaveCollectionId(availableCollections[0]?.id ?? "");
    setCreateCollection(availableCollections.length === 0);
    setNewCollectionName("");
    setSaveError(null);
    setSaveOpen(true);
  }

  function closeSaveDialog() {
    if (saving) return;
    setSaveOpen(false);
    setSaveError(null);
    requestAnimationFrame(() => saveTriggerRef.current?.focus());
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || validationError || !finalUrl.trim()) return;
    setSaving(true);
    setSaveError(null);
    const requestHeaders = Object.fromEntries(enabledHeaders.map(row => [row.key.trim(), row.value]));
    try {
      await saveTracerRequest({
        collectionId: createCollection ? null : saveCollectionId,
        newCollectionName: createCollection ? newCollectionName : null,
        title: saveTitle,
        method,
        url: finalUrl,
        headers: requestHeaders,
        ...(body.trim() && (method === "POST" || method === "PUT" || method === "PATCH") ? { body } : {}),
      });
      setSaveOpen(false);
      setHistoryNotice("Request saved to Collections.");
      requestAnimationFrame(() => saveTriggerRef.current?.focus());
      try {
        setCollections(await getTracerCollections());
        setCollectionsLoaded(true);
        setCollectionError(null);
      } catch (cause) {
        setCollectionError(cause instanceof Error ? cause.message : "Collections could not be refreshed.");
      }
    } catch (cause) {
      setSaveError(cause instanceof Error ? cause.message : "The request could not be saved. Try again.");
    } finally {
      setSaving(false);
    }
  }

  function handleSaveDialogKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key === "Escape") {
      event.preventDefault();
      closeSaveDialog();
      return;
    }
    if (event.key !== "Tab") return;
    const controls = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
    ));
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
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

  function handleRequestTabKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const tabs: RequestTab[] = ["Params", "Headers", "Body"];
    const index = tabs.indexOf(tab);
    const nextIndex = event.key === "ArrowRight" ? (index + 1) % tabs.length
      : event.key === "ArrowLeft" ? (index - 1 + tabs.length) % tabs.length
        : event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1 : -1;
    if (nextIndex < 0) return;
    event.preventDefault();
    setTab(tabs[nextIndex]);
    document.getElementById(`request-tab-${tabs[nextIndex].toLowerCase()}`)?.focus();
  }

  return (
    <main className="workspace">
      <header><h1>{platformName}</h1></header>
        <div className="history-navigation">
          <button type="button" aria-label={`${historyOpen ? "Hide" : "Show"} History & Collections`} aria-expanded={historyOpen} aria-controls="request-history" onClick={toggleHistory}>{historyOpen ? "Hide History" : "Show History"} &amp; Collections</button>
          <aside id="request-history" aria-label="Request History" hidden={!historyOpen}>
            <h2>History</h2>
            {historyLoading && <p role="status">Loading History…</p>}
            {historyError && <p role="alert">{historyError}</p>}
            {!historyLoading && !historyError && historyItems.length === 0 && <p>No dispatched requests yet.</p>}
            <ul>{historyItems.map(item => <li key={item.id}>
              <button type="button" disabled={loading} onClick={() => loadHistoryItem(item)} aria-label={`Load ${item.method} ${new URL(item.url).pathname}`}>
                <strong>{item.method}</strong> {item.statusCode ? `HTTP ${item.statusCode}` : item.failure}<small>{new URL(item.url).pathname} · {new Date(item.createdAt).toLocaleString()}</small>
              </button>
            </li>)}</ul>
            <h2>Collections</h2>
            {collectionError && <p role="alert">{collectionError}</p>}
            {!collectionError && collections.length === 0 && <p>No saved collections yet. Save a configured request to keep it for later.</p>}
            {collections.map(collection => <section className="saved-collection" key={collection.id} aria-label={collection.name}>
              <h3>{collection.name}</h3>
              {collection.requests.length === 0 && <p>No saved requests in this collection.</p>}
              <ul>{collection.requests.map(item => <li key={item.id}>
                <button type="button" disabled={loading} onClick={() => loadSavedRequest(item)} aria-label={`Load saved ${item.method} ${item.title}`}>
                  <strong>{item.method}</strong> {item.title}<small>{new URL(item.url).pathname}</small>
                </button>
              </li>)}</ul>
            </section>)}
          </aside>
        </div>
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
            <button ref={saveTriggerRef} type="button" className="save-request-button" onClick={openSaveDialog} disabled={!url.trim() || Boolean(validationError) || loading}>Save to collection</button>
            {(urlError || finalUrlError) && <p id="url-error" className="field-error" role="alert">{urlError || finalUrlError}</p>}
            {hiddenValidationError && <p id="send-validation-error" className="field-error" role="alert">{hiddenValidationError}</p>}
            <div className="tabs" role="tablist" aria-label="Request inputs">
              {(["Params", "Headers", "Body"] as const).map(value => <button key={value} id={`request-tab-${value.toLowerCase()}`} type="button" role="tab" aria-controls={`request-panel-${value.toLowerCase()}`} aria-selected={tab === value} tabIndex={tab === value ? 0 : -1} onKeyDown={handleRequestTabKeyDown} onClick={() => setTab(value)}>{value}</button>)}
            </div>
            {tab === "Params" && <div id="request-panel-params" role="tabpanel" aria-labelledby="request-tab-params" className="rows-editor">
              {params.map(row => <div className="key-value-row" key={row.id}>
                <input aria-label="Parameter key" placeholder="Key" value={row.key} disabled={loading} onChange={event => updateRows(setParams, params, row.id, { key: event.target.value, redacted: false })} />
                <input aria-label="Parameter value" placeholder={row.redacted ? "Value not saved — enter it again" : "Value"} value={row.value} disabled={loading} onChange={event => updateRows(setParams, params, row.id, { value: event.target.value, enabled: row.redacted ? Boolean(event.target.value) : row.enabled })} />
                <label><input type="checkbox" checked={row.enabled} disabled={loading || (row.redacted && !row.value)} onChange={event => updateRows(setParams, params, row.id, { enabled: event.target.checked })} />Enabled</label>
                <button type="button" aria-label="Remove parameter" disabled={loading} onClick={() => setParams(params.filter(item => item.id !== row.id))}>Remove</button>
              </div>)}
              <button type="button" onClick={() => { setParams([...params, makeRow(nextRowId)]); setNextRowId(nextRowId + 1); }} disabled={loading}>Add parameter</button>
            </div>}
            {tab === "Headers" && <div id="request-panel-headers" role="tabpanel" aria-labelledby="request-tab-headers" className="rows-editor">
              {headers.map(row => <div className="key-value-row" key={row.id}>
                <input aria-label="Header key" placeholder="Key" value={row.key} disabled={loading} aria-invalid={Boolean(headerError)} aria-describedby={headerError ? "header-error" : undefined} onChange={event => updateRows(setHeaders, headers, row.id, { key: event.target.value, redacted: false })} />
                <input aria-label="Header value" placeholder={row.redacted ? "Value not saved — enter it again" : "Value"} value={row.value} disabled={loading} onChange={event => updateRows(setHeaders, headers, row.id, { value: event.target.value, enabled: row.redacted ? Boolean(event.target.value) : row.enabled })} />
                <label><input type="checkbox" checked={row.enabled} disabled={loading || (row.redacted && !row.value)} onChange={event => updateRows(setHeaders, headers, row.id, { enabled: event.target.checked })} />Enabled</label>
                <button type="button" aria-label="Remove header" disabled={loading} onClick={() => setHeaders(headers.filter(item => item.id !== row.id))}>Remove</button>
              </div>)}
              {headerError && <p id="header-error" className="field-error" role="alert">{headerError}</p>}
              <button type="button" onClick={() => { setHeaders([...headers, makeRow(nextRowId)]); setNextRowId(nextRowId + 1); }} disabled={loading}>Add header</button>
            </div>}
            {tab === "Body" && <div id="request-panel-body" role="tabpanel" aria-labelledby="request-tab-body" className="body-editor">
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
          {historyNotice && <p role="status">{historyNotice}</p>}
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
      {saveOpen && <div className="dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) closeSaveDialog(); }}>
        <section className="save-dialog" role="dialog" aria-modal="true" aria-labelledby="save-dialog-title" aria-describedby={saveError ? "save-error" : undefined} onKeyDown={handleSaveDialogKeyDown}>
          <h2 id="save-dialog-title">Save to collection</h2>
          <form onSubmit={handleSave}>
            <label htmlFor="saved-request-title">Request title</label>
            <input ref={saveTitleRef} id="saved-request-title" value={saveTitle} maxLength={120} required disabled={saving} onChange={event => setSaveTitle(event.target.value)} />
            <fieldset disabled={saving}>
              <legend>Collection</legend>
              <label><input type="radio" name="collection-choice" checked={!createCollection} disabled={collections.length === 0} onChange={() => setCreateCollection(false)} />Choose existing</label>
              {collections.length > 0 && <select aria-label="Existing collection" value={saveCollectionId} disabled={createCollection || saving} onChange={event => setSaveCollectionId(event.target.value)}>
                {collections.map(collection => <option key={collection.id} value={collection.id}>{collection.name}</option>)}
              </select>}
              <label><input type="radio" name="collection-choice" checked={createCollection} onChange={() => setCreateCollection(true)} />Create new collection</label>
              {createCollection && <input aria-label="New collection name" placeholder="Collection name" value={newCollectionName} maxLength={80} required disabled={saving} onChange={event => setNewCollectionName(event.target.value)} />}
            </fieldset>
            {saveError && <p id="save-error" role="alert">{saveError}</p>}
            <div className="dialog-actions">
              <button type="button" onClick={closeSaveDialog} disabled={saving}>Cancel</button>
              <button type="submit" disabled={saving || !saveTitle.trim() || (createCollection ? !newCollectionName.trim() : !saveCollectionId)}>{saving ? "Saving…" : "Save request"}</button>
            </div>
          </form>
        </section>
      </div>}
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

function rowsFromUrl(value: string, firstId: number, redactedNames: string[] = []): RequestRow[] {
  const parsed = new URL(value);
  return [...parsed.searchParams.entries()].map(([key, value], index) => {
    const redacted = redactedNames.some(name => name.toLowerCase() === key.toLowerCase());
    return { id: firstId + index, key, value: redacted ? "" : value, enabled: !redacted, redacted };
  });
}