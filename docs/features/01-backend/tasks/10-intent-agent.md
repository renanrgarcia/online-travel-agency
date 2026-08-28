# 10 — Intent agent

**Roadmap step:** 5. AI layer, offline first
**Source doc:** `docs/reference/05-agents-and-intent.md`
**Depends on:** 09 (offline chat client), 04 (`SearchRequest` shape)

## Goal

Build `IntentAgentFactory`: natural language in, a typed, schema-validated `SearchRequest` out. The
first AI touchpoint, and the boundary past which nothing reads free text again.

## Scope

- `SearchRequest` covering what tasks 04–08 consume, plus a `Language` field.
- `Language` is inferred from the input query itself (the agent already reads the whole query to
  extract everything else) — never asked for as a separate input. Task 11 depends on it to answer in
  the same language the traveller asked in; see that task's evals.
- `IntentAgentFactory.Create(IChatClient)` using the framework's typed call (`RunAsync<T>`, per
  `docs/reference/08-package-versions.md`).
- Run against `OfflineChatClient`.

## Out of scope

- Real models — task 17. The explanation agent — task 11.

## Evals

| ID | Input | Expected | Why it matters |
|---|---|---|---|
| E1 | `"cheapest flight from São Paulo to Lisbon on 12 March for 2 people"` | `SearchRequest` with origin, destination, date and passenger count populated correctly | Baseline extraction |
| E2 | The same input twice | Identical `SearchRequest` | Determinism through the agent layer |
| E3 | Input missing a destination | Rejected as invalid — **not** a `SearchRequest` with a null or guessed destination | A half-filled request flowing downstream would produce a confidently wrong search |
| E4 | Model returns malformed/unparseable output | Surfaced as a failure, not an exception escaping to the caller | Real models return junk sometimes; this is expected, not exceptional |
| E5 | Input with a past date | Rejected by validation | Schema validation includes semantic validity, not just shape |
| E6 | Any successful result | Is the typed `SearchRequest` — no free-text field survives into it | The boundary claim from `docs/reference/01-architecture-overview.md`, made testable |
| E7 | Input in Portuguese | Parses equivalently to E1's English | The target market is Brazilian; monolingual intent parsing would be a product bug |
| E8 | E1's English input, then E7's Portuguese input | `Language` = `"en"` for the first, `"pt-BR"` for the second | `Language` has to actually be populated correctly, not just present as an unused field — task 11 reads it |

### Locked decisions

- **Missing required fields fail loudly** (E3). No defaults, no inference. A guessed destination is
  worse than an error, because the user cannot tell it was guessed.
- Validation happens after the typed parse, in deterministic code — never delegated to the model's own
  judgement.

## Done when

All eight evals pass.
