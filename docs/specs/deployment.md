# Deployment — Azure end to end, at zero or near-zero cost

The goal: everything on Azure (to build Azure depth deliberately), on free tiers, with the model layer
as the one place where "free" and "Azure-native" genuinely conflict.

## The target topology

| Piece | Service | Tier | Cost |
|---|---|---|---|
| `frontend/` (React SPA) | Azure Static Web Apps | Free | $0 |
| `backend/src/FlightAi.Api` | Azure App Service | Free (F1) | $0 |
| `backend/src/FlightAi.Booking.Functions` | Azure Functions | Consumption | $0 within monthly grant |
| Durable Functions state | Azure Storage | Pay-as-you-go | Cents/month at demo volume |
| Language model | See below | — | $0 (Gemini free tier) |

Azure Functions' Consumption plan includes a monthly free grant (1M executions and 400,000 GB-seconds)
that a demo will not come close to exhausting. App Service F1 is genuinely free but comes with cold
starts, no custom domain, and a daily CPU quota — all fine for a portfolio demo, none fine for
production. That's the honest trade: these tiers are for learning and demonstrating, not for serving
real travellers.

Durable Functions requires a real Azure Storage account for its state (this is what Azurite emulates
locally, see task 14). Storage is not free, but at demo volume it costs cents per month rather than
dollars. It is the one unavoidable line item.

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

Use **Gemini's free tier** (2.5 Flash) for development and the demo, via the OpenAI-compatible endpoint
pattern in `08-package-versions.md`. Free-tier limits as of 2026 are roughly 10 requests/minute and
250–1,500 requests/day depending on model and current policy — verify against Google's own docs before
relying on a specific number, since these have moved more than once this year. That's ample for a demo
and nowhere near enough for production, which is the correct shape for this project.

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

Deploy in the same order you build. Don't try to deploy anything before task 13 — there's nothing
user-facing to deploy until the API streams real results.

1. **After task 13** — deploy `FlightAi.Api` to App Service F1, confirm the SSE stream survives a real
   network path (proxies and buffering layers break streaming in ways localhost never will; this is
   the deployment step most likely to surprise you).
2. **Alongside the frontend** — deploy `frontend/` to Static Web Apps, pointed at the App Service API.
3. **After task 16** — deploy `FlightAi.Booking.Functions` to a Consumption plan Function App with a
   real Storage account.
4. **After task 17** — move the model API key into App Service configuration / Key Vault. Never commit
   it; never ship it to the browser. The model is called from the backend only.

## What this deliberately isn't

No infrastructure-as-code (Bicep/Terraform), no CI/CD pipeline, no staging environment, no custom
domain, no CDN tuning. Those are all worth learning, and none of them belong in the first pass — add
them once something is actually deployed and working, if you want a follow-on exercise.
