import { useLayoutEffect, useRef, type FormEvent, type KeyboardEvent } from "react";
import { observer } from "mobx-react-lite";
import { AppShell } from "./components/AppShell/AppShell";
import { RequestEditor } from "./components/RequestEditor/RequestEditor";
import { ResponsePanel } from "./components/ResponsePanel/ResponsePanel";
import { TopBar } from "./components/TopBar/TopBar";
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
        <ResponsePanel />
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
