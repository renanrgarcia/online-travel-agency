/**
 * The search API's origin. Empty means same-origin — correct only when the frontend and API are
 * served from the same host, which they never are once deployed (Static Web Apps vs. App Service —
 * separate origins by construction, see docs/deployment.md). Read from Vite's build-time env, never
 * hardcoded, per F03's locked decision.
 */
export function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL ?? ''
}

/**
 * The booking saga's origin — a genuinely different Azure resource from the search API (Azure
 * Functions vs. App Service, per docs/deployment.md), so it needs its own base URL rather than
 * reusing {@link getApiBaseUrl}. Empty (same-origin) by default, same as the search API — local dev
 * sets it explicitly in `.env.development` to Azure Functions Core Tools' own default port, matching
 * the curl examples in docs/reference/07-booking-saga.md.
 */
export function getBookingApiBaseUrl(): string {
  return import.meta.env.VITE_BOOKING_API_BASE_URL ?? ''
}

/**
 * True for a local dev server (`vite dev`, and Vitest by extension), false for a `vite build`
 * production bundle regardless of where it's deployed. Gates the "show raw model output" debug
 * disclosure (F06 E8 follow-up): closed-by-default wasn't enough on its own — a production user
 * clicking it still exposed the model's token vocabulary for no benefit to them, so the affordance
 * itself is now absent outside a dev/debug build, not just collapsed.
 */
export function isDebugBuild(): boolean {
  return import.meta.env.DEV
}
