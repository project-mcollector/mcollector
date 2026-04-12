import { Storage } from './Storage';
import { SessionManager } from './SessionManager';
import { getEventContext } from '../utils/getEventContext';
import { EventPayload } from '../types/EventPayload';

export class EventBuilder {
  private storage: Storage;
  private sessionManager: SessionManager;

  constructor(storage: Storage, sessionManager: SessionManager) {
    this.storage = storage;
    this.sessionManager = sessionManager;
  }

  private buildBaseEvent(): Omit<EventPayload, 'event' | 'properties'> {
    return {
      messageId: this.generateUUID(),
      timestamp: new Date().toISOString(),
      context: getEventContext(),
      anonymousId: this.storage.getAnonymousId(),
      userId: this.storage.getUserId() || undefined,
      sessionId: this.sessionManager.getSessionId()
    };
  }

  public buildTrackEvent(eventName: string, properties: Record<string, any>): EventPayload {
    return {
      ...this.buildBaseEvent(),
      event: eventName,
      properties
    };
  }

  public buildIdentifyEvent(userId: string, traits: Record<string, any>): EventPayload {
    return {
      ...this.buildBaseEvent(),
      event: '$identify', 
      userId: userId,     
      properties: traits  
    };
  }

  public buildPageEvent(name?: string, category?: string, properties: Record<string, any> = {}): EventPayload {
    const pageProperties = {
      ...properties,
      name,
      category,
      path: typeof window !== 'undefined' ? window.location.pathname : '',
      url: typeof window !== 'undefined' ? window.location.href : '',
      search: typeof window !== 'undefined' ? window.location.search : '',
      title: typeof document !== 'undefined' ? document.title : '',
    };

    return {
      ...this.buildBaseEvent(),
      event: '$pageview',
      properties: pageProperties
    };
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
