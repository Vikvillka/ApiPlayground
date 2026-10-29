import { HistoryStore } from "./HistoryStore";
import { RequestEditorStore, type RequestSource } from "./RequestEditorStore";
import { SaveRequestStore } from "./SaveRequestStore";
import { TracerStore } from "./TracerStore";

// Composes the feature stores. Components read and change state through these stores only.
export class RootStore {
  readonly editor = new RequestEditorStore();
  readonly history = new HistoryStore();
  readonly tracer: TracerStore;
  readonly saveRequest: SaveRequestStore;

  constructor() {
    this.tracer = new TracerStore(this.editor, this.history);
    this.saveRequest = new SaveRequestStore(this.editor, this.history, this.tracer);
  }

  restoreRequest(source: RequestSource): void {
    this.editor.loadRequest(source);
    this.tracer.reset();
  }
}
