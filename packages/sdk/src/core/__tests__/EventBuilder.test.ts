import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { EventBuilder } from '../EventBuilder';
import { Storage } from '../Storage';
import { SessionManager } from '../SessionManager';

describe('EventBuilder', () => {
  let storage: Storage;
  let sessionManager: SessionManager;
  let builder: EventBuilder;

  beforeEach(() => {
    window.localStorage.clear();
    document.cookie.split(';').forEach((c) => {
      document.cookie = c.replace(/^ +/, '').replace(/=.*/, `=;expires=${new Date().toUTCString()};path=/`);
    });
    storage = new Storage();
    sessionManager = new SessionManager(storage);
    builder = new EventBuilder(storage, sessionManager);
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  describe('buildTrackEvent', () => {
    it('should return an event with the given name and properties', () => {
      const event = builder.buildTrackEvent('Button Clicked', { color: 'blue' });
      expect(event.event).toBe('Button Clicked');
      expect(event.properties).toEqual({ color: 'blue' });
    });

    it('should include a messageId, timestamp, anonymousId, and sessionId', () => {
      const event = builder.buildTrackEvent('test', {});
      expect(event.messageId).toBeDefined();
      expect(event.timestamp).toBeDefined();
      expect(event.anonymousId).toBeDefined();
      expect(event.sessionId).toBeDefined();
    });

    it('should include userId when one is set in storage', () => {
      storage.setUserId('user-42');
      const event = builder.buildTrackEvent('test', {});
      expect(event.userId).toBe('user-42');
    });

    it('should not include userId when none is set', () => {
      const event = builder.buildTrackEvent('test', {});
      expect(event.userId).toBeUndefined();
    });

    it('should generate unique messageIds for each event', () => {
      const e1 = builder.buildTrackEvent('a', {});
      const e2 = builder.buildTrackEvent('b', {});
      expect(e1.messageId).not.toBe(e2.messageId);
    });
  });

  describe('buildIdentifyEvent', () => {
    it('should use $identify as event name', () => {
      const event = builder.buildIdentifyEvent('user-1', { name: 'Alice' });
      expect(event.event).toBe('$identify');
    });

    it('should set userId to the provided value', () => {
      const event = builder.buildIdentifyEvent('user-1', {});
      expect(event.userId).toBe('user-1');
    });

    it('should put traits in properties', () => {
      const traits = { name: 'Alice', plan: 'pro' };
      const event = builder.buildIdentifyEvent('user-1', traits);
      expect(event.properties).toEqual(traits);
    });
  });

  describe('buildPageEvent', () => {
    it('should use $pageview as event name', () => {
      const event = builder.buildPageEvent();
      expect(event.event).toBe('$pageview');
    });

    it('should include name and category in properties', () => {
      const event = builder.buildPageEvent('Home', 'Marketing', {});
      expect(event.properties.name).toBe('Home');
      expect(event.properties.category).toBe('Marketing');
    });

    it('should include path, url, search, and title from window/document', () => {
      const event = builder.buildPageEvent();
      expect(event.properties).toHaveProperty('path');
      expect(event.properties).toHaveProperty('url');
      expect(event.properties).toHaveProperty('search');
      expect(event.properties).toHaveProperty('title');
    });

    it('should merge extra properties', () => {
      const event = builder.buildPageEvent(undefined, undefined, { referrer: 'google.com' });
      expect(event.properties.referrer).toBe('google.com');
    });
  });
});
