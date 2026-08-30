# 02 — Static Web Apps deployment

**Originally:** frontend task F08
**Roadmap step:** 2. Static Web Apps deployment
**Source doc:** `docs/deployment.md`, `infra/README.md`
**Depends on:** Frontend task 03 (at minimum), backend task 19 (CORS) -- not on infra task 01

## Goal

Deploy the frontend to Azure Static Web Apps, under the same Bicep and CI/CD treatment the backend
already has — provisioned from source, deployed on push, no portal clicking.

## Scope

- `infra/modules/static-web-app.bicep`, wired into `main.bicep`.
- A deploy job in `.github/workflows/ci-cd.yml`: build the frontend, publish to SWA on push to `main`.
- The API base URL supplied as build configuration, pointing at the deployed App Service.

## Out of scope

- Custom domains — Free tier gives `*.azurestaticapps.net` with HTTPS, which is enough.
- SWA's managed Functions API. The backend is a separate App Service and Function App by design; using
  SWA's bundled API would be a third hosting model for no gain.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `az bicep build` on the new module and `main.bicep` | Clean | Same pre-flight discipline as task 01 (this folder) E1 |
| E2 | `az deployment sub what-if` | Shows the Static Web App created and nothing else disturbed | The check that asks Azure rather than guessing |
| E3 | Push to `main` | Frontend builds, type-checks, and publishes; the backend's job still runs | One pipeline for the whole system |
| E4 | The deployed site | Loads over HTTPS, no mixed-content or CSP errors in the console | The first thing a reviewer will look at |
| E5 | A search from the deployed site against the deployed API | All four stages render, arriving progressively | The real proof. Streaming can survive localhost and die at a proxy — backend task 13 D2 flags this, and this is the browser-side half of it |
| E6 | The API base URL | Comes from build configuration, not a committed constant | The API's hostname isn't known until it's deployed |
| E7 | The published bundle, grepped | No API key, no signing key, no secret of any kind | Everything shipped here is public by definition. Backend task 17 D1 and task 21 both depend on this staying true |
| E8 | A booking from the deployed site | Completes end to end against the deployed Function App | Two cross-origin backends, both reachable — the topology working as designed |

### Locked decisions

- **Free tier**, per `docs/deployment.md`'s topology.
- **The frontend calls the App Service directly**, rather than proxying through SWA. One less layer
  between an SSE stream and the browser — and proxies are exactly what breaks streaming.

## Deployment gate

See [`../../../deployment.md`](../../../deployment.md), step 2.

| ID | Requirement |
|---|---|
| D1 | `frontend/` deployed to Azure Static Web Apps (Free tier), provisioned by Bicep |
| D2 | The deployed frontend talks to the deployed API cross-origin, with progressive rendering intact (E5) |
| D3 | A booking completes end to end from the deployed site (E8) |

If this is your first Static Web Apps deployment, ask for a guided walkthrough — the deployment token
and the build configuration are the two places this commonly goes wrong.

## Done when

All eight evals and all three deployment gates pass.
