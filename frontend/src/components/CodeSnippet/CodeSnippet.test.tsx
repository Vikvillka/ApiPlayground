import { act, fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
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
});
