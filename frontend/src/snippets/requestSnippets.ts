import type { TracerRequest } from "../api/types";

export type SnippetLanguage = "curl" | "fetch";

export const snippetLanguages: readonly SnippetLanguage[] = ["curl", "fetch"];

export const snippetLabels: Record<SnippetLanguage, string> = {
  curl: "curl",
  fetch: "JavaScript fetch",
};

// POSIX single-quote escaping: close the quote, add an escaped quote, reopen.
// Newlines stay inside the single quotes, which the shell passes through unchanged.
export function shellQuote(value: string): string {
  return `'${value.replace(/'/g, `'\\''`)}'`;
}

export function buildCurlSnippet(request: TracerRequest): string {
  const lines = [`curl${request.method === "GET" ? "" : ` -X ${request.method}`} ${shellQuote(request.url)}`];
  for (const [name, value] of Object.entries(request.headers)) {
    lines.push(`  -H ${shellQuote(`${name}: ${value}`)}`);
  }
  if (request.body !== undefined) {
    lines.push(`  --data-raw ${shellQuote(request.body)}`);
  }
  return lines.join(" \\\n");
}

// JSON.stringify produces a valid JavaScript string literal, so quotes and newlines are escaped.
export function buildFetchSnippet(request: TracerRequest): string {
  const options = [`  method: ${JSON.stringify(request.method)}`];
  const headerEntries = Object.entries(request.headers);
  if (headerEntries.length > 0) {
    const headerLines = headerEntries.map(([name, value]) => `    ${JSON.stringify(name)}: ${JSON.stringify(value)}`);
    options.push(`  headers: {\n${headerLines.join(",\n")}\n  }`);
  }
  if (request.body !== undefined) {
    options.push(`  body: ${JSON.stringify(request.body)}`);
  }
  return [
    `const response = await fetch(${JSON.stringify(request.url)}, {`,
    options.join(",\n"),
    "});",
  ].join("\n");
}

export function buildSnippet(language: SnippetLanguage, request: TracerRequest): string {
  return language === "curl" ? buildCurlSnippet(request) : buildFetchSnippet(request);
}
