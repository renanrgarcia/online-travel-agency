# F07 — Bilingual UI

**Roadmap step:** 4. Honesty and reach
**Source doc:** `docs/reference/05-agents-and-intent.md`, backend task 18
**Depends on:** F03, F06

## Goal

Make the interface itself bilingual (pt-BR / en), so a Portuguese query doesn't produce Portuguese
prose wrapped in English chrome.

The backend already carries language end to end: the intent agent infers it from the query, and the
explanation agent writes in it. The UI is the last place that gap shows.

## Scope

- UI strings in both languages: labels, status text, degraded-state copy, booking progress.
- Language follows the `language` field from `parsed-intent` — the same decision the backend already
  made, not a second one.
- A manual override for chrome before any search has run.

## Out of scope

- Any language beyond pt-BR and en.
- Translating server-provided content. The explanation arrives already written in the right language,
  and offer values are resolved server-side; re-translating either in the browser would fork the price
  and comparison logic F04 E8 exists to prevent.
- Localising number and date *formats* — backend task 18 deliberately keeps invariant formatting, and
  this task doesn't reopen it.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A Portuguese query | Explanation in Portuguese, and UI chrome in Portuguese too | The bug this task fixes: mixed-language output reads as broken regardless of how good the translation is |
| E2 | An English query | Both in English | The mechanism works in both directions, not just defaulting to one |
| E3 | Language switching between consecutive searches | Each turn keeps the language it was answered in | A chat log is a history; retroactively relabelling old turns misrepresents what was said |
| E4 | Degraded-state copy (F06) | Translated in both languages | The states most likely to be shipped English-only, and the ones a confused user most needs to read |
| E5 | Any UI string, searched for in the codebase | No user-facing English literal inline in a component | The mechanical property that keeps E4 true as the UI grows |
| E6 | Before any search | Chrome renders in a sensible default, with a manual override available | There's no `parsed-intent` yet, so something has to decide |
| E7 | Server-provided text (explanation, offer values) | Passed through untranslated | Translating a resolved price would re-author a number in the browser |

### Locked decisions

- **Language comes from `parsed-intent`**, not from browser locale, once a search has run. The backend
  inferred it from what the user actually typed, which is better evidence than a system setting.
- **No i18n library.** Two languages and a flat set of keys is a `Record<Language, Record<Key, string>>`
  and a lookup; a library would add a dependency and a build step for pluralisation and interpolation
  machinery this doesn't need.

## Done when

All seven evals pass, verified with a real Portuguese query end to end against a running backend.
