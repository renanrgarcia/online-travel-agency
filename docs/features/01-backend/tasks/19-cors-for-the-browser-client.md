# 19 — CORS for the browser client

**Roadmap step:** 8. Safe to expose
**Source doc:** `docs/deployment.md`, `docs/reference/06-api-sse-contract.md`
**Depends on:** 13
**Build before:** feature 02, task 03 — the frontend cannot reach the API without this.

## Goal

Let a browser on a different origin call the search API. `FlightAi.Api` has no CORS configuration at
all today, which is invisible while `curl` is the only client and a hard failure the moment a real
frontend exists.

The deployed topology puts the frontend on Azure Static Web Apps and the API on App Service — two
different origins by construction (`docs/deployment.md`). Every browser call between them is
cross-origin, and an `EventSource` with no `Access-Control-Allow-Origin` on the response fails without
ever surfacing a useful error to the page.

## Scope

- CORS policy on `FlightAi.Api`, with allowed origins read from configuration.
- Verified against the SSE endpoint specifically, not just a plain JSON route.
- A note in `docs/deployment.md` that the Function App (tasks 14–16) needs its own CORS, configured in
  Azure rather than in code — a separate host is a separate origin.

## Out of scope

- Authentication of any kind. CORS is not authorization; it tells a browser which origins may *read* a
  response, and nothing more. Any real access control is a separate concern this task does not pretend
  to address.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `GET /api/search/stream` with an `Origin` header matching a configured origin | Response carries `Access-Control-Allow-Origin` for that origin, and the SSE stream still streams per task 13 E2 | The actual thing the frontend does. CORS middleware that buffers or breaks streaming would pass a naive test and fail in practice |
| E2 | Same request with an `Origin` not in configuration | No `Access-Control-Allow-Origin` in the response | The policy is a policy, not decoration |
| E3 | Preflight `OPTIONS` against the endpoint | Correct status and `Access-Control-Allow-*` headers | Some clients preflight even simple requests |
| E4 | Allowed origins changed in configuration, app restarted, no rebuild | New origin works, old one doesn't | The deployed frontend's URL isn't known at build time, so this cannot be a compile-time constant |
| E5 | Response headers on an allowed cross-origin request | Exactly one `Access-Control-Allow-Origin`, exactly one `Content-Type` | `docs/reference/09-lessons-learned.md` documents a real duplicate-header bug on this endpoint; CORS middleware is a new way to reintroduce it |
| E6 | Configuration with no origins set | The app starts and serves same-origin requests normally | A missing setting shouldn't take the API down |

### Locked decisions

- **Named policy with explicit origins, never `AllowAnyOrigin`.** A wildcard would work for the demo
  and is the wrong habit to build; it also cannot be combined with credentials if that's ever needed.
- **No credentials.** No cookies or auth headers cross this boundary, so the policy doesn't enable
  them.

## Done when

All six evals pass, and a browser page served from a different port on localhost can open an
`EventSource` against the API and receive all four event types.
