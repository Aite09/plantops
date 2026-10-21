import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** Optional features switched on in the API. The API decides; the web app only hides what would not work. */
export interface Features {
  explainFault: boolean;
}

@Injectable({ providedIn: 'root' })
export class FeaturesApi {
  // Asked once per session: switching a feature on or off means restarting the API anyway. A failed request is not
  // kept (shareReplay drops errors), so the next page that asks tries again.
  readonly #features$ = inject(HttpClient)
    .get<Features>(`${inject(API_BASE_URL)}/api/features`)
    .pipe(shareReplay(1));

  get(): Observable<Features> {
    return this.#features$;
  }
}
