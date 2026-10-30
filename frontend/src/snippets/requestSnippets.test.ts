import { describe, expect, it } from "vitest";
import type { TracerRequest } from "../api/types";
import { buildCurlSnippet, buildFetchSnippet, buildSnippet, shellQuote } from "./requestSnippets";

const getRequest: TracerRequest = {
  url: "https://example.com/items?page=2&q=a%20b",
  method: "GET",
  headers: {},
};

const postRequest: TracerRequest = {
  url: "https://example.com/items",
  method: "POST",
  headers: { "Content-Type": "application/json", "X-Name": "it's" },
  body: "{\n  \"name\": \"O'Neil\"\n}",
};

describe("shellQuote", () => {
  it("escapes single quotes for POSIX shells", () => {
    expect(shellQuote("it's")).toBe("'it'\\''s'");
  });
});

describe("buildCurlSnippet", () => {
  it("builds a GET with the final URL including query parameters and no -X flag", () => {
    expect(buildCurlSnippet(getRequest)).toBe("curl 'https://example.com/items?page=2&q=a%20b'");
  });

  it("builds a POST with method, headers, and a quoted body", () => {
    const snippet = buildCurlSnippet(postRequest);
    expect(snippet).toContain("curl -X POST 'https://example.com/items' \\\n");
    expect(snippet).toContain("  -H 'Content-Type: application/json' \\\n");
    expect(snippet).toContain("  -H 'X-Name: it'\\''s' \\\n");
    expect(snippet).toContain("  --data-raw '{\n  \"name\": \"O'\\''Neil\"\n}'");
  });
});

describe("buildFetchSnippet", () => {
  it("builds a GET with the final URL and no headers or body", () => {
    expect(buildFetchSnippet(getRequest)).toBe([
      "const response = await fetch(\"https://example.com/items?page=2&q=a%20b\", {",
      "  method: \"GET\"",
      "});",
    ].join("\n"));
  });

  it("builds a POST with escaped headers and body", () => {
    expect(buildFetchSnippet(postRequest)).toBe([
      "const response = await fetch(\"https://example.com/items\", {",
      "  method: \"POST\",",
      "  headers: {",
      "    \"Content-Type\": \"application/json\",",
      "    \"X-Name\": \"it's\"",
      "  },",
      "  body: \"{\\n  \\\"name\\\": \\\"O'Neil\\\"\\n}\"",
      "});",
    ].join("\n"));
  });
});

describe("buildSnippet", () => {
  it("dispatches to the selected language", () => {
    expect(buildSnippet("curl", getRequest)).toBe(buildCurlSnippet(getRequest));
    expect(buildSnippet("fetch", getRequest)).toBe(buildFetchSnippet(getRequest));
  });
});
