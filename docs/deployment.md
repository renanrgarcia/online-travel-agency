# Deployment — Azure end to end, at zero or near-zero cost

The goal: everything on Azure (to build Azure depth deliberately), on free tiers, with the model layer
as the one place where "free" and "Azure-native" genuinely conflict.

## The target topology

All five pieces below are now actually deployed and live, not aspirational — see
[`infra/README.md`](../infra/README.md) for how, and [`../README.md`](../README.md#try-it-live) for the
current URLs.

| Piece | Service | Tier | Cost |
|---|---|---|---|
| `frontend/` (React SPA) | Azure Static Web Apps | Free | $0 -- no billing meter exists for this SKU at all |
| `backend/src/FlightAi.Api` | Azure App Service | Free (F1) | $0 -- same, confirmed against the live Retail Prices API |
| `backend/src/FlightAi.Booking.Functions` | Azure Functions | Consumption | $0 within monthly grant |
| Durable Functions state | Azure Storage | Pay-as-you-go | **~R$0.15–0.20/day observed, not "cents/month"** -- see below |
| Language model | Gemini (task 17, live) | — | $0 (Gemini free tier), external to Azure billing entirely |

Azure Functions' Consumption plan includes a monthly free grant (1M executions and 400,000 GB-seconds)
that a demo will not come close to exhausting. App Service F1 is genuinely free but comes with cold
starts, no custom domain, and a daily CPU quota — all fine for a portfolio demo, none fine for
production. That's the honest trade: these tiers are for learning and demonstrating, not for serving
real travellers.

**The Storage line item is bigger, and different in kind, than this document originally claimed.**
Durable Functions requires a real Azure Storage account for its state (Azurite emulates this locally,
task 14) — Storage has no free tier at all, unlike everything else in this table. The original estimate
here was "cents per month," reasoning from per-GB/per-transaction pricing as if cost scaled with usage.
Real billing data (Azure Cost Management, queried directly) proved that wrong: cost is a **flat daily
floor** of roughly R$0.15–0.20 (~R$5–6/month), present identically on days with zero real traffic. The
cause isn't stored data or search/booking volume — it's the Azure Storage backend's own control-queue
polling running continuously in the background, by design, as long as the Function App exists and is
running. Two operational dials exist regardless (stop the Function App between uses; tune `host.json`'s
`extensions.durableTask.maxQueuePollingInterval` to back off further while idle) — but the actual fix is
architectural, not a dial: Durable Task's Scheduler backend has no polling loop and, per the Retail Prices
API, no base fee at all. Backend task 24 and infra task 03 carry the real switch; neither operational dial
is implemented, since the Scheduler removes the trade-off entirely rather than choosing a side of it.

A budget with four notification thresholds (25/50/75/100% of a R$20/month cap) is configured on the
subscription as the actual safety net — see `az consumption budget list`. There is no automatic spending
limit on this subscription type (Pay-As-You-Go-style Microsoft Customer Agreement), so this budget is
the only thing standing between an unexpected cost and silence.

## The model layer — where Azure-native and free diverge

**Microsoft Foundry has no perpetual free tier.** It is pay-per-token. The `Azure.AI.Projects` pattern
in `05-agents-and-intent.md` is the right production answer and worth understanding, but it will bill
you from the first call.

**Gemini's free tier is not available through Foundry.** Foundry's catalog carries OpenAI, Anthropic,
Meta, Mistral, DeepSeek, xAI, Cohere and others — Google's Gemini is not among them, and there's no
reason to expect it will be, since Gemini is a competing cloud's flagship model. Choosing Gemini means
your Azure-hosted code calls Google's API directly over the public internet. Everything else stays on
Azure; only the model call leaves.

**GitHub Models is closed to new users.** It used to be the obvious "free and Microsoft-ecosystem"
answer — free prototyping quota, and the same Azure AI Inference SDK shape as Foundry, so graduating to
Foundry was an endpoint-and-key swap. As of 16 June 2026 it is no longer available to new customers,
with Microsoft directing new users to Foundry instead. If you have pre-existing GitHub Models usage on
your account it may still work for you; if not, this door is shut.

### The recommendation

Use **Gemini's free tier** for development and the demo, via the OpenAI-compatible endpoint pattern in
`08-package-versions.md`. The specific model matters more than this section originally implied:
`gemini-2.5-flash` (this project's original choice) has since been retired for new users, and its
direct successor `gemini-3.6-flash` caps the free tier at a hard **20 requests/day per project** —
confirmed live, not assumed, and far short of what earlier revisions of this doc guessed. `gemini-3.5-flash-lite`
is what task 17 actually verified end to end: **15 requests/minute, 500 requests/day**, confirmed via
the project's own AI Studio rate-limit dashboard (`aistudio.google.com/rate-limit`) — 25x the daily
allowance of the newer-numbered model, for the same account. A higher version number is not a proxy for
a better free-tier quota. See `08-package-versions.md` and `09-lessons-learned.md` for the full story;
re-verify both the model string and its quota against that dashboard before relying on either, since
both have already moved at least once since this project started and Google's public docs page no
longer publishes per-model numbers at all. Even 500/day is ample for a demo and nowhere near enough for
production, which is the correct shape for this project.

Keep the Foundry path (`Azure.AI.Projects`) documented and compile-verified as the production swap. The
whole point of the `IChatClient` boundary in task 09 is that this choice stays a configuration change.

**The trade-off, stated plainly:** you get $0 and a working demo, at the cost of one non-Azure
dependency in an otherwise Azure-native system. If Azure purity matters more than cost, use Foundry
with a spending cap and accept a small bill. If you want to compare both, task 17's stretch goal is
exactly that experiment.

Tasks 13, 16, and 17 each carry a **Deployment gate** section with the specific acceptance criteria for
that step. If this is your first real Azure deployment — not just running services locally — say so
when you reach one and ask for a guided walkthrough rather than working from the gate's table alone;
the table is the acceptance bar for "done," not instructions for how to get there.

## Deployment order

This was written as a forward-looking plan; every step below is now actually complete and live, kept
here as the record of the order it happened in rather than a to-do list:

1. **Task 13** — `FlightAi.Api` deployed to App Service F1; the SSE stream confirmed surviving the real
   network path (proxies and buffering layers break streaming in ways localhost never will — this was
   in fact the deployment step that surprised us, per `09-lessons-learned.md`).
2. **Backend task 19 + infra task 02** — `frontend/` deployed to Static Web Apps, pointed at the App
   Service API. CORS (backend 19) was the hard prerequisite here, confirmed via a real cross-origin
   request from the deployed frontend, not just a local test.
3. **Backend task 16, with infra task 01** — `FlightAi.Booking.Functions` deployed to a Consumption
   plan Function App with a real Storage account, provisioned by Bicep rather than by hand.
4. **Task 17** — the Gemini API key moved into App Service configuration via Bicep (`@secure()`
   parameter, sourced from an environment variable at deploy time, never committed) — see
   [`infra/README.md`](../infra/README.md)'s Secrets section.
5. **Tasks 20 and 21** — rate limiting and server-authoritative prices, both live before the URL was
   shared anywhere.

## Infrastructure as code and CI/CD

Both now fully cover all three deployable pieces, not just the App Service this section originally
described:

- **[`infra/`](../infra/README.md)** — Bicep, subscription-scoped, covering the App Service, the
  Booking Functions app (Consumption plan + its Storage account), and the Static Web App. Chosen over
  Terraform for a single-cloud project with one operator: no state file to provision and protect,
  first-class `az` tooling, and it's what AZ-104 expects you to author today.
- **[`.github/workflows/ci-cd.yml`](../.github/workflows/ci-cd.yml)** — build and test on every push to
  `main` or `develop` and on PRs into `main`; on a push to `main`, three deploy jobs run in parallel
  (API, Functions, frontend), the frontend's build-time URLs sourced from repo variables rather than
  hardcoded.

Provisioning itself (`az deployment sub create`) is **not** part of the CI/CD pipeline — it's a
deliberately manual step, run against the subscription directly when infra changes, documented in
`infra/README.md`. What *is* automated is deploying application code to whatever infra already exists.

Still deliberately absent: staging environments, deployment slots (App Service F1 doesn't support them),
custom domains, CDN tuning, and Application Insights (a real cost trade-off explicitly declined — see
`infra/README.md`'s notes on `app-service.bicep`'s filesystem logging choice).

## Open gaps, as of the last infra-lane pass

Everything the original version of this section listed (backend 19/20/21, infra 01/02) is done. What's
actually still open:

- **`main` lags `develop`.** Everything above is proven on `develop`'s tip; whether it's *live* depends
  on how recently `develop` was merged into `main` and redeployed — check `git log --oneline
  origin/main..origin/develop` before assuming the live site reflects the latest work. Merging that gap
  is a deliberate release decision, not something infra changes should trigger as a side effect.
- **The Storage cost floor** described above — task-carded (backend 24, infra 03), not yet implemented.
- **Booking-side base URL wiring is fragile by construction**: the frontend's `VITE_BOOKING_API_BASE_URL`
  build-time variable has to name-match `frontend/src/config.ts`'s own read exactly, and nothing enforces
  that at build time — a naming drift here silently breaks every deployed booking call while search keeps
  working, which is exactly the failure mode a live infra-lane validation pass caught once already.
