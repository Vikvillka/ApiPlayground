import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, afterEach } from "vitest";
import { RootStoreProvider, useRootStore } from "../../stores/StoreContext";
import { CodeSnippet } from "./CodeSnippet";
import type { ReactNode } from "react";

function Wrapper({ children, onReady }: { children: ReactNode; onReady: (store: ReturnType<typeof useRootStore>) => void }) {
  const store = useRootStore();
  onReady(store);
  return <>{children}</>;
}

describe("CodeSnippet", () => {
  it("shows a curl snippet for the current request and switches to fetch", () => {
    let store: ReturnType<typeof useRootStore> | undefined;
    render(
      <RootStoreProvider>
        <Wrapper onReady={s => { store = s; }}>
          <CodeSnippet />
        </Wrapper>
      </RootStoreProvider>,
    );
    act(() => {
      store!.editor.setUrl("https://example.com/items");
      store!.editor.setMethod("POST");
      store!.editor.setBody("{\"name\":\"x\"}");
    });

    expect(screen.getByText(/^curl -X POST 'https:\/\/example\.com\/items'/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "JavaScript fetch" }));
    expect(screen.getByText(/await fetch\("https:\/\/example\.com\/items"/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "JavaScript fetch" })).toHaveAttribute("aria-pressed", "true");
  });

  describe("copy", () => {
    const originalClipboard = Object.getOwnPropertyDescriptor(navigator, "clipboard");

    afterEach(() => {
      if (originalClipboard) Object.defineProperty(navigator, "clipboard", originalClipboard);
      else delete (navigator as { clipboard?: unknown }).clipboard;
    });

    function setup() {
      let store: ReturnType<typeof useRootStore> | undefined;
      render(
        <RootStoreProvider>
          <Wrapper onReady={s => { store = s; }}>
            <CodeSnippet />
          </Wrapper>
        </RootStoreProvider>,
      );
      act(() => {
        store!.editor.setUrl("https://example.com/items");
        store!.editor.setMethod("POST");
        store!.editor.setBody("{\"name\":\"x\"}");
      });
    }

    it("copies the visible curl snippet to the clipboard", async () => {
      const writeText = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
      setup();
      const visible = screen.getByText(/^curl -X POST/).textContent ?? "";

      fireEvent.click(screen.getByRole("button", { name: "Copy" }));

      await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("Copied"));
      expect(writeText).toHaveBeenCalledWith(visible);
    });

    it("copies the fetch snippet after switching language", async () => {
      const writeText = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
      setup();
      fireEvent.click(screen.getByRole("button", { name: "JavaScript fetch" }));
      const visible = screen.getByText(/await fetch\(/).textContent ?? "";

      fireEvent.click(screen.getByRole("button", { name: "Copy" }));

      await waitFor(() => expect(writeText).toHaveBeenCalledWith(visible));
    });

    it("shows an error without throwing when the clipboard write is rejected", async () => {
      const writeText = vi.fn().mockRejectedValue(new Error("denied"));
      Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
      setup();

      fireEvent.click(screen.getByRole("button", { name: "Copy" }));

      await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Could not copy snippet"));
    });
  });
});
