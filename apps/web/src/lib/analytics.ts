'use client';

import { analytics } from '@mcollector/sdk';

const API_KEY = process.env.NEXT_PUBLIC_ANALYTICS_API_KEY || 'demo-key';
const API_HOST = process.env.NEXT_PUBLIC_ANALYTICS_API_HOST || 'http://35.228.4.134:5001/api/v1/ingest';

export const initializeAnalytics = () => {
  analytics.init(API_KEY, {
    apiHost: API_HOST,
    debug: true,
    autoTrackPages: true,
  });
};

export { analytics };
