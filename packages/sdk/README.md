# @mcollector/sdk

JavaScript / TypeScript analytics SDK for sending events to MCollector

## Install

```bash
pnpm add @mcollector/sdk
```

## Quick Start

```ts
import { analytics, logger } from "@mcollector/sdk";

// Initialize once at app startup
analytics.init("your-api-key", {
  apiHost: "http://35.228.4.134:5001/api/v1/ingest",
  debug: true,
});

// Track events
analytics.track("button_clicked", { label: "Create Project" });

// Identify a user
analytics.identify("user_123", {
  email: "user@example.com",
  plan: "pro",
});

// Structured logging — forwarded as analytics events
logger.info("dashboard_opened", { section: "Projects" });
logger.warn("rate_limit_approaching", { remaining: 10 });

try {
  await submitForm();
} catch (err) {
  logger.error(err, { formId: "onboarding" }); // Error is serialized automatically
}

await analytics.flush();
```

## API

### `init(apiKey, options?)`

Initializes the SDK. Calling `init` more than once is ignored after the first successful call

Options:

- `apiHost`: Ingestion host used for event delivery
  Default: `http://35.228.4.134:5001/api/v1/ingest`
- `debug`: Logs queueing and flush activity to the console
- `autoTrackPages`: Enables automatic page tracking. Default: `true`
- `batchSize`: Number of events to queue before flushing. Default: `10`
- `flushInterval`: Time in milliseconds before a queued batch is flushed. Default: `3000`
- `cookieDomain`: Cookie domain used for identity/session storage
- `sessionTimeoutConfig`: Session timeout in milliseconds. Default: `30 * 60 * 1000`

### `track(eventName, properties?)`

Sends a custom event with arbitrary properties

### `identify(userId, traits?)`

Associates events with a known user and stores the user ID and traits locally

### `page(name?, category?, properties?)`

Tracks a page view event. The SDK records the current URL, path, search string, and document title when it runs in the browser

### `logger.info|warn|error|debug|trace|fatal(message, metadata?)`

Sends a structured log event through the analytics queue. The event name is the method name, for example `logger.warn(...)` sends a `warn` event. Metadata is merged into event properties alongside `level` and `message`. Passing an `Error` serializes its `name`, `message`, `stack`, and `cause` into the `error` property.

### `reset()`

Clears the stored user ID, traits, and session data

### `flush()`

Forces the current queue to be sent immediately

## Event Shape

Every event includes common context such as:

- `messageId`
- `timestamp`
- `anonymousId`
- `sessionId`
- `context.url`
- `context.referrer`
- `context.userAgent`
- `context.screen`
- `context.library`
- UTM values when present in the URL

## Browser Behavior

- The SDK is designed for browser usage
- `autoTrackPages` listens to `popstate`, `history.pushState`, and `history.replaceState` so SPA route changes are tracked automatically
- Identity, anonymous ID, traits, queue state, and session state are persisted through cookies and localStorage when available
- Events are batched and sent to `${apiHost}/events`

## Example In HTML

```html
<script type="module">
  import { analytics } from "https://cdn.example.com/@mcollector/sdk";

  analytics.init("your-api-key", {
    apiHost: "http://35.228.4.134:5001/api/v1/ingest",
  });

  analytics.track("Landing Page Viewed");
</script>
```

## Notes

- If `init` has not been called, tracking calls are ignored and a warning is written to the console.
