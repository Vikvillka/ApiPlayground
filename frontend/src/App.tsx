import { useLayoutEffect, useRef, type FormEvent, type KeyboardEvent } from "react";
import { observer } from "mobx-react-lite";
import { AppShell } from "./components/AppShell/AppShell";
import { RequestEditor } from "./components/RequestEditor/RequestEditor";
import { TopBar } from "./components/TopBar/TopBar";
import { formatResponseBody, type ResponseTab } from "./request/requestHelpers";
import { RootStoreProvider, useRootStore } from "./stores/StoreContext";
import type { HistoryItem, SavedRequest } from "./tracer";
import "./App.css";

export function App() {
  return (
    <RootStoreProvider>
      <Workspace />
    </RootStoreProvider>
  );
}

const Workspace = observer(function Workspace() {
  const root = useRootStore();
  const { history, tracer, saveRequest } = root;
  const result = tracer.result;
  const saveTitleRef = useRef<HTMLInputElement>(null);
  const saveTriggerRef = useRef<HTMLButtonElement>(null);

  useLayoutEffect(() => {
    if (saveRequest.open) saveTitleRef.current?.focus();
  }, [saveRequest.open]);

  function loadHistoryItem(item: HistoryItem) {
    root.restoreRequest({
      url: item.url,
      method: item.method,
      body: item.body ?? null,
      headers: item.headers,
      redactedQueryParameters: item.redactedQueryParameters,
    });
  }

  function loadSavedRequest(item: SavedRequest) {
    root.restoreRequest({
      url: item.url,
      method: item.method,
      body: item.body ?? null,
      headers: item.headers,
    });
  }

  function closeSaveDialog() {
    if (saveRequest.saving) return;
    saveRequest.close();
    requestAnimationFrame(() => saveTriggerRef.current?.focus());
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await saveRequest.save();
    if (!saveRequest.open) requestAnimationFrame(() => saveTriggerRef.current?.focus());
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
    const currentIndex = tabs.indexOf(tracer.responseTab);
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
    tracer.setResponseTab(tabs[nextIndex]);
    document.getElementById(`response-tab-${tabs[nextIndex].toLowerCase()}`)?.focus();
  }

  const historyDrawer = (
    <section id="request-history" aria-label="Request History">
      <h2>History</h2>
      {history.loading && <p role="status">Loading History…</p>}
      {history.error && <p role="alert">{history.error}</p>}
      {!history.loading && !history.error && history.items.length === 0 && <p>No dispatched requests yet.</p>}
      <ul>{history.items.map(item => <li key={item.id}>
        <button type="button" disabled={tracer.loading} onClick={() => loadHistoryItem(item)} aria-label={`Load ${item.method} ${new URL(item.url).pathname}`}>
          <strong>{item.method}</strong> {item.statusCode ? `HTTP ${item.statusCode}` : item.failure}<small>{new URL(item.url).pathname} · {new Date(item.createdAt).toLocaleString()}</small>
        </button>
      </li>)}</ul>
      <h2>Collections</h2>
      {history.collectionError && <p role="alert">{history.collectionError}</p>}
      {!history.collectionError && history.collections.length === 0 && <p>No saved collections yet. Save a configured request to keep it for later.</p>}
      {history.collections.map(collection => <section className="saved-collection" key={collection.id} aria-label={collection.name}>
        <h3>{collection.name}</h3>
        {collection.requests.length === 0 && <p>No saved requests in this collection.</p>}
        <ul>{collection.requests.map(item => <li key={item.id}>
          <button type="button" disabled={tracer.loading} onClick={() => loadSavedRequest(item)} aria-label={`Load saved ${item.method} ${item.title}`}>
            <strong>{item.method}</strong> {item.title}<small>{new URL(item.url).pathname}</small>
          </button>
        </li>)}</ul>
      </section>)}
    </section>
  );

  return (
    <AppShell sidebar={history.open ? historyDrawer : undefined}>
      <TopBar historyOpen={history.open} onToggleHistory={() => void history.toggle()} />
      <div className="panes">
        <RequestEditor saveButtonRef={saveTriggerRef} />
        <section aria-labelledby="response-title">
          <h2 id="response-title">Response</h2>
          {tracer.loading && <p role="status">Sending request…</p>}
          {tracer.error && <p role="alert">{tracer.error}</p>}
          {tracer.notice && <p role="status">{tracer.notice}</p>}
          {!result && !tracer.error && !tracer.loading && <p>Enter a URL to send a request.</p>}
          {result && <div>
            {tracer.previousResult && <p className="previous-result" role="status">This result is for the previous request.</p>}
            {result.requestSent && result.statusCode !== null && <p className="response-summary" role="status"><strong>HTTP {result.statusCode}</strong><span>{result.elapsedMilliseconds.toFixed(0)} ms</span></p>}
            {result.error && <p role={result.requestSent ? "status" : "alert"}>{result.error}</p>}
            {result.requestSent && result.statusCode !== null && <>
              <div className="tabs response-tabs" role="tablist" aria-label="Response details">
                {(["Body", "Headers"] as const).map(value => <button key={value} id={`response-tab-${value.toLowerCase()}`} type="button" role="tab" aria-controls={`response-panel-${value.toLowerCase()}`} aria-selected={tracer.responseTab === value} tabIndex={tracer.responseTab === value ? 0 : -1} onKeyDown={handleResponseTabKeyDown} onClick={() => tracer.setResponseTab(value)}>{value}</button>)}
              </div>
              {tracer.responseTab === "Body" && <div id="response-panel-body" role="tabpanel" aria-labelledby="response-tab-body" tabIndex={0}>
                <pre>{formatResponseBody(result.body) || "The response body is empty."}</pre>
              </div>}
              {tracer.responseTab === "Headers" && <div id="response-panel-headers" role="tabpanel" aria-labelledby="response-tab-headers" tabIndex={0} className="response-headers">
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
      {saveRequest.open && <div className="dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) closeSaveDialog(); }}>
        <section className="save-dialog" role="dialog" aria-modal="true" aria-labelledby="save-dialog-title" aria-describedby={saveRequest.error ? "save-error" : undefined} onKeyDown={handleSaveDialogKeyDown}>
          <h2 id="save-dialog-title">Save to collection</h2>
          <form onSubmit={handleSave}>
            <label htmlFor="saved-request-title">Request title</label>
            <input ref={saveTitleRef} id="saved-request-title" value={saveRequest.title} maxLength={120} required disabled={saveRequest.saving} onChange={event => saveRequest.setTitle(event.target.value)} />
            <fieldset disabled={saveRequest.saving}>
              <legend>Collection</legend>
              <label><input type="radio" name="collection-choice" checked={!saveRequest.createCollection} disabled={history.collections.length === 0} onChange={() => saveRequest.setCreateCollection(false)} />Choose existing</label>
              {history.collections.length > 0 && <select aria-label="Existing collection" value={saveRequest.collectionId} disabled={saveRequest.createCollection || saveRequest.saving} onChange={event => saveRequest.setCollectionId(event.target.value)}>
                {history.collections.map(collection => <option key={collection.id} value={collection.id}>{collection.name}</option>)}
              </select>}
              <label><input type="radio" name="collection-choice" checked={saveRequest.createCollection} onChange={() => saveRequest.setCreateCollection(true)} />Create new collection</label>
              {saveRequest.createCollection && <input aria-label="New collection name" placeholder="Collection name" value={saveRequest.newCollectionName} maxLength={80} required disabled={saveRequest.saving} onChange={event => saveRequest.setNewCollectionName(event.target.value)} />}
            </fieldset>
            {saveRequest.error && <p id="save-error" role="alert">{saveRequest.error}</p>}
            <div className="dialog-actions">
              <button type="button" onClick={closeSaveDialog} disabled={saveRequest.saving}>Cancel</button>
              <button type="submit" disabled={saveRequest.saving || !saveRequest.canSubmit}>{saveRequest.saving ? "Saving…" : "Save request"}</button>
            </div>
          </form>
        </section>
      </div>}
    </AppShell>
  );
});
