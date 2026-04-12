/** Type definitions for the contextual data attached to every event (URL, user agent, screen, UTMs). */
export interface EventContext {
  url: string;
  referrer: string;
  userAgent: string;
  screen: {
    width: number;
    height: number;
  };
  library: {
    name: string;
    version: string;
  };
  utm?: {
    source?: string;
    medium?: string;
    campaign?: string;
  };
}
