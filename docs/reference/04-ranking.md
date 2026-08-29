# Ranking: a scoring function, not a chat

`FlightAi.Core/Services/Ranking/OfferScorer.cs` and `FlightAi.Core/Models/Ranking/ScoringWeights.cs`.

Ranking flights is a scoring function over structured offers, not a model call — a deterministic
function you can test, tune, and explain. No model call happens anywhere inside `OfferScorer`: the same
offers, in the same order, produce the same ranking every time, which is the property the unit tests in
`FlightAi.Tests/OfferScorerTests.cs` hold it to (`Rank_IsDeterministic_AcrossRepeatedRuns` and
`Rank_IsIndependentOfInputOrder`).

## The weights

`ScoringWeights` covers price, duration, stop count, layover quality, departure/arrival desirability,
fare flexibility, and carrier reliability. Defaults anchor price at roughly 40%, matching a publicly
circulated read on Google Flights' "Best" tab weighting (price ~40% / duration ~30% / stops ~20% /
layover ~10%) — treat that as a sanity check on a starting point, not gospel. Ship with hand-tuned
weights like these, then learn them from real booking outcomes.

## Why `Margin` defaults to zero

`Margin` defaults to zero on purpose: it's a commercial lever, and turning it on should be a deliberate,
visible, versioned decision — never an accident of a default value nobody looked at. If a real system
wants ranking to favor higher-margin offers, that has to be a conscious choice made in the open (product
and legal can see the weight, because it lives in code, not inside a prompt), not something that creeps
in as a side effect of tuning.

`OfferScorerTests.cs` has a dedicated test (`MarginWeight_DefaultsToZero_SoItNeverSwaysRankingByAccident`)
proving two offers that differ only in margin score identically under default weights — and a companion
test proving margin *does* affect ranking once the weight is deliberately turned on. Both properties are
worth keeping in any rebuild: the default is safe, and the override is available but explicit.
