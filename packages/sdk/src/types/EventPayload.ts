import {EventContext} from "./EventContext";

export interface EventPayload {
  messageId: string;
  timestamp: string;
  event: string;
  properties: Record<string, any>;
  context: EventContext;
  anonymousId: string;
  userId?: string;
  sessionId: string;
}
