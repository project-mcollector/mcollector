/** Public structured logger that forwards log calls as analytics events. */
import { Analytics, analytics } from './Analytics';

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';
export type LogMetadata = Record<string, unknown>;
export type LogMessage = string | Error;

type LogTransport = Pick<Analytics, 'track'>;

export class Logger {
  private transport: LogTransport;

  constructor(transport: LogTransport = analytics) {
    this.transport = transport;
  }

  public trace(message: LogMessage, metadata?: LogMetadata): void {
    this.capture('trace', message, metadata);
  }

  public debug(message: LogMessage, metadata?: LogMetadata): void {
    this.capture('debug', message, metadata);
  }

  public info(message: LogMessage, metadata?: LogMetadata): void {
    this.capture('info', message, metadata);
  }

  public warn(message: LogMessage, metadata?: LogMetadata): void {
    this.capture('warn', message, metadata);
  }

  public error(message: LogMessage, metadata?: LogMetadata): void {
    this.capture('error', message, metadata);
  }

  public fatal(message: LogMessage, metadata?: LogMetadata): void {
    this.capture('fatal', message, metadata);
  }

  public log(level: LogLevel, message: LogMessage, metadata?: LogMetadata): void {
    this.capture(level, message, metadata);
  }

  private capture(level: LogLevel, message: LogMessage, metadata: LogMetadata = {}): void {
    const error = message instanceof Error ? this.serializeError(message) : undefined;
    const text = message instanceof Error ? message.message : message;

    this.transport.track(level, {
      ...this.normalizeMetadata(metadata),
      level,
      message: text,
      ...(error ? { error } : {}),
    });
  }

  private normalizeMetadata(metadata: LogMetadata): LogMetadata {
    if (metadata && typeof metadata === 'object' && !Array.isArray(metadata)) {
      return metadata;
    }

    return { metadata };
  }

  private serializeError(error: Error): LogMetadata {
    return {
      name: error.name,
      message: error.message,
      stack: error.stack,
      cause: this.serializeCause(error),
    };
  }

  private serializeCause(error: Error): unknown {
    if (!('cause' in error) || error.cause === undefined) {
      return undefined;
    }

    if (error.cause instanceof Error) {
      return {
        name: error.cause.name,
        message: error.cause.message,
        stack: error.cause.stack,
      };
    }

    return error.cause;
  }
}

export const logger = new Logger();
