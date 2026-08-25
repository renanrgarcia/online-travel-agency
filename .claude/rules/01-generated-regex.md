# Rule 01 — Source-generated regex, never `new Regex(...)`

Every `Regex` in this codebase is declared via the `[GeneratedRegex]` source generator, never
constructed with `new Regex(pattern, options)` — including with `RegexOptions.Compiled`.

## Pattern

```csharp
public sealed partial class SomeType
{
    private static readonly Regex SomePattern = SomeRegex();

    // ... use SomePattern ...

    [GeneratedRegex(@"your-pattern-here", RegexOptions.Compiled)]
    private static partial Regex SomeRegex();
}
```

The containing type must be declared `partial` — a partial method (`SomeRegex()`) can only live inside
a type that's itself marked `partial`, regardless of whether that type is actually split across files.
The Roslyn source generator emits the method body into a separate, compiler-generated file at build
time; it's not something you write by hand, and it isn't physically present in the file you're editing
— find it via your IDE's "Go to Definition" on the generated method, or under
`obj/**/generated/**/*.g.cs` after a build.

## Why

- The pattern is compiled to IL **at build time**, not the first time the regex runs. `new Regex(p,
  RegexOptions.Compiled)` compiles via runtime reflection emission on first use instead — slower
  startup, and that cost repeats on every cold start (which matters on App Service F1's free tier and
  Azure Functions' consumption plan, both of which cold-start regularly).
- The generated implementation is real, debuggable, steppable C# — not a black box.
- A malformed pattern is a **build error**, not a runtime exception discovered the first time that code
  path executes.

## Reference implementation

[`backend/src/FlightAi.Core/Services/Pricing/ExplanationPlaceholderRenderer.cs`](../../backend/src/FlightAi.Core/Services/Pricing/ExplanationPlaceholderRenderer.cs) —
`TokenRegex()`, `DigitRegex()`, `MagnitudeWordsRegex()`.
