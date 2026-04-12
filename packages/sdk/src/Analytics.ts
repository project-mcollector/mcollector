/** Main entry point for the Analytics SDK. Exposes initialization and tracking methods (track, identify, page). */
import { InitOptions } from './types/Options';
import { Storage } from './core/Storage';
import { Queue } from './core/Queue';
import { SessionManager } from './core/SessionManager';
import { EventBuilder } from './core/EventBuilder';
import { defaultOptions } from './config';

export class Analytics {
  private writeKey!: string;
  private options!: InitOptions;
  private storage!: Storage;
  private queue!: Queue;
  private sessionManager!: SessionManager;
  private builder!: EventBuilder;
  private initialized = false;

  public init(writeKey: string, options: InitOptions = {}) {
    if (this.initialized) return;

    this.writeKey = writeKey;
    this.options = { ...defaultOptions, ...options };
    
    this.storage = new Storage(this.options.cookieDomain);
    this.sessionManager = new SessionManager(this.storage, this.options.sessionTimeoutConfig);
    this.queue = new Queue(this.writeKey, this.options);
    this.builder = new EventBuilder(this.storage, this.sessionManager);

    this.initialized = true;

    if (this.options.autoTrackPages) {
      this.setupAutoPageTracking();
    }
  }

  public track(eventName: string, properties: Record<string, any> = {}): void {
    if (!this.checkInitialized()) return;
    
    const event = this.builder.buildTrackEvent(eventName, properties);
    this.queue.enqueue(event);
  }

  public identify(userId: string, traits: Record<string, any> = {}): void {
    if (!this.checkInitialized()) return;


    this.storage.setUserId(userId);
    this.storage.setTraits(traits);

    const event = this.builder.buildIdentifyEvent(userId, traits);
    this.queue.enqueue(event);
  }

  public page(name?: string, category?: string, properties: Record<string, any> = {}): void {
    if (!this.checkInitialized()) return;

    const event = this.builder.buildPageEvent(name, category, properties);
    this.queue.enqueue(event);
  }

  public reset(): void {
    this.storage.clearUserId();
    this.storage.clearTraits();
    this.sessionManager.resetSession();
  }

  public flush(): Promise<void> {
    return this.queue.flush();
  }

  private checkInitialized(): boolean {
    if (!this.initialized) {
      console.warn('Analytics is not initialized. Call init() first.');
      return false;
    }
    return true;
  }

  private setupAutoPageTracking() {
    if (typeof window === 'undefined') return;

    this.page();

    window.addEventListener('popstate', () => {
      this.page();
    });

    const originalPushState = history.pushState;
    history.pushState = (...args) => {
      originalPushState.apply(history, args);
      setTimeout(() => this.page(), 0);
    };

    const originalReplaceState = history.replaceState;
    history.replaceState = (...args) => {
      originalReplaceState.apply(history, args);
      setTimeout(() => this.page(), 0);
    };
  }
}


export const analytics = new Analytics();