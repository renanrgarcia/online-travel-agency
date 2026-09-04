# Lessons learned: bugs and friction found while building this

Worth knowing about, because they only show up when you actually run the code rather than read it.

## The token regex didn't allow hyphens, but offer IDs contain them

The token regex in `ExplanationPlaceholderRenderer` originally only allowed `[A-Za-z0-9_]` inside a
token, but offer IDs contain hyphens (`GDS-a41a6784`). Tokens silently failed to match at all — and
because a token that never matches the pattern never reaches the "unresolved" list either, the renderer
reported the output as clean while the traveller-facing text still had raw `{{...}}` placeholders in
it. Fixed by widening the character class and adding a `HasUnmatchedBraces` check that flags *any*
literal `{{` surviving rendering, matched or not — a safety net specifically for the case where the
"unresolved token" tracking itself has a blind spot.

## Currency formatting used the ambient culture

Formatting used `CultureInfo.CurrentCulture` implicitly. On a machine whose OS region uses a comma
decimal separator, `500.00 USD` silently rendered as `500,00 USD` — no error, no warning, just a
differently-shaped price string depending on which machine happened to run the process. Fixed with
explicit `CultureInfo.InvariantCulture` wherever a price gets formatted, and the same pin at the top of
each console/API entry point.

## `WriteAsJsonAsync` already sets `Content-Type`

A manual `Headers.Add("Content-Type", "application/json")` before calling `WriteAsJsonAsync` — which
already sets that header itself — threw `FormatException: Cannot add value because header
'Content-Type' does not support multiple values`. The booking status endpoint 500'd on every call until
that line was deleted. Caught by actually calling the endpoint with `curl`, not by `dotnet build`, which
saw nothing wrong.

## Azurite rejected the Storage SDK's API version

Azurite 3.36 rejects the API version the current `Azure.Storage.Blobs` client sends (`2026-02-06`), so
the Durable Task orchestration listener failed to start with a storage error that has nothing to do
with any application code. The fix is a flag Azurite's own error message names directly:
`azurite --skipApiVersionCheck`. Worth knowing before spending time suspecting your own code for what's
actually a tooling-version mismatch between the local emulator and a newer client library.

## A retired model, an undocumented daily quota, and a false-positive guard -- all found only against a real model

Task 17 (swap the offline chat client for a real Gemini-backed one) surfaced three real gaps the
deterministic offline stand-in had never been able to exercise, all in one live test session:

- **The documented model string was retired.** `gemini-2.5-flash` -- the model
  `08-package-versions.md` had originally verified against -- failed with a 404 whose body named its
  own replacement (`gemini-3.6-flash`). That replacement then turned out to cap the free tier at 20
  requests/day/project, too tight for this app's 2-calls-per-search shape; `gemini-2.5-flash-lite` was
  *also* retired before `gemini-3.5-flash-lite` finally worked without hitting a wall. See
  `08-package-versions.md` for the full blow-by-blow. The general lesson: a model string is not a fact
  that stays true, and free-tier quotas are no longer even published anywhere you could check them
  without an account.
- **An offer ID's own digits tripped the price-integrity guard.** `ExplanationPlaceholderRenderer`'s
  raw-digit scan doesn't distinguish "a digit that's part of an identifier the model was explicitly
  given" from "a digit the model invented" -- so a model naturally writing "Offer LCC-002" in prose (the
  offer ID is plain text, never a token) got rejected as a violation, every single time, against a real
  model. The offline fixture's canned responses never mention an offer ID in prose, so this was
  structurally impossible to catch without a real model actually choosing its own wording. See
  `02-price-integrity.md`'s "One deliberate exception to the raw-digit scan" for the fix.
- **A real model returned "pt" instead of "pt-BR."** `PriceReferenceStore`'s localization is a strict
  `language == "pt-BR"` check (task 18), and `IntentAgentFactory`'s instructions only said to infer the
  language, not the exact code format expected downstream. The offline fixture's canned intent result
  hardcodes the exact string, so this mismatch was invisible until a real model made its own judgment
  call about how to format the value. Fixed by spelling out the exact expected codes in the agent's
  instructions.

All three share the same shape as the general lesson below: nothing here was catchable by reading the
code, or even by running the test suite against the offline stand-in. Only calling the real, actual
external model exposed them -- which is the entire point of keeping the offline path around forever
while still eventually swapping in the real thing.

## The general lesson

The first two bugs above are the classic "silent, no error in the logs" failure shape — nothing
crashes, nothing logs a warning, the output is just quietly wrong. The third is the same failure shape
one layer up the stack, in the transport rather than the data. The fourth isn't a code bug at all — it's
an undocumented tooling/version mismatch, the kind of hidden edge case that only shows up the first time
you actually run something against a local emulator instead of trusting that "it should just work."
None of these four were caught by `dotnet build`. All four were caught by actually running the system
end to end and checking real output — which is the only method that reliably catches this class of
problem.
