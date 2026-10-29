import { createContext, useContext, useState, type ReactNode } from "react";
import { RootStore } from "./RootStore";

const StoreContext = createContext<RootStore | null>(null);

export function RootStoreProvider({ children }: { children: ReactNode }) {
  const [store] = useState(() => new RootStore());
  return <StoreContext.Provider value={store}>{children}</StoreContext.Provider>;
}

export function useRootStore(): RootStore {
  const store = useContext(StoreContext);
  if (!store) {
    throw new Error("useRootStore must be used inside RootStoreProvider.");
  }
  return store;
}
