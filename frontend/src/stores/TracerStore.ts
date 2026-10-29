import { makeAutoObservable, runInAction } from "mobx";
import { sendTracerRequest } from "../api/tracerApi";
import type { TracerResponse } from "../tracer";
import { errorMessage, type ResponseTab } from "../request/requestHelpers";
import type { RequestEditorStore } from "./RequestEditorStore";
import type { HistoryStore } from "./HistoryStore";

const historySavedNotice = "The request was sent, but its History entry was not saved.";

export class TracerStore {
  loading = false;
  result: TracerResponse | null = null;
  error: string | null = null;
  previousResult = false;
  notice: string | null = null;
  responseTab: ResponseTab = "Body";
  readonly editor: RequestEditorStore;
  readonly history: HistoryStore;

  constructor(editor: RequestEditorStore, history: HistoryStore) {
    this.editor = editor;
    this.history = history;
    makeAutoObservable(this, { editor: false, history: false });
  }

  setResponseTab(value: ResponseTab): void {
    this.responseTab = value;
  }

  setNotice(value: string | null): void {
    this.notice = value;
  }

  reset(): void {
    this.result = null;
    this.error = null;
    this.notice = null;
    this.previousResult = false;
    this.responseTab = "Body";
  }

  async send(): Promise<void> {
    if (this.loading || !this.editor.canSend) return;
    this.previousResult = this.result !== null;
    this.loading = true;
    this.error = null;
    this.notice = null;
    try {
      const response = await sendTracerRequest(this.editor.tracerRequest);
      runInAction(() => {
        this.result = response;
        this.responseTab = "Body";
        this.previousResult = false;
        if (response.historySaved === false) this.notice = historySavedNotice;
        if (response.error && response.failure !== "PolicyRejected") this.error = response.error;
      });
      if (this.history.open) await this.history.refreshItems();
    } catch (cause) {
      runInAction(() => { this.error = errorMessage(cause, "The request could not be completed."); });
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }
}
