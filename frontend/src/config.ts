/**
 * The API's origin. Empty means same-origin — correct only when the frontend and API are served from
 * the same host, which they never are once deployed (Static Web Apps vs. App Service — separate
 * origins by construction, see docs/deployment.md). Read from Vite's build-time env, never hardcoded,
 * per F03's locked decision.
 */
export function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL ?? ''
}
