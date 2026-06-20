import { describe, expect, it, vi } from 'vitest';
import { Logger } from '../Logger';

describe('Logger', () => {
  it('tracks log methods as events named after the method', () => {
    const transport = { track: vi.fn() };
    const logger = new Logger(transport);

    logger.info('project opened', { projectId: 'project-1' });

    expect(transport.track).toHaveBeenCalledWith('info', {
      projectId: 'project-1',
      level: 'info',
      message: 'project opened',
    });
  });

  it('serializes Error values when logging errors', () => {
    const transport = { track: vi.fn() };
    const logger = new Logger(transport);
    const error = new Error('request failed');

    logger.error(error, { requestId: 'request-1' });

    expect(transport.track).toHaveBeenCalledWith(
      'error',
      expect.objectContaining({
        requestId: 'request-1',
        level: 'error',
        message: 'request failed',
        error: expect.objectContaining({
          name: 'Error',
          message: 'request failed',
          stack: expect.any(String),
        }),
      }),
    );
  });
});
