namespace FlightAi.Core.Models;

/// <summary>
/// Minimal offer shape sufficient to score. Replaced by the canonical <see cref="Offer"/> for real
/// pipeline use (task 04) — this stub exists so task 03 could be built and tested before suppliers
/// existed. See docs/specs/tasks/04-supplier-connector-interface.md.
/// </summary>
public sealed record ScorableOffer(string OfferId, decimal Price, TimeSpan Duration, int Stops, decimal Margin);
