import { afterEach, describe, expect, it, vi } from 'vitest';
import { ErrorCollector } from '../ErrorCollector';

describe('ErrorCollector', () => {
  let collector: ErrorCollector | undefined;

  afterEach(() => {
    collector?.stop();
    collector = undefined;
    window.onerror = null;
  });

  it('tracks errors received through window.onerror', () => {
    const transport = { track: vi.fn() };
    collector = new ErrorCollector(transport);
    collector.start();
    const error = new Error('boom');

    window.onerror?.('boom', 'https://app.test/main.js', 12, 4, error);

    expect(transport.track).toHaveBeenCalledWith(
      '$exception',
      expect.objectContaining({
        mechanism: 'window.onerror',
        handled: false,
        message: 'boom',
        source: 'https://app.test/main.js',
        lineno: 12,
        colno: 4,
        error: expect.objectContaining({
          name: 'Error',
          message: 'boom',
          stack: expect.any(String),
        }),
      }),
    );
  });

  it('preserves an existing window.onerror handler', () => {
    const previous = vi.fn(() => true);
    const transport = { track: vi.fn() };
    window.onerror = previous;
    collector = new ErrorCollector(transport);
    collector.start();

    const result = window.onerror?.('boom', 'https://app.test/main.js', 1, 2, new Error('boom'));

    expect(previous).toHaveBeenCalled();
    expect(result).toBe(true);
  });

  it('tracks resource load errors from the error event listener', () => {
    const transport = { track: vi.fn() };
    collector = new ErrorCollector(transport);
    collector.start();
    const script = document.createElement('script');
    script.src = 'https://cdn.test/app.js';
    document.body.appendChild(script);

    script.dispatchEvent(new Event('error'));

    expect(transport.track).toHaveBeenCalledWith(
      '$exception',
      expect.objectContaining({
        mechanism: 'error_event',
        handled: false,
        message: 'Resource failed to load',
        source: 'https://cdn.test/app.js',
        target: expect.objectContaining({
          tagName: 'SCRIPT',
          source: 'https://cdn.test/app.js',
        }),
      }),
    );
  });

  it('does not duplicate runtime ErrorEvent values already handled by window.onerror', () => {
    const transport = { track: vi.fn() };
    collector = new ErrorCollector(transport);
    collector.start();

    window.dispatchEvent(new ErrorEvent('error', { message: 'boom', error: new Error('boom') }));

    expect(transport.track).not.toHaveBeenCalled();
  });

  it('tracks unhandled promise rejections', () => {
    const transport = { track: vi.fn() };
    collector = new ErrorCollector(transport);
    collector.start();
    const event = new Event('unhandledrejection') as Event & { reason: unknown };
    event.reason = new Error('async boom');

    window.dispatchEvent(event);

    expect(transport.track).toHaveBeenCalledWith(
      '$exception',
      expect.objectContaining({
        mechanism: 'unhandledrejection',
        handled: false,
        message: 'async boom',
        error: expect.objectContaining({
          name: 'Error',
          message: 'async boom',
          stack: expect.any(String),
        }),
      }),
    );
  });
});
