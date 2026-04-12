import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { Queue } from '../Queue';
import { InitOptions } from '../../types/Options';
import { EventPayload } from '../../types/EventPayload';

describe('Queue', () => {
  let queue: Queue;
  let options: InitOptions;
  const writeKey = 'test-write-key';

  const createMockEvent = (id: string): EventPayload => ({
    messageId: id,
    timestamp: new Date().toISOString(),
    event: 'Test Event',
    properties: {},
    context: { url: '', referrer: '', userAgent: '', screen: { width: 0, height: 0 }, library: { name: '', version: '' } },
    anonymousId: 'anon-123',
    sessionId: 'session-123'
  });

  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal('fetch', vi.fn());

    options = {
      apiHost: 'http://localhost:5000/api/ingestion',
      debug: false,
      batchSize: 3, 
      flushInterval: 3000
    };

    window.localStorage.clear();
    queue = new Queue(writeKey, options);
  });

  afterEach(() => {
    vi.clearAllTimers();
    vi.restoreAllMocks();
    window.localStorage.clear();
  });

  it('should enqueue event and save it to localStorage', () => {
    const event = createMockEvent('1');
    queue.enqueue(event);

    const storedStr = window.localStorage.getItem('_mc_queue');
    expect(storedStr).toBeDefined();
    
    if (storedStr) {
      const stored = JSON.parse(storedStr);
      expect(stored.length).toBe(1);
      expect(stored[0].messageId).toBe('1');
    }
  });

  it('should auto-flush when batch size is reached', () => {
    (globalThis.fetch as any).mockResolvedValue({ ok: true, status: 200 });

    queue.enqueue(createMockEvent('1'));
    queue.enqueue(createMockEvent('2'));
    
    expect(globalThis.fetch).not.toHaveBeenCalled();

    queue.enqueue(createMockEvent('3'));

    expect(globalThis.fetch).toHaveBeenCalledTimes(1);
    
    const stored = JSON.parse(window.localStorage.getItem('_mc_queue') || '[]');
    expect(stored.length).toBe(0);
  });

  it('should flush when interval timer expires', () => {
    (globalThis.fetch as any).mockResolvedValue({ ok: true, status: 200 });

    queue.enqueue(createMockEvent('1'));
    expect(globalThis.fetch).not.toHaveBeenCalled();

    vi.advanceTimersByTime(3000);

    expect(globalThis.fetch).toHaveBeenCalledTimes(1);
  });

  it('should implement exponential backoff on fetch failure', async () => {
    let fetchPromiseResolver: () => void;
    let fetchPromise = new Promise<Response>((resolve, reject) => {
      fetchPromiseResolver = () => reject(new Error('Network error'));
    });

    (globalThis.fetch as any).mockImplementation(() => fetchPromise);

    queue.enqueue({ ...createMockEvent('1'), messageId: '1' } as EventPayload);
    queue.enqueue({ ...createMockEvent('2'), messageId: '2' } as EventPayload);
    
    expect(globalThis.fetch).not.toHaveBeenCalled();

    queue.enqueue({ ...createMockEvent('3'), messageId: '3' } as EventPayload);
    
    expect(globalThis.fetch).toHaveBeenCalledTimes(1);

    fetchPromiseResolver!();
    
    await Promise.resolve();

    expect(globalThis.fetch).toHaveBeenCalledTimes(1);

    fetchPromise = new Promise<Response>((resolve, reject) => {
      fetchPromiseResolver = () => reject(new Error('Network error'));
    });
    vi.advanceTimersByTime(2000);
    expect(globalThis.fetch).toHaveBeenCalledTimes(2);

    fetchPromiseResolver!();
    await Promise.resolve();

    fetchPromise = new Promise<Response>((resolve, reject) => {
      fetchPromiseResolver = () => reject(new Error('Network error'));
    });
    vi.advanceTimersByTime(4000);
    expect(globalThis.fetch).toHaveBeenCalledTimes(3);

    fetchPromiseResolver!();
    await Promise.resolve();
  });
});
