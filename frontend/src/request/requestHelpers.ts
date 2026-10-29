import type { TracerRequest } from "../tracer";

export type RequestMethod = TracerRequest["method"];
export type RequestTab = "Params" | "Headers" | "Body";
export type ResponseTab = "Body" | "Headers";

export type RequestRow = { id: number; key: string; value: string; enabled: boolean; redacted?: boolean };

export type DraftForValidation = {
  url: string;
  method: RequestMethod;
  body: string;
  params: RequestRow[];
  headers: RequestRow[];
};

export type RequestValidation = {
  finalUrl: string;
  urlError: string | null;
  finalUrlError: string | null;
  bodyError: string | null;
  headerError: string | null;
  validationError: string | null;
};

export const requestMethods: readonly RequestMethod[] = ["GET", "POST", "PUT", "PATCH", "DELETE"];

export const MAX_URL_LENGTH = 2048;

export function makeRow(id: number): RequestRow {
  return { id, key: "", value: "", enabled: true };
}

export function errorMessage(cause: unknown, fallback: string): string {
  return cause instanceof Error ? cause.message : fallback;
}

export function supportsBody(method: RequestMethod): boolean {
  return method === "POST" || method === "PUT" || method === "PATCH";
}

export function isValidUrl(value: string): boolean {
  try {
    const parsed = new URL(value);
    return (parsed.protocol === "http:" || parsed.protocol === "https:") && !parsed.username && !parsed.password && !parsed.hash;
  } catch {
    return false;
  }
}

export function activeRows(rows: RequestRow[]): RequestRow[] {
  return rows.filter(row => row.enabled && row.key.trim());
}

export function hasDuplicateHeader(rows: RequestRow[]): boolean {
  const active = activeRows(rows);
  return active.some((row, index) => active.slice(0, index)
    .some(previous => previous.key.trim().toLowerCase() === row.key.trim().toLowerCase()));
}

export function headersRecord(rows: RequestRow[]): Record<string, string> {
  return Object.fromEntries(activeRows(rows).map(row => [row.key.trim(), row.value]));
}

export function addEnabledParameters(url: string, parameters: RequestRow[]): string {
  const parsed = new URL(url);
  for (const parameter of parameters) parsed.searchParams.append(parameter.key, parameter.value);
  return parsed.toString();
}

export function rowsFromUrl(url: string, firstId: number, redactedNames: string[] = []): RequestRow[] {
  const parsed = new URL(url);
  return [...parsed.searchParams.entries()].map(([key, value], index) => {
    const redacted = redactedNames.some(name => name.toLowerCase() === key.toLowerCase());
    return { id: firstId + index, key, value: redacted ? "" : value, enabled: !redacted, redacted };
  });
}

export function originAndPath(url: string): string {
  const parsed = new URL(url);
  return `${parsed.origin}${parsed.pathname}${parsed.hash}`;
}

export function defaultSaveTitle(method: RequestMethod, url: string): string {
  return `${method} ${new URL(url).pathname || "/"}`;
}

export function formatResponseBody(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function bodyValidationError(method: RequestMethod, body: string): string | null {
  if (!body.trim()) return null;
  if (!supportsBody(method)) return "JSON bodies are only available for POST, PUT, and PATCH.";
  try {
    JSON.parse(body);
    return null;
  } catch {
    return "Enter valid JSON.";
  }
}

export function validateDraft(draft: DraftForValidation): RequestValidation {
  const hasUrl = draft.url.trim() !== "";
  const urlError = hasUrl && !isValidUrl(draft.url) ? "Enter a valid absolute HTTP or HTTPS URL." : null;
  const finalUrl = hasUrl && !urlError ? addEnabledParameters(draft.url, activeRows(draft.params)) : draft.url;
  const finalUrlError = hasUrl && !urlError && finalUrl.length > MAX_URL_LENGTH
    ? `The request URL including parameters must be ${MAX_URL_LENGTH} characters or fewer.`
    : null;
  const bodyError = bodyValidationError(draft.method, draft.body);
  const headerError = hasDuplicateHeader(draft.headers) ? "Enabled header names must be unique." : null;
  const validationError = urlError ?? finalUrlError ?? bodyError ?? headerError;
  return { finalUrl, urlError, finalUrlError, bodyError, headerError, validationError };
}

// Errors on a tab that is not visible must still block sending, so the user is told which tab to fix.
export function hiddenValidationMessage(validation: RequestValidation, tab: RequestTab): string | null {
  if (tab !== "Body" && validation.bodyError) return "Fix the Body input before sending.";
  if (tab !== "Headers" && validation.headerError) return "Fix the Headers input before sending.";
  return null;
}
