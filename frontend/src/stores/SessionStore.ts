import { makeAutoObservable, runInAction } from "mobx";
import { fetchCurrentUser, logOut, type AccountProfile } from "../api/authApi";
import { errorMessage } from "../request/requestHelpers";

export type SessionStatus = "checking" | "signedOut" | "signedIn";

export class SessionStore {
  status: SessionStatus;
  userId: string | null;
  email: string | null;
  // Shown on the auth screen, for example after the session expired.
  notice: string | null = null;
  logOutError: string | null = null;

  constructor(initial: { status: SessionStatus; userId?: string | null; email?: string | null } = { status: "checking" }) {
    this.status = initial.status;
    this.userId = initial.userId ?? null;
    this.email = initial.email ?? null;
    makeAutoObservable(this);
  }

  // Asks the server who the cookie belongs to. Any failure is treated as signed out.
  async restore(): Promise<void> {
    let profile: AccountProfile | null = null;
    try {
      profile = await fetchCurrentUser();
    } catch {
      profile = null;
    }
    runInAction(() => {
      if (profile) this.signIn(profile);
      else this.setSignedOut(null);
    });
  }

  signIn(profile: AccountProfile): void {
    this.status = "signedIn";
    this.userId = profile.id;
    this.email = profile.email;
    this.notice = null;
    this.logOutError = null;
  }

  // Called when a protected API call answers 401 while signed in.
  expire(): void {
    if (this.status !== "signedIn") return;
    this.setSignedOut("Please sign in again.");
  }

  async logOut(): Promise<void> {
    this.logOutError = null;
    try {
      await logOut();
      runInAction(() => this.setSignedOut(null));
    } catch (cause) {
      runInAction(() => { this.logOutError = errorMessage(cause, "Log out failed. Try again."); });
    }
  }

  setSignedOut(notice: string | null): void {
    this.status = "signedOut";
    this.userId = null;
    this.email = null;
    this.notice = notice;
    this.logOutError = null;
  }
}
