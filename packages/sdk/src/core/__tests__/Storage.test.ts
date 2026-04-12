import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { Storage } from '../Storage';

describe('Storage', () => {
  let storage: Storage;

  beforeEach(() => {
    storage = new Storage();
    document.cookie.split(';').forEach((c) => {
      document.cookie = c.replace(/^ +/, '').replace(/=.*/, `=;expires=${new Date().toUTCString()};path=/`);
    });
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it('should generate and return anonymous id if not set', () => {
    const aid = storage.getAnonymousId();
    expect(aid).toBeDefined();
    expect(aid.length).toBeGreaterThan(0);
    expect(storage.getAnonymousId()).toBe(aid);
  });

  it('should set and get user id', () => {
    const userId = 'user-123';
    storage.setUserId(userId);
    expect(storage.getUserId()).toBe(userId);
  });

  it('should clear user id', () => {
    const userId = 'user-123';
    storage.setUserId(userId);
    storage.clearUserId();
    expect(storage.getUserId()).toBeNull();
  });

  it('should set and get traits', () => {
    const traits = { name: 'John Doe', plan: 'pro' };
    storage.setTraits(traits);
    expect(storage.getTraits()).toEqual(traits);
  });

  it('should fallback to empty object if traits are empty or invalid', () => {
    expect(storage.getTraits()).toEqual({});
    storage.set('_mc_traits', 'invalid-json');
    expect(storage.getTraits()).toEqual({});
  });

  it('should clear traits', () => {
    storage.setTraits({ name: 'John' });
    storage.clearTraits();
    expect(storage.getTraits()).toEqual({});
  });

  it('should store values in both cookie and localStorage', () => {
    storage.set('test_key', 'test_value', 1);
    
    expect(window.localStorage.getItem('test_key')).toBe('test_value');
    
    expect(document.cookie).toContain('test_key=test_value');
  });

  it('should preferentially return from cookie over localStorage', () => {
    window.localStorage.setItem('test_key', 'ls_value');
    expect(storage.get('test_key')).toBe('ls_value');

    document.cookie = 'test_key=cookie_value';
    expect(storage.get('test_key')).toBe('cookie_value');
  });
});
