namespace FlightAi.Api;

/// <summary>
/// The <c>explanation</c> SSE event's payload, per docs/06-api-sse-contract.md. <see cref="Text"/> is
/// safe to show a traveller. <see cref="Raw"/> is the model's literal output before token resolution —
/// a debug view showing tokens in place. <see cref="IsClean"/> is false if any token failed to resolve
/// or a stray digit/word was found outside a token; see docs/02-price-integrity.md.
/// </summary>
public sealed record ExplanationPayload(string Text, string Raw, bool IsClean);
