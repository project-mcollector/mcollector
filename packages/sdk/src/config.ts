/** Default configuration options for the Analytics SDK. */
import { InitOptions } from './types/Options';

export const defaultOptions: Required<Omit<InitOptions, 'projectId'>> = {
  apiHost: 'https://api.yourdomain.com/ingest', 
  debug: false,
  autoTrackPages: true,
  autoTrackErrors: true,
  batchSize: 10,          
  flushInterval: 3000,     
  cookieDomain: '',
  sessionTimeoutConfig: 30 * 60 * 1000,        
};
