import { describe, expect, it } from "vitest";
import {
  formatResponseBody,
  headersRecord,
  hiddenValidationMessage,
  isValidUrl,
  makeRow,
  rowsFromUrl,
  validateDraft,
  type DraftForValidation,
} from "./requestHelpers";

const draft = (overrides: Partial<DraftForValidation> = {}): DraftForValidation => ({
  url: "https://api.example.com/users",
  method: "GET",
  body: "",
  params: [makeRow(1)],
  headers: [makeRow(2)],
  ...overrides,
});

describe("isValidUrl", () => {
  it("accepts absolute http and https URLs only", () => {
    expect(isValidUrl("https://example.com")).toBe(true);
    expect(isValidUrl("http://example.com/a?b=1")).toBe(true);
    expect(isValidUrl("ftp://example.com")).toBe(false);
    expect(isValidUrl("example.com")).toBe(false);
    expect(isValidUrl("https://user:pass@example.com")).toBe(false);
    expect(isValidUrl("https://example.com/#fragment")).toBe(false);
  });
});

describe("validateDraft", () => {
  it("appends enabled parameters and ignores disabled or unnamed ones", () => {
    const params = [
      { id: 1, key: "page", value: "2", enabled: true },
      { id: 2, key: "hidden", value: "x", enabled: false },
      { id: 3, key: " ", value: "y", enabled: true },
    ];
    expect(validateDraft(draft({ params })).finalUrl).toBe("https://api.example.com/users?page=2");
  });

  it("reports an invalid URL and leaves the final URL untouched", () => {
    const result = validateDraft(draft({ url: "not a url" }));
    expect(result.urlError).toBe("Enter a valid absolute HTTP or HTTPS URL.");
    expect(result.finalUrl).toBe("not a url");
    expect(result.validationError).toBe(result.urlError);
  });

  it("reports a final URL longer than 2048 characters", () => {
    const long = `https://example.com/?q=${"a".repeat(2048)}`;
    expect(validateDraft(draft({ url: long })).finalUrlError)
      .toBe("The request URL including parameters must be 2048 characters or fewer.");
  });

  it("allows JSON only for POST, PUT, and PATCH", () => {
    expect(validateDraft(draft({ method: "GET", body: "{}" })).bodyError)
      .toBe("JSON bodies are only available for POST, PUT, and PATCH.");
    expect(validateDraft(draft({ method: "POST", body: "{}" })).bodyError).toBeNull();
    expect(validateDraft(draft({ method: "POST", body: "{" })).bodyError).toBe("Enter valid JSON.");
  });

  it("treats a blank body as valid for any method", () => {
    expect(validateDraft(draft({ method: "GET", body: "   " })).bodyError).toBeNull();
  });

  it("rejects duplicate enabled header names case-insensitively", () => {
    const headers = [
      { id: 1, key: "Accept", value: "a", enabled: true },
      { id: 2, key: "accept", value: "b", enabled: true },
    ];
    expect(validateDraft(draft({ headers })).headerError).toBe("Enabled header names must be unique.");
  });

  it("ignores disabled duplicate headers", () => {
    const headers = [
      { id: 1, key: "Accept", value: "a", enabled: true },
      { id: 2, key: "accept", value: "b", enabled: false },
    ];
    expect(validateDraft(draft({ headers })).headerError).toBeNull();
  });
});

describe("hiddenValidationMessage", () => {
  it("names the hidden tab whose input is invalid", () => {
    const invalidBody = validateDraft(draft({ method: "GET", body: "{}" }));
    expect(hiddenValidationMessage(invalidBody, "Params")).toBe("Fix the Body input before sending.");
    expect(hiddenValidationMessage(invalidBody, "Body")).toBeNull();
  });

  it("names the hidden headers tab when headers are invalid", () => {
    const headers = [
      { id: 1, key: "A", value: "", enabled: true },
      { id: 2, key: "a", value: "", enabled: true },
    ];
    const invalid = validateDraft(draft({ headers }));
    expect(hiddenValidationMessage(invalid, "Body")).toBe("Fix the Headers input before sending.");
    expect(hiddenValidationMessage(invalid, "Headers")).toBeNull();
  });
});

describe("rowsFromUrl", () => {
  it("disables and blanks redacted query parameters", () => {
    const rows = rowsFromUrl("https://example.com/?token=secret&page=1", 10, ["token"]);
    expect(rows).toEqual([
      { id: 10, key: "token", value: "", enabled: false, redacted: true },
      { id: 11, key: "page", value: "1", enabled: true, redacted: false },
    ]);
  });
});

describe("headersRecord", () => {
  it("keeps only enabled, named headers with trimmed names", () => {
    const rows = [
      { id: 1, key: " Accept ", value: "json", enabled: true },
      { id: 2, key: "X-Off", value: "v", enabled: false },
      { id: 3, key: "", value: "v", enabled: true },
    ];
    expect(headersRecord(rows)).toEqual({ Accept: "json" });
  });
});

describe("formatResponseBody", () => {
  it("pretty-prints JSON and returns other text unchanged", () => {
    expect(formatResponseBody('{"a":1}')).toBe('{\n  "a": 1\n}');
    expect(formatResponseBody("plain text")).toBe("plain text");
  });
});
