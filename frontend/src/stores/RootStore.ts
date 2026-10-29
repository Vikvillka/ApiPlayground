import { makeAutoObservable } from "mobx";

// Request editor, tracer, history, collections, and save-request stores are added in the pure-helpers-and-stores story.
export class RootStore {
  constructor() {
    makeAutoObservable(this);
  }
}
