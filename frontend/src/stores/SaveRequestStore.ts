import { makeAutoObservable, runInAction } from "mobx";
import { saveTracerRequest } from "../api/tracerApi";
import type { RequestCollection } from "../api/types";
import { defaultSaveTitle, errorMessage } from "../request/requestHelpers";
import type { RequestEditorStore } from "./RequestEditorStore";
import type { HistoryStore } from "./HistoryStore";
import type { TracerStore } from "./TracerStore";

export class SaveRequestStore {
  open = false;
  saving = false;
  title = "";
  collectionId = "";
  newCollectionName = "";
  createCollection = false;
  error: string | null = null;
  readonly editor: RequestEditorStore;
  readonly history: HistoryStore;
  readonly tracer: TracerStore;

  constructor(editor: RequestEditorStore, history: HistoryStore, tracer: TracerStore) {
    this.editor = editor;
    this.history = history;
    this.tracer = tracer;
    makeAutoObservable(this, { editor: false, history: false, tracer: false });
  }

  get canSubmit(): boolean {
    if (this.title.trim() === "") return false;
    return this.createCollection ? this.newCollectionName.trim() !== "" : this.collectionId !== "";
  }

  setTitle(value: string): void {
    this.title = value;
  }

  setCollectionId(value: string): void {
    this.collectionId = value;
  }

  setNewCollectionName(value: string): void {
    this.newCollectionName = value;
  }

  setCreateCollection(value: boolean): void {
    this.createCollection = value;
  }

  async openDialog(): Promise<void> {
    if (!this.editor.canSave) return;
    // Visible state is set synchronously so the dialog appears on the same tick as the click.
    this.title = defaultSaveTitle(this.editor.method, this.editor.finalUrl);
    this.newCollectionName = "";
    this.error = null;
    this.open = true;
    if (this.history.collectionsLoaded) {
      this.applyCollectionDefaults(this.history.collections);
      return;
    }
    const available = await this.history.ensureCollections();
    runInAction(() => {
      if (this.open) this.applyCollectionDefaults(available);
    });
  }

  private applyCollectionDefaults(available: RequestCollection[]): void {
    this.collectionId = available[0]?.id ?? "";
    this.createCollection = available.length === 0;
  }

  close(): void {
    if (this.saving) return;
    this.open = false;
    this.error = null;
  }

  async save(): Promise<void> {
    if (this.saving || !this.editor.canSave || !this.canSubmit) return;
    this.saving = true;
    this.error = null;
    try {
      await saveTracerRequest({
        collectionId: this.createCollection ? null : this.collectionId,
        newCollectionName: this.createCollection ? this.newCollectionName : null,
        title: this.title,
        ...this.editor.tracerRequest,
      });
      runInAction(() => {
        this.open = false;
        this.tracer.setNotice("Request saved to Collections.");
      });
      await this.history.loadCollections("Collections could not be refreshed.");
    } catch (cause) {
      runInAction(() => { this.error = errorMessage(cause, "The request could not be saved. Try again."); });
    } finally {
      runInAction(() => { this.saving = false; });
    }
  }
}
