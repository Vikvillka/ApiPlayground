import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { RootStoreProvider, useRootStore } from "../../stores/StoreContext";
import { RequestExport } from "./RequestExport";

function Wrapper({ children, onReady }: { children: ReactNode; onReady: (store: ReturnType<typeof useRootStore>) => void }) {
  const store = useRootStore();
  onReady(store);
  return <>{children}</>;
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function readBlob(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error);
    reader.readAsText(blob);
  });
}

describe("RequestExport", () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let downloadNames: string[];
  let downloadedBlobs: Blob[];

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    downloadNames = [];
    downloadedBlobs = [];
    URL.createObjectURL = vi.fn((blob: Blob) => {
      downloadedBlobs.push(blob);
      return "blob:export";
    });
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      downloadNames.push(this.download);
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    Reflect.deleteProperty(URL, "createObjectURL");
    Reflect.deleteProperty(URL, "revokeObjectURL");
  });

  function setup() {
    let store: ReturnType<typeof useRootStore> | undefined;
    render(
      <RootStoreProvider>
        <Wrapper onReady={s => { store = s; }}>
          <RequestExport />
        </Wrapper>
      </RootStoreProvider>,
    );
    act(() => {
      store!.editor.setUrl("https://example.com/items");
    });
  }

  it("saves the export as a JSON file named apiplayground-export.json", async () => {
    const document = { format: "apiplayground.export.v1" };
    fetchMock.mockResolvedValue(jsonResponse(document));
    setup();

    fireEvent.click(screen.getByRole("button", { name: "Export JSON" }));

    await waitFor(() => expect(downloadNames).toEqual(["apiplayground-export.json"]));
    expect(downloadedBlobs).toHaveLength(1);
    await expect(readBlob(downloadedBlobs[0])).resolves.toBe(JSON.stringify(document, null, 2));
  });

  it("shows the server message and does not download when the export fails", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ detail: "Export is not available." }, 500));
    setup();

    fireEvent.click(screen.getByRole("button", { name: "Export JSON" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Export is not available."));
    expect(downloadNames).toEqual([]);
  });
});
