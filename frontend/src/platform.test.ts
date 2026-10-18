import { describe, expect, it } from "vitest";
import { platformName } from "./platform";

describe("platform scaffold", () => {
  it("exposes the application name", () => {
    expect(platformName).toBe("ApiPlayground");
  });
});