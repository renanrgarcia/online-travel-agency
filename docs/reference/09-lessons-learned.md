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

## The general lesson

The first two bugs above are the classic "silent, no error in the logs" failure shape — nothing
crashes, nothing logs a warning, the output is just quietly wrong. The third is the same failure shape
one layer up the stack, in the transport rather than the data. The fourth isn't a code bug at all — it's
an undocumented tooling/version mismatch, the kind of hidden edge case that only shows up the first time
you actually run something against a local emulator instead of trusting that "it should just work."
None of these four were caught by `dotnet build`. All four were caught by actually running the system
end to end and checking real output — which is the only method that reliably catches this class of
problem.
