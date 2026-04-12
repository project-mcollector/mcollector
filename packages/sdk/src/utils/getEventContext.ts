import { EventContext } from "../types/EventContext";
import pkg from '../../package.json';


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
      version: pkg.version, 
    },
    ...(utm && { utm })
  };
}