type ErrorTransport = {
  track(eventName: string, properties?: Record<string, any>): void;
};

type WindowOnError = OnErrorEventHandler;

export class ErrorCollector {
  private transport: ErrorTransport;
  private previousOnError: WindowOnError = null;
  private active = false;

  constructor(transport: ErrorTransport) {
    this.transport = transport;
  }

  public start(): void {
    if (this.active || typeof window === 'undefined') return;

    this.active = true;
    this.previousOnError = window.onerror;

    window.onerror = (message, source, lineno, colno, error) => {
      this.capture('window.onerror', {
        message: this.stringifyReason(message),
        source,
        lineno,
        colno,
        error: error instanceof Error ? this.serializeError(error) : undefined,
      });

      if (typeof this.previousOnError === 'function') {
        return this.previousOnError.call(window, message, source, lineno, colno, error);
      }

      return false;
    };

    window.addEventListener('error', this.handleErrorEvent, true);
    window.addEventListener('unhandledrejection', this.handleUnhandledRejection);
  }

  public stop(): void {
    if (!this.active || typeof window === 'undefined') return;

    window.onerror = this.previousOnError;
    window.removeEventListener('error', this.handleErrorEvent, true);
    window.removeEventListener('unhandledrejection', this.handleUnhandledRejection);
    this.active = false;
  }

  private handleErrorEvent = (event: ErrorEvent | Event): void => {
    if (event instanceof ErrorEvent && event.error) {
      return;
    }

    const target = event.target;

    this.capture('error_event', {
      message: event instanceof ErrorEvent ? event.message : 'Resource failed to load',
      source: event instanceof ErrorEvent ? event.filename : this.getTargetSource(target),
      lineno: event instanceof ErrorEvent ? event.lineno : undefined,
      colno: event instanceof ErrorEvent ? event.colno : undefined,
      target: this.serializeTarget(target),
      error: event instanceof ErrorEvent && event.error instanceof Error
        ? this.serializeError(event.error)
        : undefined,
    });
  };

  private handleUnhandledRejection = (event: PromiseRejectionEvent): void => {
    const reason = event.reason;

    this.capture('unhandledrejection', {
      message: reason instanceof Error ? reason.message : this.stringifyReason(reason),
      reason: reason instanceof Error ? undefined : this.serializeUnknown(reason),
      error: reason instanceof Error ? this.serializeError(reason) : undefined,
    });
  };

  private capture(mechanism: string, properties: Record<string, any>): void {
    this.transport.track('$exception', {
      mechanism,
      handled: false,
      ...this.currentPage(),
      ...properties,
    });
  }

  private currentPage(): Record<string, string> {
    return {
      url: typeof window !== 'undefined' ? window.location.href : '',
      path: typeof window !== 'undefined' ? window.location.pathname : '',
      title: typeof document !== 'undefined' ? document.title : '',
    };
  }

  private serializeError(error: Error): Record<string, unknown> {
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

  private stringifyReason(reason: unknown): string {
    if (typeof reason === 'string') return reason;
    if (reason instanceof Error) return reason.message;

    try {
      return JSON.stringify(reason);
    } catch {
      return String(reason);
    }
  }

  private serializeUnknown(value: unknown): unknown {
    if (value === null || ['string', 'number', 'boolean'].includes(typeof value)) {
      return value;
    }

    try {
      return JSON.parse(JSON.stringify(value));
    } catch {
      return String(value);
    }
  }

  private serializeTarget(target: EventTarget | null): Record<string, string | undefined> | undefined {
    if (!(target instanceof Element)) return undefined;

    return {
      tagName: target.tagName,
      id: target.id || undefined,
      className: typeof target.className === 'string' ? target.className || undefined : undefined,
      source: this.getTargetSource(target),
    };
  }

  private getTargetSource(target: EventTarget | null): string | undefined {
    if (target instanceof HTMLScriptElement || target instanceof HTMLImageElement) {
      return target.src || undefined;
    }

    if (target instanceof HTMLLinkElement) {
      return target.href || undefined;
    }

    return undefined;
  }
}
