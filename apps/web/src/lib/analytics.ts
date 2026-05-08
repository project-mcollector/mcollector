'use client';

import { analytics } from '@mcollector/sdk';

const API_KEY = process.env.NEXT_PUBLIC_ANALYTICS_API_KEY || 'demo-key';
const API_HOST =
  process.env.NEXT_PUBLIC_ANALYTICS_API_HOST ||
  process.env.NEXT_PUBLIC_INGESTION_URL ||
  '';
const PROJECT_ID = process.env.NEXT_PUBLIC_ANALYTICS_PROJECT_ID || '';

export const initializeAnalytics = () => {
  if (!API_HOST || !PROJECT_ID || API_KEY === 'demo-key') {
    console.warn(
      '[mcollector] Analytics env is incomplete. Check NEXT_PUBLIC_ANALYTICS_API_KEY, NEXT_PUBLIC_ANALYTICS_PROJECT_ID, NEXT_PUBLIC_ANALYTICS_API_HOST.',
    );
    return;
  }

  analytics.init(API_KEY, {
    projectId: PROJECT_ID,
    apiHost: API_HOST,
    debug: true,
    autoTrackPages: true,
  });
};

export { analytics };
