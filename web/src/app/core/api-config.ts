import { InjectionToken } from '@angular/core';

/** Base URL of the PlantOps API, e.g. http://localhost:5297. Provided from /config.json at start-up (see app-config.ts). */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
