# 04 — Duffel API key

**Roadmap step:** 4. Duffel API key
**Source doc:** `infra/README.md`, `docs/reference/12-supplier-api-options.md`
**Depends on:** Infra task nothing new structurally — this repeats the exact `geminiApiKey` pattern
already in `infra/modules/app-service.bicep` (task 17's infra counterpart) for a second secret. Backend
task 25's deployment gate depends on this one being live, not the other way around — the resource-level
wiring here doesn't need task 25's code to exist first, same as every other infra task in this feature.

## Goal

Wire a Duffel test-mode API token into `FlightAi.Api`'s deployed configuration, exactly the way the
Gemini key already is — so backend task 25's `DuffelConnector` has somewhere real to read its
credential from once it's deployed.

## Scope

- `infra/modules/app-service.bicep`: add `@secure() param duffelApiKey string = ''`, and append
  `{ name: 'Duffel__ApiKey', value: duffelApiKey }` to `appSettings`. Empty default, same reasoning as
  `geminiApiKey`: backend task 25 already treats a missing key as "don't register `DuffelConnector`," not
  a startup failure — this is not the same shape as `priceAssertionSigningKey`, which has no default and
  intentionally fails startup without one.
- `infra/main.bicep`: the same `@secure()` param, threaded into the `appService` module call only — not
  `functionsApp`, since only `FlightAi.Api` ever calls a supplier connector.
- `infra/main.bicepparam`: `param duffelApiKey = readEnvironmentVariable('DUFFEL_API_KEY', '')`, matching
  `geminiApiKey`'s pattern exactly (the empty-string default, not `priceAssertionSigningKey`'s
  no-default-at-all one).
- `infra/README.md`: a short addition alongside the existing Gemini key documentation, same pattern —
  set `DUFFEL_API_KEY` in the shell before deploying if you want the real connector live.

## Out of scope

- Key Vault. Per this project's existing locked decision (backend task 17, infra task 01) at this scale,
  App Service Configuration app settings are sufficient — no reason to treat this secret differently.
- Anything in `functions.bicep` — the Booking Functions app never calls Duffel (backend task 25 is
  search-only, no booking integration).
- Backend task 25's own code. This task provisions the configuration slot; task 25 is what actually
  reads `Duffel:ApiKey` and does something with it.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `az bicep build` on `app-service.bicep` and `main.bicep` | Clean, no warnings | Same pre-flight discipline as every infra task here |
| E2 | `az deployment sub what-if` with no `DUFFEL_API_KEY` set | Shows the new app setting created with an empty value; nothing else disturbed | Confirms the empty-default path deploys cleanly — this must work with no key configured, same as local dev |
| E3 | `az deployment sub what-if` with `DUFFEL_API_KEY` set to a real test-mode token | Shows the app setting's value changing; nothing else disturbed | The real path — a token actually reaches the deployed app |
| E4 | `az deployment sub create` with the token set | The Web App's application settings show `Duffel__ApiKey` populated, confirmed via `az webapp config appsettings list`, not just inferred from the template | Matches this project's existing standard of confirming a secret actually landed, not just that the deployment succeeded |
| E5 | The repository, grepped | No Duffel token — test or otherwise — committed anywhere, including in `main.bicepparam` | The same rule already enforced for `priceAssertionSigningKey` and `geminiApiKey` |

### Locked decisions

- **Empty-string default, like `geminiApiKey` — not no-default, like `priceAssertionSigningKey`.**
  Whether a real supplier is wired in is optional infrastructure state, not a correctness requirement the
  app should refuse to start without.

## Done when

All five evals pass, and `infra/README.md` documents this key the same way it documents the Gemini one.
