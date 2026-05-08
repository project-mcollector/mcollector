/** Type definitions for the SDK initialization options. */
export interface InitOptions {
  projectId?: string;
  apiHost?: string;
  debug?: boolean;
  autoTrackPages?: boolean;
  batchSize?: number;
  flushInterval?: number;
  cookieDomain?: string;
  sessionTimeoutConfig?: number;
}
