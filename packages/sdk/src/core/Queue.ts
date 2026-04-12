import { EventPayload } from '../types/EventPayload';
import { InitOptions } from '../types/Options';

const QUEUE_STORAGE_KEY = '_mc_queue';

export class Queue {
  private writeKey: string;
  private options: InitOptions;
  private queue: EventPayload[] = [];
  private flushTimer: any = null;
  private isFlushing = false;
  private retryCount = 0;
  private readonly MAX_RETRIES = 5;

  constructor(writeKey: string, options: InitOptions) {
    this.writeKey = writeKey;
    this.options = options;
    
    this.loadQueue();

    if (typeof window !== 'undefined') {
      window.addEventListener('beforeunload', () => this.flushSync());
      window.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
          this.flushSync();
        }
      });
    }
  }

  public enqueue(event: EventPayload): void {
    if (this.options.debug) {
      console.log('[mcollector] Enqueue event:', event);
    }

    this.queue.push(event);
    this.saveQueue();

    const batchSize = this.options.batchSize || 10;
    
    if (this.queue.length >= batchSize) {
      this.flush();
    } else {
      this.scheduleFlush();
    }
  }

  private scheduleFlush(): void {
    if (this.flushTimer) {
      clearTimeout(this.flushTimer);
    }
    
    const interval = this.options.flushInterval || 3000;
    this.flushTimer = setTimeout(() => {
      this.flush();
    }, interval);
  }

  public async flush(): Promise<void> {
    if (this.isFlushing || this.queue.length === 0) return;
    
    this.isFlushing = true;
    
    const batch = [...this.queue];
    this.queue = [];
    this.saveQueue();

    try {
      const payload = {
        writeKey: this.writeKey,
        events: batch
      };

      const host = this.options.apiHost || 'http://localhost:5000/api/ingestion';
      const endpoint = `${host.replace(/\/$/, '')}/events`;

      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload),
        keepalive: true 
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      if (this.options.debug) {
        console.log(`[mcollector] Successfully flushed ${batch.length} events.`);
      }
      this.retryCount = 0; 
    } catch (error) {
      this.retryCount++;
      if (this.options.debug) {
        console.error(`[mcollector] Flush failed (Attempt ${this.retryCount}/${this.MAX_RETRIES}). Restoring queue.`, error);
      }
      this.queue = [...batch, ...this.queue];
      this.saveQueue();

      if (this.retryCount <= this.MAX_RETRIES) {
        const backoffDelay = Math.min(1000 * Math.pow(2, this.retryCount), 60000); 
        setTimeout(() => this.flush(), backoffDelay);
      } else {
        if (this.options.debug) {
          console.error('[mcollector] Max retries reached. Stopping automatic flush until new events arrive.');
        }
        this.retryCount = 0;
      }
    } finally {
      this.isFlushing = false;
    }
  }

  private flushSync(): void {
    if (this.queue.length === 0) return;

    const batch = [...this.queue];
    this.queue = [];
    this.saveQueue();

    const payload = {
      writeKey: this.writeKey,
      events: batch
    };

    const host = this.options.apiHost || 'http://localhost:5000/api/ingestion';
    const endpoint = `${host.replace(/\/$/, '')}/events`;

    if (typeof navigator !== 'undefined' && navigator.sendBeacon) {
      const blob = new Blob([JSON.stringify(payload)], { type: 'application/json' });
      navigator.sendBeacon(endpoint, blob);
    }
  }

  private saveQueue(): void {
    try {
      if (typeof window !== 'undefined') {
        window.localStorage.setItem(QUEUE_STORAGE_KEY, JSON.stringify(this.queue));
      }
    } catch (e) {}
  }

  private loadQueue(): void {
    try {
      if (typeof window !== 'undefined') {
        const stored = window.localStorage.getItem(QUEUE_STORAGE_KEY);
        if (stored) {
          this.queue = JSON.parse(stored);
        }
      }
    } catch (e) {}
  }
}
