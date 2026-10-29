import { makeAutoObservable } from "mobx";
import {
  activeRows,
  headersRecord,
  hiddenValidationMessage,
  makeRow,
  originAndPath,
  rowsFromUrl,
  supportsBody,
  validateDraft,
  type RequestMethod,
  type RequestRow,
  type RequestTab,
  type RequestValidation,
} from "../request/requestHelpers";
import type { TracerRequest } from "../api/types";

export type RequestSource = {
  url: string;
  method: RequestMethod;
  body: string | null;
  headers: { name: string; value: string; redacted: boolean }[];
  redactedQueryParameters?: string[];
};

function updateRow(rows: RequestRow[], id: number, change: Partial<RequestRow>): RequestRow[] {
  return rows.map(row => row.id === id ? { ...row, ...change } : row);
}

export class RequestEditorStore {
  url = "";
  method: RequestMethod = "GET";
  tab: RequestTab = "Params";
  params: RequestRow[] = [makeRow(1)];
  headers: RequestRow[] = [makeRow(2)];
  body = "";
  private nextRowId = 3;

  constructor() {
    makeAutoObservable(this);
  }

  get validation(): RequestValidation {
    return validateDraft(this);
  }

  get finalUrl(): string {
    return this.validation.finalUrl;
  }

  get hiddenValidationError(): string | null {
    return hiddenValidationMessage(this.validation, this.tab);
  }

  get canSend(): boolean {
    return this.url.trim() !== "" && this.validation.validationError === null;
  }

  get canSave(): boolean {
    return this.canSend;
  }

  get tracerRequest(): TracerRequest {
    const sendBody = this.body.trim() !== "" && supportsBody(this.method);
    return {
      url: this.finalUrl,
      method: this.method,
      headers: headersRecord(this.headers),
      ...(sendBody ? { body: this.body } : {}),
    };
  }

  get activeParams(): RequestRow[] {
    return activeRows(this.params);
  }

  setUrl(value: string): void {
    this.url = value;
  }

  setMethod(value: RequestMethod): void {
    this.method = value;
  }

  setTab(value: RequestTab): void {
    this.tab = value;
  }

  setBody(value: string): void {
    this.body = value;
  }

  updateParam(id: number, change: Partial<RequestRow>): void {
    this.params = updateRow(this.params, id, change);
  }

  updateHeader(id: number, change: Partial<RequestRow>): void {
    this.headers = updateRow(this.headers, id, change);
  }

  addParam(): void {
    this.params.push(makeRow(this.nextRowId));
    this.nextRowId += 1;
  }

  addHeader(): void {
    this.headers.push(makeRow(this.nextRowId));
    this.nextRowId += 1;
  }

  removeParam(id: number): void {
    this.params = this.params.filter(row => row.id !== id);
  }

  removeHeader(id: number): void {
    this.headers = this.headers.filter(row => row.id !== id);
  }

  // Restores a history or saved request. Redacted values stay blank and disabled until the user enters them.
  loadRequest(source: RequestSource): void {
    this.url = originAndPath(source.url);
    const restoredParams = rowsFromUrl(source.url, this.nextRowId, source.redactedQueryParameters ?? []);
    this.params = restoredParams.length ? restoredParams : [makeRow(this.nextRowId)];
    const firstHeaderId = this.nextRowId + Math.max(1, restoredParams.length);
    const restoredHeaders = source.headers.map((header, index) => ({
      id: firstHeaderId + index,
      key: header.name,
      value: header.redacted ? "" : header.value,
      enabled: !header.redacted,
      redacted: header.redacted,
    }));
    this.headers = restoredHeaders.length ? restoredHeaders : [makeRow(firstHeaderId)];
    this.nextRowId = firstHeaderId + Math.max(1, restoredHeaders.length);
    this.method = source.method;
    this.body = source.body ?? "";
    this.tab = "Params";
  }
}
