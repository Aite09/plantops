import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';
import { API_BASE_URL } from './app/core/api-config';
import { APP_CONFIG, loadAppConfig } from './app/core/app-config';

// Read /config.json first, then start Angular with those values provided for injection.
loadAppConfig()
  .then((config) =>
    bootstrapApplication(App, {
      providers: [
        ...appConfig.providers,
        { provide: APP_CONFIG, useValue: config },
        { provide: API_BASE_URL, useValue: config.apiBaseUrl },
      ],
    }),
  )
  .catch((error: unknown) => {
    console.error(error);
    document.body.textContent = 'PlantOps could not start because its configuration is missing. Please try again later.';
  });
