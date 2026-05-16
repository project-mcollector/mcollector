import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { SessionManager } from '../SessionManager';
import { Storage } from '../Storage';

describe('SessionManager', () => {
  let storage: Storage;
  let sessionManager: SessionManager;

  beforeEach(() => {
    storage = new Storage();
    window.localStorage.clear();
    document.cookie.split(';').forEach((c) => {
      document.cookie = c.replace(/^ +/, '').replace(/=.*/, `=;expires=${new Date().toUTCString()};path=/`);
    });
    vi.useFakeTimers();
    sessionManager = new SessionManager(storage);
  });

  afterEach(() => {
    vi.useRealTimers();
    window.localStorage.clear();
  });

  it('should generate a session id on first call', () => {
    const sessionId = sessionManager.getSessionId();
    expect(sessionId).toBeDefined();
    expect(sessionId.length).toBeGreaterThan(0);
  });

  it('should return the same session id within the timeout window', () => {
    const id1 = sessionManager.getSessionId();
    vi.advanceTimersByTime(1000);
    const id2 = sessionManager.getSessionId();
    expect(id1).toBe(id2);
  });

  it('should generate a new session id after timeout expires', () => {
    const id1 = sessionManager.getSessionId();
    vi.advanceTimersByTime(31 * 60 * 1000);
    const id2 = sessionManager.getSessionId();
    expect(id1).not.toBe(id2);
  });

  it('should respect a custom session timeout', () => {
    const shortTimeout = 5000;
    const customManager = new SessionManager(storage, shortTimeout);
    const id1 = customManager.getSessionId();
    vi.advanceTimersByTime(shortTimeout + 1);
    const id2 = customManager.getSessionId();
    expect(id1).not.toBe(id2);
  });

  it('should update last active timestamp on getSessionId', () => {
    sessionManager.getSessionId();
    const before = window.localStorage.getItem('_mc_last_active');
    vi.advanceTimersByTime(1000);
    sessionManager.getSessionId();
    const after = window.localStorage.getItem('_mc_last_active');
    expect(Number(after)).toBeGreaterThan(Number(before));
  });

  it('should clear session data on resetSession', () => {
    sessionManager.getSessionId();
    sessionManager.resetSession();
    expect(window.localStorage.getItem('_mc_sid')).toBeNull();
    expect(window.localStorage.getItem('_mc_last_active')).toBeNull();
  });

  it('should generate a new session id after resetSession', () => {
    const id1 = sessionManager.getSessionId();
    sessionManager.resetSession();
    const id2 = sessionManager.getSessionId();
    expect(id1).not.toBe(id2);
  });
});
