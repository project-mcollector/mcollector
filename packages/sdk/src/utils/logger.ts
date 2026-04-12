class Logger {
  private isDebugEnabled: boolean = false;

  public setDebug(enabled: boolean): void {
    this.isDebugEnabled = enabled;
  }

  public log(message: string, ...args: any[]): void {
    if (this.isDebugEnabled) {
      console.log(`[mcollector] ${message}`, ...args);
    }
  }

  public warn(message: string, ...args: any[]): void {
    if (this.isDebugEnabled) {
      console.warn(`[mcollector] ${message}`, ...args);
    }
  }

  public error(message: string, ...args: any[]): void {
    if (this.isDebugEnabled) {
      console.error(`[mcollector] ${message}`, ...args);
    }
  }
}

export const logger = new Logger();
