# Rule 02 — Primary constructors for simple dependency capture

When a type's constructor does nothing but store a parameter for later use, declare it as a primary
constructor and reference the parameter directly in members. Don't declare a private readonly field and
an explicit constructor body just to copy a parameter into it.

## Pattern

```csharp
// Yes:
public sealed class SomeType(SomeDependency dependency)
{
    public void DoWork() => dependency.DoSomething();
}

// No:
public sealed class SomeType
{
    private readonly SomeDependency _dependency;
    public SomeType(SomeDependency dependency) => _dependency = dependency;
    public void DoWork() => _dependency.DoSomething();
}
```

## When this rule doesn't apply

Fall back to a conventional constructor + field when the parameter needs validation, transformation, or
storage under a different name/shape than what the constructor received, or when the type has multiple
constructors. A primary constructor parameter that's just captured for later use is the target case;
don't force the pattern where real constructor logic belongs.

## Why

Less code for the same behavior, and it reads as what it is — this type's only job regarding
`dependency` is to hold onto it. Note this is about compact syntax for constructor-parameter capture,
not about how the value gets there: nothing here implies a DI container is involved. A primary
constructor parameter is just a constructor parameter; whether it's supplied by `new SomeType(x)`
directly or resolved by an IoC container later is a separate decision, made wherever the type is
actually constructed.

## Reference implementation

[`backend/src/FlightAi.Core/Services/ExplanationPlaceholderRenderer.cs`](../../backend/src/FlightAi.Core/Services/ExplanationPlaceholderRenderer.cs) —
`ExplanationPlaceholderRenderer(PriceReferenceStore store)`.
