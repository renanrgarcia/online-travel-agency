# Bilingual UI — `frontend/src/i18n/`, `frontend/src/chat/turnLanguage.ts`

The backend carries language end to end already: the intent agent infers it from the query itself
(`parsed-intent.language`, `05-agents-and-intent.md`), and the explanation agent writes in that same
language (`ExplanationAgentFactory.BuildInstructions`, backend task 11). The frontend's job is narrower
than it sounds — make the *chrome* around that content follow the same language, without ever
translating the content itself.

## No i18n library

Two languages (`en`, `pt-BR`) and a flat set of UI-chrome keys is a lookup table:

```ts
export const STRINGS: Record<Language, Strings> = { en, 'pt-BR': ptBR }
```

`Strings` is one interface, both language objects satisfy it in full — a missing key in either is a
compile error, not a silent fallback to the wrong language. A real i18n library would add a dependency
and pluralization/interpolation machinery this doesn't need; `strings.stopsMany.replace('{n}', ...)` is
the entire interpolation story here.

## Locked decision: language comes from `parsed-intent`, not a setting

Once a search has run, its own `parsed-intent.language` — what the intent agent actually inferred from
the words the user typed — is better evidence of the right language than browser locale or a stale
manual pick. `App.tsx`'s `AppShell` watches `chat.turns` for the most recently *resolved* language
(`turnLanguage.ts`'s `latestResolvedTurnLanguage`) and calls the same `setLanguage` the manual toggle
uses, every time a new search resolves one. There's no separate "auto" vs. "manual" mode to reconcile —
both write the same piece of state, whichever happened most recently wins, and the manual toggle
(`LanguageToggle.tsx`) stays visible and functional throughout, since it's still the only source of
truth before any search has run at all.

## The harder problem: a chat log is a history

A completed search turn must keep the language it was actually answered in, even after a *later*
search in a different language moves the app's own chrome on. Retroactively relabelling an old turn's
stage headers, supplier statuses, or booking progress text would misrepresent what the app actually
said at the time — the same reasoning a chat transcript never gets machine-retranslated after the
fact.

This rules out the obvious approach (every component just reads `useLanguage()`'s ambient value) for
anything rendered *inside* a turn. The fix has two parts:

**1. Turn language is derived, not stored, for search turns.**

```ts
// turnLanguage.ts
export function assistantTurnLanguage(turn: AssistantTurn, fallback: Language): Language {
  return turn.stages.parsedIntent ? toUiLanguage(turn.stages.parsedIntent.language) : fallback
}
```

An `AssistantTurn`'s own `stages.parsedIntent.language` is a plain field on data that's already
immutable once set (`applyEvent`'s `parsed-intent` case only ever fires once per turn) — so
`AssistantTurnView` recomputes this on every render, and it's stable regardless of what the *ambient*
chrome language has since become. Before `parsedIntent` arrives (the brief "Understanding your
search…" window), there's no better evidence yet, so it falls back to whatever's currently active —
the same thing "searching…" itself is doing.

A `BookingTurn` has no `parsed-intent` of its own to derive from — it's created by clicking "Book this
offer" on a specific search turn's specific offer. So its `language: Language` field is a real,
stored value, threaded through the click at creation time (`ChatView`'s `onBookOffer` →
`AssistantTurnView` passes the *source* turn's own resolved language → `chat.startBooking(offer,
language)`), not read back from ambient state later. Once set, it never changes — a booking turn
freezes to the conversation it came from.

**2. `LanguageOverride` freezes descendants that don't know turns exist.**

`OfferCard` and `OfferComparison` call `useLanguage()` internally, same as every other component —
they were built before F07 and have no reason to know a "turn" concept exists at all. Rather than
thread a `strings` prop through components that don't otherwise need one, `AssistantTurnView` wraps
just the offers it renders in a scoped context override:

```tsx
<LanguageOverride language={turnLanguage}>
  <ol className="offer-list">{/* OfferCard × N */}</ol>
  <OfferComparison offers={stages.rankedOffers} />
</LanguageOverride>
```

`LanguageOverride` is a second `LanguageContext.Provider`, nested inside the app's real one, pinned to
a fixed language with a no-op `setLanguage` (nothing inside a frozen turn should be changing chrome-wide
state). Any component further down the tree that calls `useLanguage()` — today's `OfferCard`/
`OfferComparison`, and anything added later — picks up the frozen value automatically, via React's own
context resolution, with zero coupling back to the fact that a turn exists. `BookingTurnView` doesn't
need this trick: it has nothing nested that calls `useLanguage()` on its own, so it just computes
`STRINGS[turn.language]` directly.

## What's never translated

The explanation text, `raw`, and every offer value (price, duration, stops, refundability) pass
through the frontend completely untouched, regardless of which language chrome happens to be showing.
This isn't an oversight — translating a resolved price or a model-written explanation in the browser
would mean the frontend authoring content the backend's price-integrity design
(`02-price-integrity.md`) specifically exists to keep out of anyone's hands but the server. The
explanation already arrives written in the query's own language; the frontend's only job is to get its
own chrome to match it, not to second-guess or re-render the content itself.

## Verifying this without a real bilingual model call

The offline demo client (`FlightAi.Api`'s `DemoOfflineChatClient`, backend task 09) matches on a fixed
substring and always returns the same canned, English-language `parsed-intent`, regardless of what
language the matched query was actually typed in — there's no way to make it produce a genuinely
Portuguese-detected intent through a live search. F07's live verification instead toggled the manual
language switch mid-conversation, after a real (English-resolved) search had completed, and confirmed
in the running browser that the completed turn's content and a booking turn started from it both
stayed English while the app's own chrome followed the toggle — proving the freezing mechanism live
without needing the backend to genuinely detect Portuguese in that session. The component-level tests
(`AssistantTurnView.test.tsx`, `BookingTurnView.test.tsx`, `turnLanguage.test.ts`) cover the
Portuguese-detection path directly, since they construct the `parsed-intent` payload by hand rather
than depending on a real model call.
