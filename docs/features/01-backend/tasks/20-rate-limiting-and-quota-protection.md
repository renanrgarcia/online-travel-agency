# 20 — Rate limiting and quota protection

**Roadmap step:** 8. Safe to expose
**Source doc:** `docs/deployment.md`, `docs/reference/03-suppliers-and-budget.md`
**Depends on:** 13
**Build before:** 17 goes public — a real model key behind an unprotected endpoint is a metered
resource anyone can spend.

## Goal

Bound how often the search endpoint can be called, so a crawler or a loop can't exhaust the model
quota, the supplier budgets, or App Service F1's daily CPU allowance.

`LookToBookBudget` (task 07) already protects *suppliers* from being over-called. Nothing protects the
endpoint itself, the model, or the host. Once deployed, the URL is public: `docs/deployment.md` sizes
the model layer at Gemini's free tier — roughly 10 requests/minute — and App Service F1 at 60 CPU-minutes
per day. Both are trivially exhaustible by traffic nobody intended.

## Scope

- ASP.NET Core's built-in rate limiting middleware on the search endpoint.
- Limits from configuration, not constants.
- `429` with `Retry-After` when the limit binds.

## Out of scope

- Authentication, API keys, or per-user quotas — there are no users. This is blunt protection against
  volume, not an entitlement system.
- Distributed/shared limiter state. One F1 instance means in-memory is sufficient and honest; a
  multi-instance deployment would need revisiting, and isn't in this topology.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | N requests inside the window, where N is the configured limit | All succeed | The limit doesn't bite normal use |
| E2 | The N+1th request inside the same window | `429`, with a `Retry-After` header | A caller that can't tell *when* to retry will just hammer |
| E3 | After the window elapses | Requests succeed again | It's a rate, not a permanent block — the same shape as task 07's budget |
| E4 | A request rejected by the limiter | No intent parse, no supplier call, no model call happens | The whole point: the limiter must run *before* the expensive work, or it protects nothing |
| E5 | A stream already in flight when the limit binds for new requests | The in-flight stream completes normally | Rate limiting is about admission, not killing live connections |
| E6 | Limit changed in configuration, app restarted | New limit applies with no rebuild | Tuning this against real traffic shouldn't need a deploy |
| E7 | Two different clients, limit of N each | Each gets its own allowance | A single global bucket means one noisy caller silently denies everyone |

### Locked decisions

- **Fixed window, not token bucket or sliding window.** Simplest thing that bounds the cost; the
  precision of smarter algorithms buys nothing at this traffic level.
- **Partition by client IP**, read from `X-Forwarded-For` when present — App Service sits behind a
  proxy, so the socket's remote address is the proxy, not the caller. Getting this wrong collapses
  every client into one partition and makes E7 silently false.
- **The limiter is applied to the search endpoint specifically**, not globally — a health check or a
  static file shouldn't consume a search allowance.

## Done when

All seven evals pass. E4 is the one with money attached: a limiter that rejects *after* the model call
has already been made costs exactly as much as no limiter at all.
