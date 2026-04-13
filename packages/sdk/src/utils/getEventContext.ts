/** Helper function to extract browser and environment context (URL, user agent, UTMs) for events. */
import { EventContext } from "../types/EventContext";

declare const process: any;

export function getEventContext(): EventContext {
  const searchParams = new URLSearchParams(window.location.search);
  const utmSource = searchParams.get('utm_source');
  const utmMedium = searchParams.get('utm_medium');
  const utmCampaign = searchParams.get('utm_campaign');

  const utm = (utmSource || utmMedium || utmCampaign) ? {
    ...(utmSource && { source: utmSource }),
    ...(utmMedium && { medium: utmMedium }),
    ...(utmCampaign && { campaign: utmCampaign }),
  } : undefined;

  return {
    url: window.location.href,
    referrer: document.referrer,
    userAgent: navigator.userAgent,
    screen: {
      width: window.screen.width,
      height: window.screen.height,
    },
    library: {
      name: "mcollector-js",
      version: typeof process !== 'undefined' && process.env.__SDK_VERSION__ ? process.env.__SDK_VERSION__ : 'unknown', 
    },
    ...(utm && { utm })
  };
}