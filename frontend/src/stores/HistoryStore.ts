import { makeAutoObservable, runInAction } from "mobx";
import { getTracerCollections, getTracerHistory } from "../api/tracerApi";
import type { HistoryItem, RequestCollection } from "../api/types";
import { errorMessage } from "../request/requestHelpers";

export class HistoryStore {
  open = false;
  loading = false;
  items: HistoryItem[] = [];
  error: string | null = null;
  collections: RequestCollection[] = [];
  collectionsLoaded = false;
  collectionError: string | null = null;

  constructor() {
    makeAutoObservable(this);
  }

  async toggle(): Promise<void> {
    this.open = !this.open;
    if (!this.open) return;
    this.loading = true;
    this.error = null;
    try {
      const items = await getTracerHistory();
      runInAction(() => { this.items = items; });
    } catch (cause) {
      runInAction(() => { this.error = errorMessage(cause, "History could not be loaded."); });
    }
    await this.loadCollections("Collections could not be loaded.");
    runInAction(() => { this.loading = false; });
  }

  // Keeps the completed send visible even when the history refresh fails.
  async refreshItems(): Promise<void> {
    try {
      const items = await getTracerHistory();
      runInAction(() => { this.items = items; });
    } catch {
      /* The send result stays visible; the list is refreshed the next time History opens. */
    }
  }

  async loadCollections(failureMessage: string): Promise<void> {
    try {
      const collections = await getTracerCollections();
      runInAction(() => {
        this.collections = collections;
        this.collectionsLoaded = true;
        this.collectionError = null;
      });
    } catch (cause) {
      runInAction(() => { this.collectionError = errorMessage(cause, failureMessage); });
    }
  }

  async ensureCollections(): Promise<RequestCollection[]> {
    if (!this.collectionsLoaded) await this.loadCollections("Collections could not be loaded.");
    return this.collections;
  }
}
