import { Storage } from './Storage';

const SESSION_ID_KEY = '_mc_sid';
const LAST_ACTIVE_KEY = '_mc_last_active';
const DEFAULT_TIMEOUT_MS = 30 * 60 * 1000; 

export class SessionManager {
  private storage: Storage;
  private sessionTimeout: number;

  constructor(storage: Storage, sessionTimeout?: number) {
    this.storage = storage;
    this.sessionTimeout = sessionTimeout || DEFAULT_TIMEOUT_MS;
  }

  public getSessionId(): string {
    let sessionId = this.storage.get(SESSION_ID_KEY);
    const lastActiveStr = this.storage.get(LAST_ACTIVE_KEY);
    
    const now = Date.now();
    const lastActive = lastActiveStr ? parseInt(lastActiveStr, 10) : 0;

    if (!sessionId || (now - lastActive > this.sessionTimeout)) {
      sessionId = this.generateUUID();
      this.storage.set(SESSION_ID_KEY, sessionId, 1);
    }

    this.updateLastActive();

    return sessionId;
  }

  public updateLastActive(): void {
    this.storage.set(LAST_ACTIVE_KEY, Date.now().toString(), 1);
  }

  public resetSession(): void {
    this.storage.remove(SESSION_ID_KEY);
    this.storage.remove(LAST_ACTIVE_KEY);
  }

  private generateUUID(): string {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
      return crypto.randomUUID();
    }
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
      const r = (Math.random() * 16) | 0,
        v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}
