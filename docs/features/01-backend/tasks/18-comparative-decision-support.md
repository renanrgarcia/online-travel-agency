# 18 — Comparative decision support

**Roadmap step:** 6. Decision support
**Source doc:** `docs/reference/02-price-integrity.md`, `docs/reference/04-ranking.md`
**Depends on:** 01, 11, 13
**Build before:** 17 — task 17's 20-run stress test against a real model should cover this mechanism,
not land after it.

## Goal

Let the explanation actually help someone *choose* — say which offer is cheaper, which is faster, which
is the only refundable one, and by how much — without the model authoring any of those comparisons.

Ranking (task 03) already decides *which offer is best*. It cannot tell a traveller *why, compared to
what*, and a ranked list alone doesn't answer "is it worth $180 more to save three hours?" That
question is the actual decision, and answering it well is the product.

## The problem this closes

The explanation agent receives opaque tokens — `{{PRICE_LCC-002}}`, `{{DURATION_LCC-002}}` — with no
embedded magnitude. It therefore has **no basis** for a claim like "a cheaper option is…" beyond the
order it received the offers in. Today, any comparative statement it makes is either narration of
code-decided rank order or an invention.

And an invented comparison is invisible to the existing guard. `ExplanationPlaceholderRenderer` rejects
digits outside a token; "LCC-001 is cheaper than LCC-002" contains no digit. A model can state the exact
opposite of the truth and pass every check the system currently has.

So this task extends the step-1 invariant one level up:

> A language model may never author a number the traveller sees — **and may never author a comparison
> either.** Both are facts about the offers, and both must come from code.

The mechanism already half exists: `PriceReferenceStore.RegisterPriceDelta` was built and tested in
task 01 (E13–E15) and has never been called by production code.

## Scope

- **Price deltas** between explained offers — wire up the existing `RegisterPriceDelta`.
- **Duration deltas** — the same pattern, new registration (`"3h shorter"` / `"2h 30m longer"` /
  `"the same duration"`).
- **Superlative facts** — cheapest, fastest, fewest stops, only refundable option. Computed from the
  offer set, registered as tokens, so a superlative is as code-authored as a price.
- **Comparison facts reach the explanation agent** alongside the per-offer tokens, and the agent's
  instructions require every comparative claim to use one.
- **Resolved token text follows the request's language** (see below — this is a live gap, not new work
  invented for this task).

## Out of scope

- **Decimal-separator localisation.** `R$180.00` stays dot-separated rather than becoming `R$180,00`.
  `docs/reference/09-lessons-learned.md` documents a real bug from culture-dependent number formatting,
  and task 01 locked invariant culture in response. Localising *words* doesn't reopen that; localising
  *numeric format* does, and it deserves its own decision rather than being smuggled in here.
- **Comparisons against offers outside the explained set** (E4).
- **Structurally detecting an invented comparison** (E10) — see the limitation below.

## The existing localisation gap

`RegisterStops` returns `"nonstop"` / `"1 stop"` / `"N stops"`. `RegisterRefundable` returns
`"refundable"` / `"non-refundable"`. `RegisterPriceDelta` returns `"$42.00 more"`. All English,
unconditionally, with no language parameter.

Task 11's E8 and E10 exercise Portuguese explanations but assert only that rendering succeeds with no
violations — never that the *rendered* text is Portuguese. So a `pt-BR` explanation renders today as
Portuguese prose containing English fragments: *"A melhor oferta é $500.00, com nonstop."* Adding delta
and superlative tokens multiplies that leakage, which is why fixing it belongs here rather than being
deferred.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Two explained offers at 590 and 410, same currency | The delta token between them resolves to `"$180.00 more"` (and its inverse to `"$180.00 less"`) | The comparison is code-authored, exactly like the number is |
| E2 | Two explained offers at 8h and 11h | Duration delta resolves to `"3h shorter"` / `"3h longer"`; equal durations resolve to `"the same duration"` | Same mechanism, second dimension — price alone doesn't drive the decision |
| E3 | Three offers where a different one is cheapest, fastest, and uniquely refundable | Each superlative token resolves only for the offer that actually holds it; no offer receives a superlative it doesn't hold | A false superlative is a lie the digit guard cannot catch, so it must be impossible to *generate* |
| E4 | Six ranked offers, top three explained | No comparison fact is registered involving ranks 4–6 | Don't state facts about offers the traveller isn't being shown |
| E5 | The prompt sent to the `IChatClient` | Contains no raw price or duration digits — only tokens (task 11 E2, extended to the new tokens) | The comparison mechanism must not itself become the leak it was built to prevent |
| E6 | Model response using delta and superlative tokens | Renders clean, every token resolved, guard passes | End to end through the price-integrity boundary |
| E7 | Model writes `"about 20% cheaper"` instead of using a delta token | Still rejected by task 02's digit guard | The existing guard keeps doing its job against the obvious failure |
| E8 | `pt-BR` request, offers with 0 stops and a refundable fare | Every resolved token's text is Portuguese (`"sem escalas"`, `"reembolsável"`, `"a mais"`) — no English fragment survives into the rendered output | The target market. Today this silently fails |
| E9 | Same offers, same language, twice | Byte-identical comparison facts | Determinism, the same property ranking has |
| E10 | Model writes `"but it's faster"` with no token anywhere | **Not** caught — renders clean | Documents exactly where the structural guarantee stops. See below |

### Locked decisions

- **Comparisons are computed only among the offers actually explained** (`ExplainedOfferCount`, 3
  today), never the full result set.
- **Deltas are relative to the top-ranked offer**, not every pair — with three offers, all-pairs is six
  facts to state and reads as noise. Rank 1 is the reference point the traveller is deciding against.
- **Superlatives are tokens, not plain words in the instructions.** `"the cheapest option"` arriving as
  resolved token text keeps the claim code-authored; letting the model write the word "cheapest" itself
  does not.
- **Resolved text is localised by word, not by number format** — see Out of scope.
- **The model still writes the connective prose.** Handing it a fully-formed comparison sentence as one
  token would be maximally safe and would also make the agent pointless; per-dimension tokens plus model
  glue is the deliberate middle position.

### The limitation, stated honestly (E10)

A digit outside a token is mechanically detectable. A comparative *adjective* is not — catching
"faster" used wrongly would need real comprehension of two-language free text, and a keyword lexicon
would produce false positives on correct sentences that happen to contain a comparative word.

So this task makes correct comparison **available and instructed**, not **enforced**. That is a weaker
guarantee than the digit guard, and E10 exists to keep it visible rather than let the stronger claim get
assumed. Strengthening it — a lexicon check, or a second model pass verifying claims against the facts —
is a real follow-on, deliberately not attempted here.

## Done when

E1–E9 pass and E10 is documented and understood. E3 and E8 are the ones that matter most: E3 because a
confidently wrong superlative destroys trust faster than a missing one, and E8 because half the intended
audience reads Portuguese.
