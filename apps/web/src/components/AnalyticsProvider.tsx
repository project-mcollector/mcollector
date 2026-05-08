'use client';

import { useEffect } from 'react';
import { initializeAnalytics } from '@/lib/analytics';

export function AnalyticsProvider() {
  useEffect(() => {
    initializeAnalytics();
  }, []);

  return null;
}
