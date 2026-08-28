# Phase 9 M4 — Styling & Animation in Markup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let markup declare `Style`/`VisualStateGroup`/`VisualState` (with optional transition animation), resource dictionaries (hierarchical, merged, implicit-by-type), and `Timeline` as a reusable resource — the four already-working C# styling/animation types become markup-serializable.

**Architecture:** Three small, genuinely reusable extensions to the markup loader (constructor-argument binding, duck-typed collection population, and a declarative "attribute overflow becomes a setter" convention) let `Style`/`VisualState`/`VisualStateGroup`/`Timeline`/`AnimationKeyframe` be constructed from markup *without* changing their C# constructor shapes at all. A new `ResourceDictionary` type plus a `{StaticResource}` extension reuse the existing `Parent`/`LogicalParent` walk `DataContext` already established. VisualState transitions reuse the existing `Animation`/`PropertyValuePrecedence.Animation` tier, with a small per-property in-flight-animation tracker added to `UIElement` so re-triggering a transition replaces rather than fights the previous one.

**Tech Stack:** C#/.NET 8, xUnit, the existing `Icy.Markup`/`Icy.Data.Markup`/`Icy.UI.Styles`/`Icy.Animations` namespaces.

**Spec:** `docs/superpowers/specs/2026-08-26-m4-styling-animation-markup-design.md` — read it alongside this plan; this plan resolves the spec's two flagged "open items for implementation planning" (keyframe interruption mechanics in Task 11, `Timeline` markup-shape compatibility in Task 12) and one gap the spec didn't mention at all (none of `Style`/`VisualState`/`VisualStateGroup`/`Timeline`/`AnimationKeyframe` have a parameterless constructor, which `DefaultMarkupActivator` requires — Task 1).

**Design decisions confirmed with Ivan before this plan was written** (see conversation): constructor-argument binding lives in the activator as a *generic* mechanism (not per-type special-casing); `VisualState`'s `State` is a fourth reserved attribute, bound the same generic way; `Timeline.Keyframes` stays `IReadOnlyList<AnimationKeyframe>` (not widened to `IList`) and is populated through a generic duck-typed-`Add`-method extension to collection population, not a `Timeline`-specific loader branch.

## Global Constraints

- No `ControlTemplate`/`DataTemplate` this milestone (spec's explicit scope cut) — do not add either.
- Every public type/member touched or added needs complete XML doc comments (`<summary>`, `<param>`, `<returns>`, `<remarks>`, `<see cref>`/`<paramref>`/`langword` as appropriate) — this drives the generated docs site.
- `MarkupLoader` stays synchronous and all-or-nothing (first error is a `MarkupException` carrying file position; no partially-built tree is ever returned) — every new code path must throw `MarkupException.At(...)`, not swallow or partially apply.
- This milestone touches core `IcyUI` only — no `IcyUI.MonoGame`/`IcyUI.Stride`/`IcyUI.FNA` project changes — except the two sample `.csproj`s picking up one new shared demo file (`<Compile Include>`, matching the M1–M3.5 precedent).
- Existing C# call sites (`new Style(typeof(Button))`, `new VisualStateGroup("CommonStates")`, `new VisualState<Button>("Normal", ControlState.Hovered)`, `Timeline.FromTo(...)`, hand-built `AnimationKeyframe`) must keep compiling and behaving identically — every change in this plan is additive to those types' public surface, never a breaking rename/removal.
- Full test suite must stay green after every task (currently 442/442 per the M3.5 milestone note) — run the full suite, not just the new tests, before each commit.

---

## Task 1: Generic constructor-argument binding for parameterless-ctor-less markup types

**Files:**
- Modify: `sources/IcyUI/Markup/IMarkupActivator.cs`
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs`
- Test: `sources/IcyUI.Tests/Markup/MarkupLoaderConstructorBindingTests.cs` (new)

**Interfaces:**
- Produces: `IMarkupActivator.CreateInstance(Type type, IReadOnlyDictionary<string, object?> constructorArguments)` (new interface member, default-implemented to throw so no existing implementer breaks); `DefaultMarkupActivator`'s working implementation of it.
- Produces: `MarkupLoader.CreateObject` now consumes constructor-bound attributes before `ApplyAttributes` sees them (internal behavior change, no public signature change).

Today `DefaultMarkupActivator.CreateInstance(Type)` throws when a type has no parameterless constructor. `Style(Type)`, `VisualState(string, ControlState)`, `VisualStateGroup(string)`, `Timeline(string, TimeSpan)`, and `AnimationKeyframe(float, object?)` (a `readonly struct`) all fall into this category, and none of them should gain a parameterless constructor (that would change their C# ergonomics and validation-at-construction guarantees). Instead, when an element's tag type has no parameterless constructor, the loader finds attributes matching the (single) parameterized constructor's parameter names — case-insensitive — converts each, and constructs directly.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Markup/MarkupLoaderConstructorBindingTests.cs
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupLoaderConstructorBindingTests
    {
        private class RequiresName : UIElement
        {
            public RequiresName(string name)
            {
                BoundName = name;
            }

            public string BoundName { get; }

            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<RequiresName>();
            return configuration;
        }

        [Fact]
        public void CreateInstance_BindsAttributeToMatchingConstructorParameter_CaseInsensitive()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var element = (RequiresName)loader.Load("""<RequiresName nAmE="Alice"/>""");

            Assert.Equal("Alice", element.BoundName);
        }

        [Fact]
        public void CreateInstance_MissingRequiredAttribute_ThrowsMarkupException()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.Load("""<RequiresName/>"""));
            Assert.Contains("name", ex.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreateInstance_ConstructorAttributeIsNotAlsoAppliedAsAnOrdinaryProperty()
        {
            // BoundName has no setter - if the loader tried to apply "Name" a second time as a property after
            // consuming it for the constructor, this would throw "read-only" instead of succeeding.
            var loader = new MarkupLoader(CreateConfiguration());

            var element = (RequiresName)loader.Load("""<RequiresName Name="Bob"/>""");

            Assert.Equal("Bob", element.BoundName);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupLoaderConstructorBindingTests"`
Expected: FAIL — `RequiresName` has no parameterless constructor, so `MarkupLoader.Load` throws `MarkupException` from today's `DefaultMarkupActivator.CreateInstance(Type)` for every test, including the one asserting success.

- [ ] **Step 3: Extend `IMarkupActivator`**

```csharp
// sources/IcyUI/Markup/IMarkupActivator.cs — add to the existing interface
/// <summary>
/// Creates an instance of <paramref name="type"/> using a constructor whose parameters are all satisfied by
/// <paramref name="constructorArguments"/>, for a type that has no parameterless constructor.
/// </summary>
/// <param name="type">The type to construct, already resolved from the markup tag.</param>
/// <param name="constructorArguments">
/// The already-converted argument values, keyed by constructor parameter name (case-insensitive).
/// </param>
/// <returns>The new instance. Never <see langword="null"/>.</returns>
/// <exception cref="MarkupException">
/// <paramref name="type"/> has no single public constructor whose parameters are all covered by
/// <paramref name="constructorArguments"/>, or construction threw.
/// </exception>
/// <remarks>
/// The default implementation throws, so an existing custom <see cref="IMarkupActivator"/> that only overrides
/// <see cref="CreateInstance(Type)"/> keeps compiling and fails clearly for a type it can't construct, rather
/// than silently misbehaving.
/// </remarks>
object CreateInstance(Type type, IReadOnlyDictionary<string, object?> constructorArguments) =>
    throw new MarkupException($"'{type.FullName}' has no parameterless constructor, and this {nameof(IMarkupActivator)} doesn't support constructor-argument binding.");
```

Add to `DefaultMarkupActivator`:

```csharp
/// <inheritdoc/>
public object CreateInstance(Type type, IReadOnlyDictionary<string, object?> constructorArguments)
{
    ArgumentNullException.ThrowIfNull(type);
    ArgumentNullException.ThrowIfNull(constructorArguments);

    System.Reflection.ConstructorInfo constructor = ResolveConstructor(type, constructorArguments.Keys);
    object?[] arguments = [.. constructor.GetParameters().Select(p => constructorArguments[p.Name!])];

    try
    {
        return constructor.Invoke(arguments);
    }
    catch (Exception ex)
    {
        throw new MarkupException($"'{type.FullName}' threw while being constructed: {ex.Message}", innerException: ex);
    }
}

private static System.Reflection.ConstructorInfo ResolveConstructor(Type type, IEnumerable<string> availableNames)
{
    var available = new HashSet<string>(availableNames, StringComparer.OrdinalIgnoreCase);
    var candidates = type.GetConstructors()
        .Where(c => c.GetParameters().Length > 0 && c.GetParameters().All(p => available.Contains(p.Name!)))
        .ToList();

    if (candidates.Count == 1)
        return candidates[0];

    throw new MarkupException(candidates.Count == 0
        ? $"'{type.FullName}' has no parameterized constructor whose parameters are all covered by its markup attributes ({string.Join(", ", available)})."
        : $"'{type.FullName}' has {candidates.Count} constructors whose parameters are all covered by its markup attributes - markup construction requires exactly one match.");
}
```

- [ ] **Step 4: Wire `MarkupLoader.CreateObject` to use it**

Replace the body of `CreateObject` in `sources/IcyUI/Markup/MarkupLoader.cs`:

```csharp
private object CreateObject(XElement element, MarkupLoadContext context)
{
    Type type = ResolveInstanceType(element, context);
    HashSet<string>? consumedByConstructor = null;
    object instance = type.GetConstructor(Type.EmptyTypes) != null
        ? markup.Activator.CreateInstance(type)
        : CreateObjectFromConstructorAttributes(type, element, context, out consumedByConstructor);

    ApplyAttributes(element, instance, context, consumedByConstructor);
    ApplyChildren(element, instance, context);
    return instance;
}

/// <summary>
/// Constructs <paramref name="type"/> through its single parameterized public constructor, binding each
/// parameter to an attribute of the same name (case-insensitive).
/// </summary>
private object CreateObjectFromConstructorAttributes(Type type, XElement element, MarkupLoadContext context, out HashSet<string> consumed)
{
    var candidates = type.GetConstructors().Where(c => c.GetParameters().Length > 0).ToList();
    if (candidates.Count != 1)
    {
        throw MarkupException.At(
            $"'{type.Name}' has no parameterless constructor, and markup can only construct a type with exactly one parameterized public constructor (found {candidates.Count}).",
            element,
            context.SourcePath);
    }

    System.Reflection.ParameterInfo[] parameters = candidates[0].GetParameters();
    var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (System.Reflection.ParameterInfo parameter in parameters)
    {
        XAttribute? attribute = element.Attributes().FirstOrDefault(a =>
            !a.IsNamespaceDeclaration
            && !MarkupNamespaces.IsDirective(a.Name.Namespace)
            && string.Equals(a.Name.LocalName, parameter.Name, StringComparison.OrdinalIgnoreCase));

        if (attribute == null)
        {
            throw MarkupException.At(
                $"'{type.Name}' requires an attribute '{parameter.Name}' - it has no parameterless constructor.",
                element,
                context.SourcePath);
        }

        object? value = parameter.ParameterType == typeof(Type)
            ? types.ResolveTypeName(attribute.Value, element, attribute, context.SourcePath)
            : ConvertValue(attribute.Value, parameter.ParameterType, attribute, context);

        arguments[parameter.Name!] = value;
        consumed.Add(attribute.Name.LocalName);
    }

    return markup.Activator.CreateInstance(type, arguments);
}
```

Update `ApplyAttributes` to accept and skip consumed names:

```csharp
private void ApplyAttributes(XElement element, object instance, MarkupLoadContext context, HashSet<string>? consumedByConstructor = null)
{
    foreach (XAttribute attribute in element.Attributes())
    {
        if (attribute.IsNamespaceDeclaration)
            continue;
        if (consumedByConstructor != null && consumedByConstructor.Contains(attribute.Name.LocalName))
            continue;
        // ... unchanged body
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupLoaderConstructorBindingTests"`
Expected: PASS (all 3)

- [ ] **Step 6: Run the full suite**

Run: `dotnet test sources/IcyUI.Tests`
Expected: PASS, no regressions (baseline 442/442)

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/Markup/IMarkupActivator.cs sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI.Tests/Markup/MarkupLoaderConstructorBindingTests.cs
git commit -m "Add generic constructor-argument binding to the markup activator"
```

---

## Task 2: Generic duck-typed `Add`-method collection population

**Files:**
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs`
- Test: `sources/IcyUI.Tests/Markup/MarkupLoaderCollectionPopulationTests.cs` (new)

**Interfaces:**
- Consumes: nothing new.
- Produces: `MarkupLoader`'s collection-population branches (`ApplyPropertyElement`, `ApplyContentChildren`) now populate any *concretely* `Add`-able collection, not just ones whose declared property type is `IList`. `Timeline.Keyframes` (declared `IReadOnlyList<AnimationKeyframe>`, backed at runtime by a real `List<AnimationKeyframe>`) becomes populable through this with zero `Timeline`-specific loader code.

C#'s own collection-initializer syntax already works this way: any enumerable object with a public instance `Add` method qualifies, regardless of its statically-declared interface. Reuse that exact rule here — reflect on the *runtime* type of the current property value (not its declared type) for a public `Add(T)` method whose parameter accepts the item type.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Markup/MarkupLoaderCollectionPopulationTests.cs
using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupLoaderCollectionPopulationTests
    {
        // Mirrors Timeline.Keyframes' exact shape: content-property is IReadOnlyList<T>, backed by a real List<T>
        // at runtime, with no publicly-implemented IList/ICollection<T> on the declared type.
        private class ReadOnlyListHost : UIElement
        {
            private readonly List<int> items = [];

            [Markup.ContentProperty(nameof(Items))]
            public IReadOnlyList<int> Items => items;

            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        private class IntBox
        {
            public IntBox(int value) => Value = value;

            public int Value { get; }
        }

        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<ReadOnlyListHost>();
            configuration.Types.Markup.RegisterShortName<IntBox>();
            return configuration;
        }

        [Fact]
        public void ContentChildren_PopulateAnIReadOnlyListBackedByARealListAtRuntime()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var host = (ReadOnlyListHost)loader.Load(
                """
                <ReadOnlyListHost>
                  <IntBox Value="1"/>
                  <IntBox Value="2"/>
                </ReadOnlyListHost>
                """);

            // ConvertValue can't turn an IntBox into an int, so this test only proves *some* item reached the
            // list - Task 12 covers the real Timeline.Keyframes case end-to-end.
            Assert.Equal(2, host.Items.Count);
        }
    }
}
```

Note: `IntBox` here doesn't convert to `int` — swap the test body to add ints directly once conversion is confirmed, or simplify: since `ConvertValue` no-ops when the value's already assignable, change `Items` item type handling to accept `object` for this isolated test. Simplify the fixture instead — make `Items` an `IReadOnlyList<IntBox>`:

```csharp
[Markup.ContentProperty(nameof(Items))]
public IReadOnlyList<IntBox> Items => items;
```
(with `items` typed `List<IntBox>`), and assert `host.Items[0].Value == 1`. Use this corrected version when writing the file.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupLoaderCollectionPopulationTests"`
Expected: FAIL — `current is IList list` is `false` for an `IReadOnlyList<T>`-typed value that isn't also `IList`, so `ApplyContentChildren` falls into the single-value-assignment branch and throws because `Items` has no setter.

- [ ] **Step 3: Add duck-typed `Add` detection**

Add a helper near `GetItemType` in `MarkupLoader.cs`:

```csharp
/// <summary>
/// Finds a public instance <c>Add</c> method on <paramref name="collection"/>'s runtime type that accepts a
/// single argument - the same duck-typed rule C#'s own collection-initializer syntax uses, so a property
/// declared as a read-only interface (<see cref="IReadOnlyList{T}"/>, say) can still be populated as long as
/// what it actually returns has an <c>Add</c>.
/// </summary>
private static (System.Reflection.MethodInfo Add, Type ItemType)? FindAddMethod(object collection)
{
    foreach (System.Reflection.MethodInfo method in collection.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
    {
        if (method.Name != "Add")
            continue;
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 1)
            return (method, parameters[0].ParameterType);
    }

    return null;
}
```

Replace the `IList` branches in both `ApplyPropertyElement` and `ApplyContentChildren` with a shared helper called first:

```csharp
// In ApplyPropertyElement, replacing the `if (current is IList list) { ... }` block:
if (current != null && FindAddMethod(current) is { } addable)
{
    foreach (XElement entry in children)
    {
        addable.Add.Invoke(current, [ConvertValue(CreateObject(entry, context), addable.ItemType, entry, context)]);
    }

    return;
}
```

```csharp
// In ApplyContentChildren, replacing the `if (current is IList list) { ... }` block:
if (current != null && FindAddMethod(current) is { } addable)
{
    foreach (XElement child in children)
    {
        addable.Add.Invoke(current, [ConvertValue(CreateObject(child, context), addable.ItemType, child, context)]);
    }

    return;
}
```

`List<T>`, `UIElementCollection`, and every other collection already used by existing markup (`Panel.Children`, `Grid.ColumnDefinitions`, `Style.StateGroups`, `VisualStateGroup.States`) all expose a public `Add(T)`, so this is a strict superset of the old `IList`-only behavior — no existing test should need to change. Remove the now-unused `GetItemType(Type)` helper only if nothing else calls it (check first — `IDictionary` population doesn't use it).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupLoaderCollectionPopulationTests"`
Expected: PASS

- [ ] **Step 5: Run the full suite**

Run: `dotnet test sources/IcyUI.Tests`
Expected: PASS, no regressions — this is the step that proves the generalization didn't break `Panel.Children`/`Grid.ColumnDefinitions`/etc.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI.Tests/Markup/MarkupLoaderCollectionPopulationTests.cs
git commit -m "Generalize markup collection population to any duck-typed Add method"
```

---

## Task 3: Declarative attribute-overflow-to-setters convention

**Files:**
- Create: `sources/IcyUI/Markup/MarkupSetterCollectionAttribute.cs`
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs`
- Test: `sources/IcyUI.Tests/Markup/MarkupSetterCollectionTests.cs` (new)

**Interfaces:**
- Consumes: `MarkupMember.Resolve`, `ConvertValue`, `PropertyRegistry.GetPropertyStore(Type)` (already exists).
- Produces: `MarkupSetterCollectionAttribute` (a `[ContentProperty]`-shaped declarative marker), `MarkupLoadContext.SetterTargetType` (new mutable field), `MarkupLoader.TryApplyAsSetterOverflow`.

`Style` and `VisualState` both hold most of their markup-facing "properties" (`Background`, `BorderThickness`, ...) as entries in a `Dictionary<string, object?> Setters`, not as real CLR properties — the generic `ApplyProperty` path can never resolve them by reflection. Mark the two types with `[MarkupSetterCollection(nameof(Setters))]`; when `ApplyProperty` fails to resolve an attribute as an ordinary member on a type carrying that marker, it converts the value against an *ambient* "setter target type" (the nearest enclosing `<Style>`'s `TargetType`, threaded through `MarkupLoadContext`) and writes it into the marked dictionary instead of throwing "unknown property".

- [ ] **Step 1: Write the failing test**

```csharp
// sources/IcyUI.Tests/Markup/MarkupSetterCollectionTests.cs
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupSetterCollectionTests
    {
        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<TestElement>();
            configuration.Types.Markup.RegisterShortName<Style>();
            return configuration;
        }

        [Fact]
        public void UnresolvedAttributeOnStyle_BecomesASetterConvertedAgainstTargetType()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var style = (Style)loader.Load("""<Style TargetType="TestElement" Width="42"/>""");

            Assert.Equal(42f, Assert.Contains("Width", (System.Collections.IDictionary)style.Setters));
        }

        [Fact]
        public void UnresolvableSetterName_ThrowsMarkupException()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.Load("""<Style TargetType="TestElement" NotARealProperty="1"/>"""));
            Assert.Contains("NotARealProperty", ex.Description);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupSetterCollectionTests"`
Expected: FAIL — `Width` doesn't resolve on `Style` via `MarkupMember.Resolve`, so `ApplyProperty` throws "Unknown property 'Width' on 'Style'" for both tests (the second test's *expected* exception message differs from what's actually thrown, which is itself a failure signal here).

- [ ] **Step 3: Add `MarkupSetterCollectionAttribute`**

```csharp
// sources/IcyUI/Markup/MarkupSetterCollectionAttribute.cs
using System.Reflection;

namespace Icy.Markup
{
    /// <summary>
    /// Designates the property that holds a type's markup attributes that don't resolve to a real CLR member -
    /// each becomes an entry in the named <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>
    /// instead of raising an "unknown property" error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="UI.Styles.Style"/> and <see cref="UI.Styles.VisualState"/>: an attribute like
    /// <c>Background="Blue"</c> on <c>&lt;Style TargetType="Button"&gt;</c> isn't a property of <c>Style</c>
    /// itself - it's a setter to apply to a <c>Button</c> once the style is applied. The value is converted
    /// against the nearest enclosing <c>&lt;Style&gt;</c>'s <c>TargetType</c> (see
    /// <see cref="MarkupLoadContext.SetterTargetType"/>) rather than against the marked type's own properties.
    /// </para>
    /// <para>
    /// Mirrors <see cref="ContentPropertyAttribute"/>'s shape and inheritance rule.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the dictionary property attribute overflow is written into.</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class MarkupSetterCollectionAttribute(string name) : Attribute
    {
        /// <summary>
        /// Gets the name of the dictionary property attribute overflow is written into.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the setter-collection property name declared by <paramref name="type"/> or inherited from one of
        /// its base types.
        /// </summary>
        /// <param name="type">The type to look the setter-collection property up for.</param>
        /// <returns>The property's name, or <see langword="null"/> when the type declares none.</returns>
        public static string? GetSetterCollectionName(Type type) =>
            type.GetCustomAttribute<MarkupSetterCollectionAttribute>(inherit: true)?.Name;
    }
}
```

- [ ] **Step 4: Thread `SetterTargetType` through `MarkupLoadContext` and wire the overflow path**

In `MarkupLoader.cs`'s private `MarkupLoadContext` class, add:

```csharp
/// <summary>
/// Gets or sets the <see cref="Icy.UI.Styles.Style.TargetType"/> of the nearest enclosing
/// <see cref="Icy.UI.Styles.Style"/> being constructed, used to resolve an unrecognized attribute on a type
/// marked with <see cref="MarkupSetterCollectionAttribute"/> against the right <see cref="PropertyRegistry"/>
/// store. <see langword="null"/> outside any style.
/// </summary>
public Type? SetterTargetType { get; set; }
```

In `CreateObject`, save/restore it around a `Style`'s own attribute/children application (this is the only place that establishes it — `VisualStateGroup`/`VisualState` nested inside `<Style.StateGroups>` just inherit whatever is already ambient):

```csharp
private object CreateObject(XElement element, MarkupLoadContext context)
{
    Type type = ResolveInstanceType(element, context);
    HashSet<string>? consumedByConstructor = null;
    object instance = type.GetConstructor(Type.EmptyTypes) != null
        ? markup.Activator.CreateInstance(type)
        : CreateObjectFromConstructorAttributes(type, element, context, out consumedByConstructor);

    Type? previousSetterTargetType = context.SetterTargetType;
    if (instance is Icy.UI.Styles.Style style)
        context.SetterTargetType = style.TargetType;

    try
    {
        ApplyAttributes(element, instance, context, consumedByConstructor);
        ApplyChildren(element, instance, context);
    }
    finally
    {
        context.SetterTargetType = previousSetterTargetType;
    }

    return instance;
}
```

In `ApplyProperty`, try the overflow path before throwing "unknown property":

```csharp
private void ApplyProperty(object instance, string name, string value, XAttribute attribute, MarkupLoadContext context)
{
    Type type = instance.GetType();
    MarkupMember? member = MarkupMember.Resolve(type, name, registry);
    if (member == null)
    {
        if (TryApplyAsSetterOverflow(instance, name, value, attribute, context))
            return;

        if (type.GetEvent(name, BindingFlags.Public | BindingFlags.Instance) != null)
            throw MarkupException.At($"'{type.Name}.{name}' is an event. Wiring handlers from markup isn't supported yet.", attribute, context.SourcePath);

        throw MarkupException.At(
            $"Unknown property '{name}' on '{type.Name}'.{NameSuggestion.Clause(name, MarkupMember.EnumerateNames(type, registry))}",
            attribute,
            context.SourcePath);
    }

    if (!member.CanSet)
        throw MarkupException.At($"'{type.Name}.{name}' is read-only and can't be assigned from an attribute.", attribute, context.SourcePath);

    AssignValue(instance, member, value, attribute, context);
}

/// <summary>
/// Resolves <paramref name="name"/> as a setter to apply through <paramref name="context"/>'s ambient
/// <see cref="MarkupLoadContext.SetterTargetType"/>, writing it into <paramref name="instance"/>'s
/// <see cref="MarkupSetterCollectionAttribute"/>-marked dictionary.
/// </summary>
/// <returns><see langword="true"/> when <paramref name="instance"/>'s type opts into this, whether or not the
/// name itself resolved (a bad name still throws - it just throws from inside this method).</returns>
private bool TryApplyAsSetterOverflow(object instance, string name, string value, XAttribute attribute, MarkupLoadContext context)
{
    string? setterMemberName = MarkupSetterCollectionAttribute.GetSetterCollectionName(instance.GetType());
    if (setterMemberName == null)
        return false;

    if (context.SetterTargetType == null)
    {
        throw MarkupException.At(
            $"'{instance.GetType().Name}' has no governing target type in scope to resolve '{name}' against.",
            attribute,
            context.SourcePath);
    }

    if (!registry.GetPropertyStore(context.SetterTargetType).TryGetProperty(name, searchInherited: true, out IPropertyReference? property))
    {
        throw MarkupException.At(
            $"'{context.SetterTargetType.Name}' has no property '{name}' to set." +
            NameSuggestion.Clause(name, registry.GetPropertyStore(context.SetterTargetType).EnumerateProperties().Select(x => x.Name)),
            attribute,
            context.SourcePath);
    }

    var setters = (Dictionary<string, object?>)instance.GetType().GetProperty(setterMemberName)!.GetValue(instance)!;
    setters[name] = ConvertValue(value, property.PropertyType, attribute, context, instance, MarkupMember.FromReference(property));
    return true;
}
```

- [ ] **Step 5: Mark `Style` and `VisualState`**

```csharp
// sources/IcyUI/UI/Styles/Style.cs
[Markup.MarkupSetterCollection(nameof(Setters))]
public class Style(Type targetType)
```

```csharp
// sources/IcyUI/UI/Styles/VisualState.cs
[Markup.MarkupSetterCollection(nameof(Setters))]
public class VisualState(string name, ControlState state)
```

(Add `using Icy.Markup;` to both files instead of the fully-qualified attribute name if that reads more consistently with the file's existing style — check the file's other usings first.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupSetterCollectionTests"`
Expected: PASS

- [ ] **Step 7: Run the full suite, then commit**

Run: `dotnet test sources/IcyUI.Tests`

```bash
git add sources/IcyUI/Markup/MarkupSetterCollectionAttribute.cs sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI/UI/Styles/Style.cs sources/IcyUI/UI/Styles/VisualState.cs sources/IcyUI.Tests/Markup/MarkupSetterCollectionTests.cs
git commit -m "Add declarative attribute-overflow-to-setters convention for Style/VisualState markup"
```

---

## Task 4: `ResourceDictionary` type and `UIElement.Resources`

**Files:**
- Create: `sources/IcyUI/UI/ResourceDictionary.cs`
- Modify: `sources/IcyUI/UI/UIElement.cs`
- Test: `sources/IcyUI.Tests/UI/ResourceDictionaryTests.cs` (new)

**Interfaces:**
- Produces: `ResourceDictionary : IDictionary<string, object?>` (string-keyed, insertion-ordered — wrap `OrderedDictionary` or a `Dictionary<string,object?>` plus a separate ordered key list; simplest is `Dictionary<string, object?>` since .NET's `Dictionary<TKey,TValue>` already preserves insertion order in practice for pure adds/no-removes, but that's an implementation detail, not a contract — use `System.Collections.Specialized.OrderedDictionary`-style explicit ordering only if a test demands enumeration order; MergedDictionaries lookup order is the only place order is spec-load-bearing and that's a separate `List<ResourceDictionary>`, not dictionary enumeration order). `ResourceDictionary.MergedDictionaries { get; } : IList<ResourceDictionary>`. `ResourceDictionary.TryGetValue(string key, out object? value)` — own entries first, then `MergedDictionaries` in reverse (last-added wins). `UIElement.Resources { get; } : ResourceDictionary` (lazily allocated, like `Panel.Children`).
- Consumes: nothing new.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/UI/ResourceDictionaryTests.cs
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class ResourceDictionaryTests
    {
        [Fact]
        public void TryGetValue_FindsOwnEntry()
        {
            var dictionary = new ResourceDictionary { ["Accent"] = "Blue" };

            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            Assert.Equal("Blue", value);
        }

        [Fact]
        public void TryGetValue_OwnEntryShadowsMergedEntry()
        {
            var merged = new ResourceDictionary { ["Accent"] = "Red" };
            var dictionary = new ResourceDictionary { ["Accent"] = "Blue" };
            dictionary.MergedDictionaries.Add(merged);

            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            Assert.Equal("Blue", value);
        }

        [Fact]
        public void TryGetValue_LaterMergedDictionaryShadowsEarlierOne()
        {
            var earlier = new ResourceDictionary { ["Accent"] = "Red" };
            var later = new ResourceDictionary { ["Accent"] = "Green" };
            var dictionary = new ResourceDictionary();
            dictionary.MergedDictionaries.Add(earlier);
            dictionary.MergedDictionaries.Add(later);

            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            Assert.Equal("Green", value);
        }

        [Fact]
        public void TryGetValue_MissingKey_ReturnsFalse()
        {
            var dictionary = new ResourceDictionary();

            Assert.False(dictionary.TryGetValue("Nope", out _));
        }

        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        [Fact]
        public void UIElement_ResourcesIsLazyAndEmptyByDefault()
        {
            var element = new TestElement();

            Assert.Empty(element.Resources);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~ResourceDictionaryTests"`
Expected: FAIL — `ResourceDictionary` doesn't exist yet.

- [ ] **Step 3: Implement `ResourceDictionary`**

```csharp
// sources/IcyUI/UI/ResourceDictionary.cs
using System.Collections;

namespace Icy.UI
{
    /// <summary>
    /// A string-keyed collection of named resources (<see cref="Styles.Style"/>s, <see cref="Animations.Timeline"/>s,
    /// brushes, or any other value), optionally composed from other dictionaries via <see cref="MergedDictionaries"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <see cref="UIElement"/> carries its own via <see cref="UIElement.Resources"/> - lookup
    /// (<c>{StaticResource}</c>, see <see cref="Markup.Extensions.StaticResourceExtension"/>) walks from the
    /// requesting element up through <see cref="UIElement.Parent"/>/<see cref="UIElement.LogicalParent"/> to the
    /// root, checking each element's own dictionary before moving to its parent.
    /// </para>
    /// <para>
    /// Within a single dictionary, a direct entry always shadows a same-key entry from
    /// <see cref="MergedDictionaries"/>; among merged dictionaries themselves, a later one shadows an earlier one
    /// on collision - both are intentional (theme layering), not errors.
    /// </para>
    /// </remarks>
    public class ResourceDictionary : IDictionary<string, object?>
    {
        private readonly Dictionary<string, object?> entries = [];

        /// <summary>
        /// Gets the other dictionaries this one is composed from, checked in order (a later one shadows an
        /// earlier one) after this dictionary's own direct entries.
        /// </summary>
        public IList<ResourceDictionary> MergedDictionaries { get; } = [];

        /// <inheritdoc/>
        public object? this[string key]
        {
            get => entries[key];
            set => entries[key] = value;
        }

        /// <inheritdoc/>
        public ICollection<string> Keys => entries.Keys;

        /// <inheritdoc/>
        public ICollection<object?> Values => entries.Values;

        /// <inheritdoc/>
        public int Count => entries.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Looks up <paramref name="key"/> in this dictionary's own entries, then in <see cref="MergedDictionaries"/>
        /// (in reverse order, so a later merged dictionary wins).
        /// </summary>
        /// <param name="key">The resource key to look up.</param>
        /// <param name="value">The resolved value, when this returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when <paramref name="key"/> is found, directly or through a merge.</returns>
        public bool TryGetValue(string key, out object? value)
        {
            if (entries.TryGetValue(key, out value))
                return true;

            for (int i = MergedDictionaries.Count - 1; i >= 0; i--)
            {
                if (MergedDictionaries[i].TryGetValue(key, out value))
                    return true;
            }

            value = null;
            return false;
        }

        /// <inheritdoc/>
        public void Add(string key, object? value) => entries.Add(key, value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, object?> item) => Add(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear() => entries.Clear();

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)entries).Contains(item);

        /// <inheritdoc/>
        public bool ContainsKey(string key) => entries.ContainsKey(key);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, object?>>)entries).CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => entries.GetEnumerator();

        /// <inheritdoc/>
        public bool Remove(string key) => entries.Remove(key);

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)entries).Remove(item);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
```

- [ ] **Step 4: Add `UIElement.Resources`**

In `sources/IcyUI/UI/UIElement.cs`, near the `Children`-style plain properties (not a `[RegisterReference]` - resources aren't part of the value-precedence system):

```csharp
private ResourceDictionary? resources;

/// <summary>
/// Gets the resources this element declares - <see cref="Styles.Style"/>s, <see cref="Animations.Timeline"/>s,
/// or anything else keyed by name for <c>{StaticResource}</c> lookup within this element's subtree.
/// </summary>
/// <remarks>
/// Lazily allocated - reading this on an element with no declared resources never allocates.
/// See <see cref="ResourceDictionary"/>'s remarks for how lookup walks the element tree.
/// </remarks>
public ResourceDictionary Resources => resources ??= [];
```

- [ ] **Step 5: Run tests to verify they pass, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~ResourceDictionaryTests"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/ResourceDictionary.cs sources/IcyUI/UI/UIElement.cs sources/IcyUI.Tests/UI/ResourceDictionaryTests.cs
git commit -m "Add ResourceDictionary and UIElement.Resources"
```

---

## Task 5: `{StaticResource}` markup extension

**Files:**
- Create: `sources/IcyUI/Markup/Extensions/StaticResourceExtension.cs`
- Modify: `sources/IcyUI/Markup/MarkupConfiguration.cs` (extension dictionary — seeded inline there with `"Binding"`)
- Modify: `sources/IcyUI/Markup/MarkupExtensionContext.cs`, `sources/IcyUI/Markup/MarkupLoader.cs`, `sources/IcyUI/UI/UIElement.cs`
- Test: `sources/IcyUI.Tests/Markup/StaticResourceExtensionTests.cs` (new)

**Interfaces:**
- Consumes: `ResourceDictionary.TryGetValue` (Task 4).
- Produces: `StaticResourceExtension`, registered under the name `"StaticResource"`; `MarkupLoadContext.ElementStack` (new); `MarkupExtensionContext.ElementStack` (new).

**Design note resolved during SDD pre-flight (this is not what an earlier draft of this task assumed — read this before implementing):** `{StaticResource}` cannot walk `UIElement.Parent` at all. Markup builds bottom-up — `MarkupLoader.ApplyContentChildren`'s `list.Add(CreateObject(child, context))` fully constructs a child (running all of *its own* attribute/child resolution, where any `{StaticResource}` inside it would resolve) *before* adding it to the parent's collection, which is the step that actually sets `child.Parent`. So during any child's own construction, `Parent` is always `null` — walking it would only ever see the single element `{StaticResource}` was written directly on, never a true ancestor, silently defeating the entire point of hierarchical resource dictionaries.

The fix: walk the *loader's own construction-time nesting stack* instead of the runtime `Parent` link — it already mirrors document nesting exactly, and unlike `Parent` it's fully populated (innermost-to-outermost) at the exact moment any attribute or child text on the innermost element is being resolved.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Markup/StaticResourceExtensionTests.cs
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class StaticResourceExtensionTests
    {
        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<Style>();
            return configuration;
        }

        [Fact]
        public void ResolvesFromTheDeclaringElementsOwnResources()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            // <Border.Resources> is processed before <Border.Style> - both are property elements handled in
            // document order within the same ApplyChildren call, so this works even though Style is an ATTRIBUTE
            // -eligible property: written as a property element here specifically so Resources is populated first
            // (an attribute-form Style="..." would be resolved during ApplyAttributes, which runs before ANY
            // property elements - including this element's own <Border.Resources> - so it could never see it).
            var border = (Border)loader.Load(
                """
                <Border>
                  <Border.Resources>
                    <Style x:Key="Accent" TargetType="Border" Width="42"/>
                  </Border.Resources>
                  <Border.Style>{StaticResource Accent}</Border.Style>
                </Border>
                """);

            Assert.NotNull(border.Style);
            Assert.Equal(42f, border.Style!.Setters["Width"]);
        }

        [Fact]
        public void ResolvesFromAnAncestorsResourcesWhenNotFoundLocally()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var root = (StackPanel)loader.Load(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <Style x:Key="Accent" TargetType="Border" Width="42"/>
                  </StackPanel.Resources>
                  <Border>
                    <Border.Style>{StaticResource Accent}</Border.Style>
                  </Border>
                </StackPanel>
                """);

            var child = (Border)root.Children[0];
            Assert.NotNull(child.Style);
            Assert.Equal(42f, child.Style!.Setters["Width"]);
        }

        [Fact]
        public void UnresolvableKey_ThrowsMarkupExceptionAtLoad()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.Load("""<Border Background="{StaticResource Nope}"/>"""));
            Assert.Contains("Nope", ex.Description);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~StaticResourceExtensionTests"`
Expected: FAIL — `{StaticResource ...}` isn't a registered extension yet (`Unknown markup extension '{StaticResource}'`).

- [ ] **Step 3: Thread a construction-time element stack**

In `MarkupLoader.cs`'s `MarkupLoadContext`, add:

```csharp
/// <summary>
/// Gets the <see cref="UIElement"/>s currently under construction, innermost last - what
/// <c>{StaticResource}</c> (see <see cref="Markup.Extensions.StaticResourceExtension"/>) walks in reverse. Must
/// be a construction-time stack, not <see cref="UIElement.Parent"/>: markup builds bottom-up (a child's own
/// attributes/children fully resolve, via <see cref="CreateObject"/>, before it's added to any parent's
/// collection - the step that actually sets <see cref="UIElement.Parent"/>), so <c>Parent</c> is always
/// <see langword="null"/> for the entire duration of an element's own construction.
/// </summary>
public List<UIElement> ElementStack { get; } = [];
```

In `CreateObject`, push/pop alongside the `SetterTargetType` save/restore added in Task 3 (read the current body of `CreateObject` first — this extends it, not replaces Task 3's addition):

```csharp
bool pushedElement = instance is UIElement;
if (instance is UIElement element)
    context.ElementStack.Add(element);

try
{
    ApplyAttributes(element_or_instance, instance, context, consumedByConstructor); // keep whatever parameter Task 1 already established here
    ApplyChildren(element_or_instance, instance, context);
}
finally
{
    context.SetterTargetType = previousSetterTargetType; // from Task 3 - keep it
    if (pushedElement)
        context.ElementStack.RemoveAt(context.ElementStack.Count - 1);
}
```

- [ ] **Step 4: Give `MarkupExtensionContext` access to the stack**

`MarkupExtensionContext` currently takes `(target, member, configuration, names, node, sourcePath)`. Add an `elementStack` parameter:

```csharp
public sealed class MarkupExtensionContext(
    object target,
    MarkupMember member,
    IcyConfiguration configuration,
    MarkupNameScope names,
    IReadOnlyList<UIElement> elementStack,
    IXmlLineInfo? node,
    string? sourcePath)
{
    // ... existing members unchanged ...

    /// <summary>
    /// Gets the <see cref="UIElement"/>s currently under construction, innermost last - see
    /// <see cref="Markup.MarkupLoader"/>'s private <c>MarkupLoadContext.ElementStack</c> for why this can't
    /// simply be <see cref="UIElement.Parent"/>.
    /// </summary>
    public IReadOnlyList<UIElement> ElementStack { get; } = elementStack;
}
```

Update its one construction site in `MarkupLoader.ResolveExtension`:

```csharp
var extensionContext = new MarkupExtensionContext(instance, member, configuration, context.Names, context.ElementStack, node, context.SourcePath);
```

(Passing `context.ElementStack` directly, not a copy, is intentional and safe: `ResolveExtension` runs synchronously inside `ConvertValue`, itself called synchronously from `ApplyAttributes`/`ApplyChildren` while the relevant elements are still on the stack — the extension's `ProvideValue` reads it before returning, and nothing mutates it concurrently.)

- [ ] **Step 5: Implement `StaticResourceExtension`**

```csharp
// sources/IcyUI/Markup/Extensions/StaticResourceExtension.cs
using Icy.UI;

namespace Icy.Markup.Extensions
{
    /// <summary>
    /// <c>{StaticResource Key}</c>: resolves a named resource by walking outward from the innermost element
    /// currently under construction (see <see cref="MarkupExtensionContext.ElementStack"/>) to the document root,
    /// checking each element's own <see cref="UIElement.Resources"/> in turn.
    /// </summary>
    /// <remarks>
    /// Resolved once, at load time - not re-evaluated if the resource dictionary changes afterward (there is no
    /// mechanism for that yet; this matches how <see cref="ContentPropertyAttribute"/> content and every other
    /// non-<c>{Binding}</c> value in markup is a one-time assignment).
    /// </remarks>
    [MarkupExtensionDefaultProperty(nameof(Key))]
    public sealed class StaticResourceExtension : IMarkupExtension
    {
        /// <summary>
        /// Gets or sets the key to look up.
        /// </summary>
        public string Key { get; set; } = "";

        /// <inheritdoc/>
        /// <exception cref="MarkupException">
        /// No ancestor's <see cref="UIElement.Resources"/> (own entries or merged dictionaries) has an entry for
        /// <see cref="Key"/>.
        /// </exception>
        public object? ProvideValue(MarkupExtensionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            for (int i = context.ElementStack.Count - 1; i >= 0; i--)
            {
                UIElement element = context.ElementStack[i];
                if (element.HasResources && element.Resources.TryGetValue(Key, out object? value))
                    return value;
            }

            throw MarkupException.At($"No resource named '{Key}' was found.", context.Node, context.SourcePath);
        }
    }
}
```

This uses `element.HasResources` to avoid allocating an empty `Resources` dictionary on every ancestor during lookup — add that alongside `Resources` in `UIElement.cs`:

```csharp
/// <summary>
/// Gets a value indicating whether <see cref="Resources"/> has been allocated - reading it never allocates on
/// its own, unlike reading <see cref="Resources"/> itself.
/// </summary>
public bool HasResources => resources != null;
```

- [ ] **Step 6: Register `"StaticResource"` in `MarkupConfiguration`**

```csharp
// sources/IcyUI/Markup/MarkupConfiguration.cs
private readonly Dictionary<string, Type> extensions = new(StringComparer.Ordinal)
{
    ["Binding"] = typeof(BindingExtension),
    ["StaticResource"] = typeof(Extensions.StaticResourceExtension),
};
```

- [ ] **Step 7: Run tests to verify they pass, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~StaticResourceExtensionTests"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 8: Commit**

```bash
git add sources/IcyUI/Markup/Extensions/StaticResourceExtension.cs sources/IcyUI/Markup/MarkupExtensionContext.cs sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI/Markup/MarkupConfiguration.cs sources/IcyUI/UI/UIElement.cs sources/IcyUI.Tests/Markup/StaticResourceExtensionTests.cs
git commit -m "Add the {StaticResource} markup extension"
```

---

## Task 6: `ResourceDictionary.MergedDictionaries` loaded via `Source=`

**Files:**
- Modify: `sources/IcyUI/UI/ResourceDictionary.cs` (add `Source`-driven population support point — see below)
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs` (special-case `<ResourceDictionary Source="...">` the same way any other element is constructed, but loading a *dictionary* file rather than a `UIElement` one)
- Create: an `IAssetImporter<ResourceDictionary>` registration alongside the existing `MarkupImporter` wiring (check `sources/IcyUI/Markup/MarkupImporter.cs` for the `UIElement` pattern and mirror it)
- Test: `sources/IcyUI.Tests/Markup/ResourceDictionaryMergingTests.cs` (new)

**Interfaces:**
- Consumes: `IAssetContext`/`AssetResolver.LoadAsset<T>` (existing, used by `NavigationService`).
- Produces: a document whose root is `<ResourceDictionary>` loads as a `ResourceDictionary`, the same way a document whose root is `<Page>` loads as a `Page`; `<ResourceDictionary Source="Themes/Default.xml"/>` inside `<X.MergedDictionaries>` loads eagerly through that same path.

`MarkupLoader.Load<T>` already supports "the document's root must be a `T`" for `UIElement`-derived roots. `ResourceDictionary` isn't a `UIElement`, so `Load`'s hard requirement (`if (instance is not UIElement element) throw ...`) needs loosening. Read `MarkupLoader.Load(TextReader, string?)` again before changing it — the constraint exists because *most* documents are meant to be UI trees; relax it to "any object", and keep the existing `Load<T>(string, ...)` generic constrained to `UIElement` as-is (nothing currently calls it for a non-`UIElement`, and this task adds a new, separately-constrained overload rather than loosening that one, to avoid touching `NavigationService`/`Page` call sites).

- [ ] **Step 1: Write the failing test**

```csharp
// sources/IcyUI.Tests/Markup/ResourceDictionaryMergingTests.cs
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Markup
{
    public class ResourceDictionaryMergingTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void LoadingADocumentWhoseRootIsAResourceDictionary_ReturnsIt()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            object loaded = loader.LoadObject("""<ResourceDictionary><SolidColorBrush x:Key="Accent">Blue</SolidColorBrush></ResourceDictionary>""");

            var dictionary = Assert.IsType<ResourceDictionary>(loaded);
            Assert.True(dictionary.ContainsKey("Accent"));
        }
    }
}
```

(Name the new non-`UIElement`-constrained entry point `LoadObject` to avoid an ambiguous overload against the existing `Load(TextReader, ...)`/`Load(string, ...)` pair that both return `UIElement` — check for naming collisions against existing members before finalizing.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~ResourceDictionaryMergingTests"`
Expected: FAIL — no `LoadObject` member exists, and even bypassing that, `<ResourceDictionary>` isn't a registered short name/built-in type yet (fixed in Task 13) and `Load` would reject a non-`UIElement` root anyway.

- [ ] **Step 3: Add `MarkupLoader.LoadObject`, sharing logic with `Load` via a private core**

```csharp
/// <summary>
/// Loads a markup document whose root need not be a <see cref="UIElement"/> - a <see cref="UI.ResourceDictionary"/>
/// document, for instance.
/// </summary>
/// <param name="text">The markup document.</param>
/// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
/// <returns>The root object the document declares.</returns>
/// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
public object LoadObject(string text, string? sourcePath = null)
{
    ArgumentNullException.ThrowIfNull(text);
    using var reader = new StringReader(text);
    return LoadObject(reader, sourcePath);
}

/// <summary>
/// Loads a markup document from a stream whose root need not be a <see cref="UIElement"/>.
/// </summary>
/// <param name="stream">The stream to read the document from.</param>
/// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
/// <returns>The root object the document declares.</returns>
/// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
public object LoadObject(Stream stream, string? sourcePath = null)
{
    ArgumentNullException.ThrowIfNull(stream);
    using var reader = new StreamReader(stream, leaveOpen: true);
    return LoadObject(reader, sourcePath);
}

/// <summary>
/// Loads a markup document from an already-parsed <see cref="TextReader"/> whose root need not be a
/// <see cref="UIElement"/>.
/// </summary>
/// <param name="reader">The reader positioned at the start of the document.</param>
/// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
/// <returns>The root object the document declares.</returns>
/// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
public object LoadObject(TextReader reader, string? sourcePath = null) => LoadCore(reader, sourcePath, out _);
```

Extract the existing `public UIElement Load(TextReader reader, ...)` body into a shared private core that also returns the parsed root element (needed for the "must be a UIElement" error's file position), then have both `Load` and `LoadObject` call it:

```csharp
private object LoadCore(TextReader reader, string? sourcePath, out XElement root)
{
    ArgumentNullException.ThrowIfNull(reader);

    XDocument document = ParseDocument(reader, sourcePath);
    root = document.Root ?? throw new MarkupException("The document is empty.", sourcePath);

    var context = new MarkupLoadContext(sourcePath, new MarkupNameScope());

    // Construct the whole tree against the configuration's registry, so every element captures the same one
    // the loader resolves properties through.
    using (PropertyRegistry.UseScope(registry))
    {
        object instance = CreateObject(root, context);
        if (instance is UIElement element)
            MarkupNameScope.SetScope(element, context.Names);
        return instance;
    }
}

public UIElement Load(TextReader reader, string? sourcePath = null)
{
    object instance = LoadCore(reader, sourcePath, out XElement root);
    return instance as UIElement
        ?? throw MarkupException.At($"The root element must be a '{nameof(UIElement)}', but '{instance.GetType().Name}' isn't one.", root, sourcePath);
}
```

(This is a refactor of the existing method — move its doc comment/remarks onto `LoadCore` and/or `Load` as appropriate rather than dropping them; check the current file for the exact text before editing.)

- [ ] **Step 4: Register `ResourceDictionary` as a built-in short name and load `Source=`**

Add `ResourceDictionary` to `MarkupConfiguration.BuiltInNamespaces`'s search (it's already `["Icy.UI", "Icy.UI.Controls"]` and `ResourceDictionary` lives in `Icy.UI`, so no change needed there — it resolves automatically once the type exists).

For `<ResourceDictionary Source="Themes/Default.xml"/>` entries inside `<X.MergedDictionaries>`: this needs a reserved attribute on `ResourceDictionary` similar to `x:Class`, resolved and *replaced* by the loaded dictionary rather than set as an ordinary property (a `ResourceDictionary` has no `Source` property — it's a load-time instruction, not runtime state, matching `x:Class`'s "consumed before the instance existed" framing). Handle it as a directive-like check inside `ApplyAttributes`/`CreateObject`: after constructing a `ResourceDictionary` instance, if the element carries a (non-namespaced) `Source` attribute, load and merge instead of continuing normal attribute/child processing for that element:

```csharp
// In CreateObject, right after constructing `instance`:
if (instance is ResourceDictionary && element.Attribute("Source") is { } source)
{
    return LoadMergedDictionary(source.Value, element, context);
}
```

```csharp
private ResourceDictionary LoadMergedDictionary(string path, XElement element, MarkupLoadContext context)
{
    IAssetContext assetContext = configuration.Assets.AssetResolver is null
        ? throw MarkupException.At("No asset resolver is configured to load a merged ResourceDictionary Source.", element, context.SourcePath)
        : AssetContext.ApplicationContext; // adjust to whatever IAssetContext this loader's configuration actually exposes - see Task 6 Step 4 note below

    if (!assetContext.IsAvailable(path))
        throw MarkupException.At($"No resource dictionary found at '{path}'.", element, context.SourcePath);

    return configuration.Assets.AssetResolver.LoadAsset<ResourceDictionary>(assetContext, path);
}
```

Before finalizing this step, read `sources/IcyUI/Navigation/NavigationService.cs`'s `Navigate(string)` method in full (already partially seen) to copy its *exact* `IAssetContext`/`configuration.Assets` access pattern verbatim — the sketch above is illustrative, not final; `MarkupLoader`'s constructor only captures `configuration.Types.*` today, so it will need `configuration` itself (or specifically `configuration.Assets`) added as a captured field to reach the asset pipeline. Also register a `ResourceDictionaryImporter : IAssetImporter<ResourceDictionary>` mirroring whatever `MarkupImporter`'s existing `IAssetImporter<UIElement>` registration looks like (`AddMarkupSupport`), so `LoadAsset<ResourceDictionary>` has an importer to dispatch to — extend `AddMarkupSupport` (or add a sibling registration call) to also register a `ResourceDictionary` importer that calls `MarkupLoader.LoadObject` and casts.

- [ ] **Step 5: Write the merged-`Source` test, verify it fails then passes**

```csharp
[Fact]
public void MergedDictionarySource_LoadsThroughTheAssetPipeline()
{
    // Uses a fixture file the same way NavigationServiceTests does for Page assets - add
    // sources/IcyUI.Tests/Resources/theme.xml with a single keyed Style or brush entry, and an
    // IAssetContext pointed at the test Resources folder (copy NavigationServiceTests' setup).
}
```
Fill this in by directly copying `NavigationServiceTests`' asset-context fixture setup (`sources/IcyUI.Tests/Navigation/NavigationServiceTests.cs` and its `sources/IcyUI.Tests/Resources/*.xml` fixtures) — mirror that pattern exactly for a new `sources/IcyUI.Tests/Resources/theme.xml` fixture.

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~ResourceDictionaryMergingTests"` (fails, then passes after implementation)
Run: `dotnet test sources/IcyUI.Tests` (full suite green)

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI/UI/ResourceDictionary.cs sources/IcyUI.Tests/Markup/ResourceDictionaryMergingTests.cs sources/IcyUI.Tests/Resources/theme.xml
git commit -m "Load ResourceDictionary.MergedDictionaries through the asset pipeline"
```

---

## Task 7: Implicit (keyless) styles

**Files:**
- Create: `sources/IcyUI/Markup/IImplicitResourceKey.cs`
- Modify: `sources/IcyUI/UI/Styles/Style.cs`, `sources/IcyUI/UI/ResourceDictionary.cs`, `sources/IcyUI/Markup/MarkupLoader.cs`, `sources/IcyUI/UI/UIElement.cs`
- Test: `sources/IcyUI.Tests/UI/ImplicitStyleTests.cs` (new)

**Interfaces:**
- Produces: `IImplicitResourceKey.ImplicitResourceKey { get; }`, `ResourceDictionary.GetImplicitStyleKey(Type)` (a static helper both `Style` and the attach-time lookup call), `UIElement`'s attach-time implicit-style application.

A keyless `<Style TargetType="Button">` inside a `<X.Resources>` dictionary registers under a reserved key derived from its `TargetType`. When any `Button` attaches to a canvas (`OnAttached`) with no local `Style` already set, it looks that key up via the exact same ancestor-walk `{StaticResource}` uses, and — if found — assigns it as its `Style`. **Documented behavior**: this means "no style" and "never explicitly set" are the same state (`Style == null`); an element that wants to opt out of an ambient implicit style needs its own no-op `Style`, not `Style = null`. Flag this to Ivan as worth a one-line mention in the M4 docs page once written — it's a real, if minor, semantic wrinkle the spec didn't call out.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/UI/ImplicitStyleTests.cs
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI
{
    public class ImplicitStyleTests
    {
        [Fact]
        public void ExactTypeMatch_AppliesAutomaticallyOnAttach()
        {
            var root = new StackPanel();
            var style = new Style(typeof(Button));
            style.Setters["Width"] = 99f;
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = style;

            var button = new Button();
            root.Children.Add(button);
            var canvas = new Canvas(Icy.Tests.Rendering.FakeRenderContext.Create(), new Icy.Tests.Input.FakeInputSystem());
            canvas.Add(root);

            Assert.Equal(99f, button.Width);
        }

        [Fact]
        public void SubclassDoesNotMatchABaseTypeImplicitStyle()
        {
            var root = new StackPanel();
            var style = new Style(typeof(Button));
            style.Setters["Width"] = 99f;
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = style;

            var toggle = new ToggleButton();
            root.Children.Add(toggle);
            var canvas = new Canvas(Icy.Tests.Rendering.FakeRenderContext.Create(), new Icy.Tests.Input.FakeInputSystem());
            canvas.Add(root);

            Assert.True(float.IsNaN(toggle.Width));
        }

        [Fact]
        public void ExplicitLocalStyle_IsNotOverriddenByAnImplicitOne()
        {
            var root = new StackPanel();
            var implicitStyle = new Style(typeof(Button));
            implicitStyle.Setters["Width"] = 99f;
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = implicitStyle;

            var explicitStyle = new Style(typeof(Button));
            explicitStyle.Setters["Width"] = 10f;
            var button = new Button { Style = explicitStyle };
            root.Children.Add(button);
            var canvas = new Canvas(Icy.Tests.Rendering.FakeRenderContext.Create(), new Icy.Tests.Input.FakeInputSystem());
            canvas.Add(root);

            Assert.Equal(10f, button.Width);
        }
    }
}
```

Check `Canvas`'s actual constructor signature and any existing `FakeRenderContext.Create()`-style helper before finalizing — copy the construction pattern used by an existing attach-focused test (e.g. `CanvasAttachmentTests.cs`) verbatim rather than guessing the constructor shape.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~ImplicitStyleTests"`
Expected: FAIL — `ResourceDictionary.GetImplicitStyleKey` doesn't exist; even once compiling, nothing applies an implicit style on attach yet.

- [ ] **Step 3: Add the key scheme**

```csharp
// sources/IcyUI/Markup/IImplicitResourceKey.cs
namespace Icy.Markup
{
    /// <summary>
    /// Lets an object populated into a keyless dictionary entry (no <c>x:Key</c>) supply its own resource key,
    /// instead of the loader requiring one.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="UI.Styles.Style"/> so a keyless <c>&lt;Style TargetType="Button"&gt;</c>
    /// registers as an implicit, type-targeted style (see <see cref="UI.ResourceDictionary.GetImplicitStyleKey"/>)
    /// instead of requiring an explicit <c>x:Key</c>.
    /// </remarks>
    public interface IImplicitResourceKey
    {
        /// <summary>
        /// Gets the key this object should register under when added to a dictionary with no explicit
        /// <c>x:Key</c>, or <see langword="null"/> if it has none.
        /// </summary>
        string? ImplicitResourceKey { get; }
    }
}
```

In `ResourceDictionary.cs`, add:

```csharp
/// <summary>
/// The reserved key prefix an implicit (keyless, type-targeted) <see cref="Styles.Style"/> registers under.
/// </summary>
private const string ImplicitStyleKeyPrefix = "#implicit-style:";

/// <summary>
/// Gets the reserved resource key an implicit <see cref="Styles.Style"/> targeting <paramref name="targetType"/>
/// registers under.
/// </summary>
/// <param name="targetType">The exact type the implicit style targets.</param>
/// <returns>The reserved key.</returns>
public static string GetImplicitStyleKey(Type targetType) => ImplicitStyleKeyPrefix + targetType.FullName;
```

In `Style.cs`, implement the interface explicitly (keeps it out of `Style`'s public surface):

```csharp
public class Style(Type targetType) : Markup.IImplicitResourceKey
{
    // ... existing members ...

    string? Markup.IImplicitResourceKey.ImplicitResourceKey => ResourceDictionary.GetImplicitStyleKey(TargetType);
}
```

- [ ] **Step 4: Update dictionary population to use it, and check for duplicate keys (spec §5)**

**Ruling (made during SDD pre-flight, recorded in the ledger):** `ResourceDictionary` (Task 4) only implements the *generic* `IDictionary<string, object?>`, not the non-generic `System.Collections.IDictionary` the original `ApplyPropertyElement` branch checked against (`current is IDictionary dictionary` would be `false` for it otherwise, silently falling through to the wrong branch). A repo-wide check confirms `ResourceDictionary` is the *only* dictionary-shaped markup property that will ever exist at this point — nothing else uses this branch today. Switch the branch's type check to the generic interface outright rather than making `ResourceDictionary` also implement the non-generic one:

In `MarkupLoader.ApplyPropertyElement`, replace the existing `if (current is IDictionary dictionary)` branch with:

```csharp
if (current is IDictionary<string, object?> dictionary)
{
    foreach (XElement entry in children)
    {
        object value = CreateObject(entry, context);
        XAttribute? key = entry.Attribute(MarkupNamespaces.DirectivesNamespace + MarkupDirectives.Key);
        string resolvedKey = key?.Value
            ?? (value as Markup.IImplicitResourceKey)?.ImplicitResourceKey
            ?? throw MarkupException.At($"Entries of '{type.Name}.{propertyName}' need an {MarkupDirectives.Qualified(MarkupDirectives.Key)}.", entry, context.SourcePath);

        if (dictionary.ContainsKey(resolvedKey))
            throw MarkupException.At($"Duplicate key '{resolvedKey}' in this dictionary.", entry, context.SourcePath);

        dictionary[resolvedKey] = value;
    }

    return;
}
```

(`IDictionary` in the `using System.Collections;` sense is no longer referenced by this branch at all — remove the now-unnecessary cast/using if nothing else in the file needs the non-generic interface; check first.)

- [ ] **Step 5: Apply an implicit style on attach**

In `UIElement.cs`, extend `OnAttached()`:

```csharp
protected virtual void OnAttached()
{
    if (Style == null && ResolveImplicitStyle() is { } implicitStyle)
        Style = implicitStyle;

    foreach (UIElement child in GetVisualChildren())
    {
        // ... existing cascade unchanged ...
    }
}

/// <summary>
/// Looks up an implicit (keyless, exact-type-targeted) <see cref="Styles.Style"/> for this element's own type,
/// walking <see cref="Parent"/> the same way <c>{StaticResource}</c> does.
/// </summary>
private Style? ResolveImplicitStyle()
{
    string key = ResourceDictionary.GetImplicitStyleKey(GetType());
    for (UIElement? element = this; element != null; element = element.Parent)
    {
        if (element.HasResources && element.Resources.TryGetValue(key, out object? value) && value is Style style)
            return style;
    }

    return null;
}
```

- [ ] **Step 6: Run tests to verify they pass, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~ImplicitStyleTests"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/Markup/IImplicitResourceKey.cs sources/IcyUI/UI/Styles/Style.cs sources/IcyUI/UI/ResourceDictionary.cs sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI/UI/UIElement.cs sources/IcyUI.Tests/UI/ImplicitStyleTests.cs
git commit -m "Add implicit (keyless, exact-type) style application"
```

---

## Task 8: `Style.Apply` `BasedOn` cycle guard

**Files:**
- Modify: `sources/IcyUI/UI/Styles/Style.cs`
- Test: `sources/IcyUI.Tests/UI/Styles/StyleTests.cs` (extend existing file)

**Interfaces:** none new — internal robustness fix flagged explicitly by the spec (§5).

- [ ] **Step 1: Write the failing test**

Add to `sources/IcyUI.Tests/UI/Styles/StyleTests.cs`:

```csharp
[Fact]
public void Apply_BasedOnCycle_ThrowsInsteadOfStackOverflow()
{
    var element = new TestElement();
    var a = new Style(typeof(TestElement));
    var b = new Style(typeof(TestElement));
    a.BasedOn = b;
    b.BasedOn = a;

    Assert.Throws<InvalidOperationException>(() => element.Style = a);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~Apply_BasedOnCycle"`
Expected: FAIL (stack overflow would crash the test host — if it does, note that and proceed directly to the fix; don't try to "prove" a crash under a test runner, just implement the guard and confirm the *fixed* test passes cleanly).

- [ ] **Step 3: Add the guard**

```csharp
// sources/IcyUI/UI/Styles/Style.cs
/// <summary>
/// Applies this style's setters and state groups to <paramref name="control"/>.
/// </summary>
/// <param name="control">The element to apply this style to.</param>
/// <exception cref="InvalidOperationException">This style's <see cref="BasedOn"/> chain contains a cycle.</exception>
public void Apply(UIElement control) => Apply(control, visited: []);

private void Apply(UIElement control, HashSet<Style> visited)
{
    if (!visited.Add(this))
        throw new InvalidOperationException($"'{nameof(BasedOn)}' forms a cycle - a style can't (directly or indirectly) be based on itself.");

    BasedOn?.Apply(control, visited);

    IPropertyStore store = control.GetPropertyStore();
    foreach (KeyValuePair<string, object?> setter in Setters)
    {
        if (store.TryGetProperty(setter.Key, out IPropertyReference? property))
        {
            property.SetTierValue(control, PropertyValuePrecedence.Style, setter.Value);
        }
    }

    foreach (VisualStateGroup group in StateGroups)
    {
        control.RegisterStateGroup(group);
    }
}
```

- [ ] **Step 4: Run test to verify it passes, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~Apply_BasedOnCycle"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Styles/Style.cs sources/IcyUI.Tests/UI/Styles/StyleTests.cs
git commit -m "Guard Style.Apply against a BasedOn cycle"
```

---

## Task 9: `VisualState.Duration`/`Easing`, `<Style.StateGroups>`, and markup registration for Style/VisualStateGroup/VisualState

**Files:**
- Modify: `sources/IcyUI/UI/Styles/VisualState.cs`, `sources/IcyUI/UI/Styles/Style.cs`, `sources/IcyUI/UI/Styles/VisualStateGroup.cs` doesn't exist as its own file (it's in `VisualState.cs`) — add `[ContentProperty]` there.
- Modify: `sources/IcyUI/Markup/MarkupConfiguration.cs` if `Icy.UI.Styles` needs adding to `BuiltInNamespaces` (check first — it's a different namespace than `Icy.UI`/`Icy.UI.Controls`, so it does).
- Test: `sources/IcyUI.Tests/Markup/StyleMarkupTests.cs` (new)

**Interfaces:**
- Produces: `VisualState.Duration { get; set; } : TimeSpan?`, `VisualState.Easing { get; set; } : EasingFunction?` (new plain settable properties — resolved automatically by `MarkupMember.Resolve`'s reflection fallback, no loader changes needed). `[ContentProperty(nameof(Style.StateGroups))]`... actually `Style`'s content should stay reserved for a future use / has no natural single content property per the spec (setters are attributes, state groups are the `<Style.StateGroups>` *property element*, not content) — do **not** add `[ContentProperty]` to `Style`. `[ContentProperty(nameof(VisualStateGroup.States))]` on `VisualStateGroup` (so `<VisualStateGroup Name="...">`'s children are its states without needing `<VisualStateGroup.States>`).

- [ ] **Step 1: Write the failing test**

```csharp
// sources/IcyUI.Tests/Markup/StyleMarkupTests.cs
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class StyleMarkupTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void FullStyleWithStateGroups_ParsesToTheEquivalentHandBuiltStyle()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var style = (Style)loader.LoadObject(
                """
                <Style TargetType="Button" Background="Blue">
                  <Style.StateGroups>
                    <VisualStateGroup Name="CommonStates">
                      <VisualState Name="MouseOver" State="Hovered" Duration="0:0:0.15" Easing="CubicOut" Background="LightBlue"/>
                    </VisualStateGroup>
                  </Style.StateGroups>
                </Style>
                """);

            Assert.Equal(typeof(Button), style.TargetType);
            Assert.Equal("Blue", style.Setters["Background"]?.ToString());
            VisualStateGroup group = Assert.Single(style.StateGroups);
            Assert.Equal("CommonStates", group.Name);
            VisualState state = Assert.Single(group.States);
            Assert.Equal(ControlState.Hovered, state.State);
            Assert.Equal(TimeSpan.FromMilliseconds(150), state.Duration);
            Assert.NotNull(state.Easing);
            Assert.Equal("LightBlue", state.Setters["Background"]?.ToString());
        }
    }
}
```

Adjust the `Background` assertions once `BrushTypeConverter`'s actual conversion target/`ToString()` behavior is confirmed (Task 5's note applies here too) — the point of the test is structural equivalence to the hand-built tree, matching `MarkupEquivalenceTests`' own convention; use `Assert.IsType<...>`/direct brush-property comparisons instead of `.ToString()` if that's how the existing brush tests assert.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~StyleMarkupTests"`
Expected: FAIL — `<VisualStateGroup>`/`<VisualState>` aren't resolvable types yet (`Icy.UI.Styles` isn't in `BuiltInNamespaces`), `VisualState` has no `Duration`/`Easing`, and `<Style.StateGroups>` needs `Style.StateGroups` to already be populable (it is — `List<VisualStateGroup>`, `Add`-able — Task 2 covers the mechanism, no `Style`-specific work needed there).

- [ ] **Step 3: Add `Duration`/`Easing` to `VisualState`**

```csharp
// sources/IcyUI/UI/Styles/VisualState.cs
public class VisualState(string name, ControlState state) : Markup.IImplicitResourceKey
{
    // ... existing members ...

    /// <summary>
    /// Gets or sets how long entering this state takes to animate each setter from the element's current
    /// effective value to its target, or <see langword="null"/> to snap instantly (the default).
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the easing function used for the transition when <see cref="Duration"/> is set. Ignored when
    /// <see cref="Duration"/> is <see langword="null"/>.
    /// </summary>
    public Animations.EasingFunction? Easing { get; set; }
}
```

(`VisualState` doesn't need `IImplicitResourceKey` - remove that if added by mistake; only `Style` needs it. Skip this interface on `VisualState`.)

- [ ] **Step 4: Add `[ContentProperty]` to `VisualStateGroup`**

```csharp
// sources/IcyUI/UI/Styles/VisualState.cs, on the VisualStateGroup class
[Markup.ContentProperty(nameof(States))]
public class VisualStateGroup(string name)
```

- [ ] **Step 5: Register the `Icy.UI.Styles` namespace as built-in**

```csharp
// sources/IcyUI/Markup/MarkupConfiguration.cs
public IList<string> BuiltInNamespaces { get; } = ["Icy.UI", "Icy.UI.Controls", "Icy.UI.Styles"];
```

Also update `MarkupConfiguration.FindBuiltIn`'s doc comment/remarks if they enumerate the namespace list literally (check first).

- [ ] **Step 6: Run test to verify it passes, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~StyleMarkupTests"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Styles/VisualState.cs sources/IcyUI/Markup/MarkupConfiguration.cs sources/IcyUI.Tests/Markup/StyleMarkupTests.cs
git commit -m "Add VisualState.Duration/Easing and register Icy.UI.Styles for markup"
```

---

## Task 10: `<Setter>` element fallback

**Files:**
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs`
- Test: extend `sources/IcyUI.Tests/Markup/StyleMarkupTests.cs`

**Interfaces:** none new public — `<Setter Property="X" Value="Y"/>` is a markup-loader convention, not a new runtime type (per spec §Setter syntax: "No new Setter runtime type").

`<Setter>` needs to be recognized as a *child element* of `<Style>`/`<VisualState>`/`<Style.StateGroups>`'s state entries specifically when a plain attribute can't express the value (a markup extension, or a name colliding with a reserved attribute). Since `Style`/`VisualState` have no content property, `ApplyChildren`'s existing `ApplyContentChildren` path (which requires `[ContentProperty]`) doesn't apply — `<Setter>` needs its own recognition, similar to how `IsPropertyElement` recognizes `Grid.ColumnDefinitions`-style dotted names, but keyed on the *element's own type* (`Setter`) rather than a dotted property name.

- [ ] **Step 1: Write the failing test**

Add to `StyleMarkupTests.cs`:

```csharp
[Fact]
public void SetterElement_IsAFallbackForAttributeSetters()
{
    var loader = new MarkupLoader(CreateConfiguration());

    var style = (Style)loader.LoadObject(
        """
        <Style TargetType="Button">
          <Setter Property="Width" Value="42"/>
        </Style>
        """);

    Assert.Equal(42f, style.Setters["Width"]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~SetterElement"`
Expected: FAIL — `<Setter>` isn't recognized; `Style` has no `[ContentProperty]`, so `ApplyChildren` throws "has no content property, so it can't have children or text."

- [ ] **Step 3: Recognize `<Setter>` in `ApplyChildren`**

In `MarkupLoader.ApplyChildren`, before falling through to the content-property path, check for `<Setter>` children when the parent type carries `[MarkupSetterCollection]`:

```csharp
private void ApplyChildren(XElement element, object instance, MarkupLoadContext context)
{
    List<XElement> content = [];
    foreach (XElement child in element.Elements())
    {
        if (IsSetterElement(child, instance))
        {
            ApplySetterElement(instance, child, context);
        }
        else if (IsPropertyElement(child, instance.GetType(), context, out string? propertyName))
        {
            ApplyPropertyElement(instance, propertyName, child, context);
        }
        else
        {
            content.Add(child);
        }
    }

    // ... existing content/text handling unchanged ...
}

/// <summary>
/// Recognizes a <c>&lt;Setter&gt;</c> child on an object marked <see cref="MarkupSetterCollectionAttribute"/> -
/// a markup-only convention with no runtime <c>Setter</c> type (see the type's remarks).
/// </summary>
private static bool IsSetterElement(XElement child, object instance) =>
    child.Name.LocalName == "Setter" && MarkupSetterCollectionAttribute.GetSetterCollectionName(instance.GetType()) != null;

private void ApplySetterElement(object instance, XElement setterElement, MarkupLoadContext context)
{
    XAttribute property = setterElement.Attribute("Property")
        ?? throw MarkupException.At("A <Setter> needs a 'Property' attribute.", setterElement, context.SourcePath);
    XAttribute value = setterElement.Attribute("Value")
        ?? throw MarkupException.At("A <Setter> needs a 'Value' attribute.", setterElement, context.SourcePath);

    // Reuses the exact same resolution TryApplyAsSetterOverflow uses for an attribute-form setter.
    if (!TryApplyAsSetterOverflow(instance, property.Value, value.Value, value, context))
    {
        throw MarkupException.At($"'{instance.GetType().Name}' doesn't support <Setter> elements.", setterElement, context.SourcePath);
    }
}
```

Note `TryApplyAsSetterOverflow` takes `(object instance, string name, string value, XAttribute attribute, MarkupLoadContext context)` from Task 3 — reusing it here means its `attribute` parameter (used only for error line-info) receives the `Value` attribute rather than a `Property`-named one; that's fine, it's purely for error positions.

- [ ] **Step 4: Run test to verify it passes, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~SetterElement"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI.Tests/Markup/StyleMarkupTests.cs
git commit -m "Add the <Setter> element fallback for Style/VisualState"
```

---

## Task 11: VisualState transition animation

**Files:**
- Modify: `sources/IcyUI/UI/UIElement.cs`
- Test: `sources/IcyUI.Tests/UI/Styles/VisualStateTransitionTests.cs` (new)

**Interfaces:**
- Consumes: `Animation`/`Timeline.FromTo`/`AnimationExtensions.Animate` (existing), `IPropertyReference.GetRawValue`/`SetTierValue` (existing).
- Produces: `UIElement`'s private `ApplyStateSetters` now animates when `state.Duration` is set; a new private `Dictionary<string, Animation> activeTransitions` field resolves the spec's flagged "exact mechanics of interrupting/replacing an in-flight per-property Animation" open item.

Resolution of that open item: `PropertyValuePrecedence.Animation` (2) already outranks `VisualState` (1) (see `PropertyValuePrecedence.cs`). So: set the VisualState-tier value immediately as today (this is the value that will be showing once any transition finishes), then start an `Animation` from the *previous* effective value to that same target, keyed per property name in a small tracking dictionary so a second transition arriving before the first finishes calls `Stop()` on the old one first (matching `Animation.Stop`'s existing "clear my tier contribution" contract) rather than letting two animations fight over the same property.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/UI/Styles/VisualStateTransitionTests.cs
using Icy.Animations;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI.Styles
{
    public class VisualStateTransitionTests
    {
        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        [Fact]
        public void DurationUnset_SnapsInstantly()
        {
            var element = new TestElement { Width = 5 };
            var group = new VisualStateGroup("Group");
            var hovered = new VisualState("Hovered", ControlState.Hovered);
            hovered.Setters["Width"] = 20f;
            group.States.Add(hovered);
            element.RegisterStateGroup(group);

            element.ControlState = ControlState.Hovered;

            Assert.Equal(20f, element.Width);
        }

        [Fact]
        public void DurationSet_AnimatesFromCurrentValueToTarget()
        {
            var element = new TestElement(); // Width unset -> NaN local, but VisualState tier starts from whatever GetRawValue reports before the transition
            var group = new VisualStateGroup("Group");
            var hovered = new VisualState("Hovered", ControlState.Hovered) { Duration = TimeSpan.FromSeconds(1) };
            hovered.Setters["Width"] = 20f;
            group.States.Add(hovered);
            element.RegisterStateGroup(group);
            element.Width = 0f; // give it a real starting value instead of NaN, which ValueInterpolator can't lerp

            element.ControlState = ControlState.Hovered;

            // Immediately after triggering, the transition has started but not completed - value should have moved
            // partway or still be at the animation's t=0 sample, not already snapped to 20.
            Assert.NotEqual(20f, element.Width);

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(2));

            Assert.Equal(20f, element.Width);
        }

        [Fact]
        public void RetriggeringBeforeCompletion_ReplacesTheInFlightAnimation()
        {
            var element = new TestElement { Width = 0f };
            var group = new VisualStateGroup("Group");
            var hovered = new VisualState("Hovered", ControlState.Hovered) { Duration = TimeSpan.FromSeconds(1) };
            hovered.Setters["Width"] = 20f;
            var normal = new VisualState("Normal", ControlState.Normal) { Duration = TimeSpan.FromSeconds(1) };
            normal.Setters["Width"] = 0f;
            group.States.Add(hovered);
            group.States.Add(normal);
            element.RegisterStateGroup(group);

            element.ControlState = ControlState.Hovered;
            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromMilliseconds(500));
            float midway = element.Width;
            Assert.True(midway is > 0f and < 20f);

            element.ControlState = ControlState.Normal; // re-trigger before the Hovered transition finishes
            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(2));

            Assert.Equal(0f, element.Width); // settles on Normal's target, not stuck fighting the old animation
        }
    }
}
```

Check `Dispatcher.GetCurrentThreadDispatcher()`/`UpdateAnimations` are accessible the way existing `Icy.Animations` tests already call them (grep `sources/IcyUI.Tests` for existing `Animation`/`Dispatcher` tests and copy that exact access pattern — `Animation.Update` is `internal`, so confirm what's actually reachable from the test project via `InternalsVisibleTo` before finalizing this test file).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~VisualStateTransitionTests"`
Expected: `DurationUnset_SnapsInstantly` PASSES already (today's behavior). The other two FAIL — `Duration`/`Easing` are read but never used by `ApplyStateSetters`, so both currently snap instantly regardless of `Duration`.

- [ ] **Step 3: Implement the transition**

In `UIElement.cs`, add the tracking field near `activeStates`:

```csharp
private Dictionary<string, Animations.Animation>? activeStateTransitions;
```

Replace `ApplyStateSetters`:

```csharp
private void ApplyStateSetters(Styles.VisualState? state)
{
    if (state == null)
        return;

    IPropertyStore store = GetPropertyStore();
    foreach (KeyValuePair<string, object?> setter in state.Setters)
    {
        if (!store.TryGetProperty(setter.Key, out IPropertyReference? property))
            continue;

        if (state.Duration is { } duration)
        {
            AnimateStateSetter(property, setter.Key, setter.Value, duration, state.Easing);
        }
        else
        {
            StopStateTransition(setter.Key);
            property.SetTierValue(this, PropertyValuePrecedence.VisualState, setter.Value);
        }
    }
}

/// <summary>
/// Animates <paramref name="property"/> from its current effective value to <paramref name="targetValue"/> over
/// <paramref name="duration"/>, replacing any transition already in flight for the same property name.
/// </summary>
/// <remarks>
/// Sets the true <see cref="PropertyValuePrecedence.VisualState"/>-tier value immediately (it is what will show
/// once the transition ends, and what a later <see cref="RevertStyle"/>/state change reverts against), then
/// plays the visible transition on the strictly-higher <see cref="PropertyValuePrecedence.Animation"/> tier,
/// clearing that tier's contribution once the transition completes so the VisualState-tier value takes over with
/// no visible jump. Resolves the spec's "interrupting/replacing an in-flight transition" open item: at most one
/// transition <see cref="Animations.Animation"/> is ever in flight per property name on a given element.
/// </remarks>
private void AnimateStateSetter(IPropertyReference property, string propertyName, object? targetValue, TimeSpan duration, Animations.EasingFunction? easing)
{
    object? currentValue = property.GetRawValue(this);
    property.SetTierValue(this, PropertyValuePrecedence.VisualState, targetValue);

    StopStateTransition(propertyName);

    var timeline = Animations.Timeline.FromTo(propertyName, duration, currentValue, targetValue, easing);
    Animations.Animation animation = this.Animate(timeline);
    animation.Completed += (_, _) => animation.Stop();

    activeStateTransitions ??= [];
    activeStateTransitions[propertyName] = animation;
}

private void StopStateTransition(string propertyName)
{
    if (activeStateTransitions != null && activeStateTransitions.Remove(propertyName, out Animations.Animation? existing))
        existing.Stop();
}
```

And in `ClearStateSetters` (the method right after `ApplyStateSetters` in the file), also stop any in-flight transition for a property being cleared, so leaving a state mid-transition doesn't leave a stale `Animation` running against a tier nothing wants anymore:

```csharp
private void ClearStateSetters(Styles.VisualState? state)
{
    if (state == null)
        return;
    IPropertyStore store = GetPropertyStore();
    foreach (string propertyName in state.Setters.Keys)
    {
        StopStateTransition(propertyName);
        if (store.TryGetProperty(propertyName, out IPropertyReference? property))
        {
            property.ClearTierValue(this, PropertyValuePrecedence.VisualState);
        }
    }
}
```

(Check the exact existing body of `ClearStateSetters` first — the snippet above assumes it mirrors `ApplyStateSetters`'s loop shape; adjust to match whatever it actually does today rather than assuming.)

`Timeline.FromTo` requires `duration > TimeSpan.Zero` (guarded in its constructor) — a `VisualState.Duration` of exactly `TimeSpan.Zero` would throw. Treat `Duration == TimeSpan.Zero` the same as `null` (instant snap) in `ApplyStateSetters`'s branch condition: `if (state.Duration is { } duration && duration > TimeSpan.Zero)`.

- [ ] **Step 4: Run tests to verify they pass, then run full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~VisualStateTransitionTests"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/UIElement.cs sources/IcyUI.Tests/UI/Styles/VisualStateTransitionTests.cs
git commit -m "Animate VisualState transitions when Duration is set"
```

---

## Task 12: `Timeline` as a markup resource

**Files:**
- Modify: `sources/IcyUI/Data/Markup/PropertyRegistry.cs` (add `TypeConverter`), `sources/IcyUI/Configuration/BuildingExtensions.cs` (keep it in sync with `ReflectionConfiguration.TypeConverter`), `sources/IcyUI/Animations/Animation.cs` (convert keyframe values at construction), `sources/IcyUI/Animations/Timeline.cs` (markup registration attributes), `sources/IcyUI/Animations/AnimationKeyframe.cs` (none — already constructor-arg-bindable via Task 1 as-is), `sources/IcyUI/Markup/MarkupConfiguration.cs` (register `Icy.Animations` as a built-in namespace)
- Test: `sources/IcyUI.Tests/Animations/AnimationKeyframeConversionTests.cs` (new), `sources/IcyUI.Tests/Markup/TimelineMarkupTests.cs` (new)

**Interfaces:**
- Produces: `PropertyRegistry.TypeConverter { get; set; } : ITypeConverter` (new, default `new TypeConversionManager()`), used by `Animation`'s constructor to convert each keyframe's value against the resolved property's `PropertyType` — resolves the spec's other flagged open item ("whether Timeline's shape needs adjustment to serialize cleanly") without changing `Timeline`'s or `AnimationKeyframe`'s public shape at all: markup-authored keyframes arrive as raw strings, hand-authored C# keyframes arrive already correctly typed, and running both through `ITypeConverter.Convert` is a no-op for the latter (`TypeConversionManager` short-circuits when the value is already assignable to the target type).

- [ ] **Step 1: Write the failing conversion test**

```csharp
// sources/IcyUI.Tests/Animations/AnimationKeyframeConversionTests.cs
using Icy.Animations;
using Icy.Data.Markup;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Animations
{
    public class AnimationKeyframeConversionTests
    {
        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        [Fact]
        public void StringKeyframeValues_AreConvertedToThePropertysRealType()
        {
            var element = new TestElement();
            var timeline = new Timeline("Width", TimeSpan.FromSeconds(1));
            timeline.AddKeyframe(0f, "0"); // markup-shaped: a raw string, not a float
            timeline.AddKeyframe(1f, "40");

            var animation = new Animation(element, timeline);
            animation.Start();

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(1));

            Assert.Equal(40f, element.Width); // would still be the string "40" (or throw) without conversion
        }

        [Fact]
        public void AlreadyTypedKeyframeValues_PassThroughUnchanged()
        {
            var element = new TestElement();
            var timeline = Timeline.FromTo("Width", TimeSpan.FromSeconds(1), 0f, 40f);

            var animation = new Animation(element, timeline);
            animation.Start();

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(1));

            Assert.Equal(40f, element.Width);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify the first fails**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~AnimationKeyframeConversionTests"`
Expected: `AlreadyTypedKeyframeValues_PassThroughUnchanged` already PASSES (today's behavior for correctly-typed values). `StringKeyframeValues_AreConvertedToThePropertysRealType` FAILS — `Evaluate`'s `ValueInterpolator.TryLerp("0", "40", ...)` can't lerp strings, so it falls back to holding/snapping the raw string, and `SetTierValue` ends up storing `"40"` (a string) into a `float` property, which either throws inside the property store or leaves `Width` unconverted — either way the assertion fails.

- [ ] **Step 3: Add `PropertyRegistry.TypeConverter`**

```csharp
// sources/IcyUI/Data/Markup/PropertyRegistry.cs
/// <summary>
/// Gets or sets the type converter used to coerce values contributed to this registry's properties from a
/// possibly-mismatched source type (a markup attribute string, most commonly) into each property's real type.
/// </summary>
/// <remarks>
/// Kept alongside <see cref="Default"/>/<see cref="Current"/> rather than reached only through
/// <see cref="Configuration.ReflectionConfiguration.TypeConverter"/>, because <see cref="Icy.Animations.Animation"/>
/// only ever has a target <see langword="object"/> to work from - going through <see cref="For(object)"/> is the
/// same pattern <see cref="Icy.Animations.Animation"/>'s constructor already uses to reach the right registry, so
/// this reaches the right converter the same way, without adding a new ambient service or a new project reference
/// from <c>Icy.Animations</c>.
/// </remarks>
public ITypeConverter TypeConverter { get; set; } = new TypeConversionManager();
```

Keep it in sync with `ReflectionConfiguration.TypeConverter`: in `sources/IcyUI/Configuration/BuildingExtensions.cs`'s `WithTypeConverter`:

```csharp
public static IReflectionConfigurationBuilder WithTypeConverter(this IReflectionConfigurationBuilder builder, ITypeConverter converter)
{
    builder.Types.TypeConverter = converter;
    builder.Types.PropertyRegistry.TypeConverter = converter;
    return builder;
}
```

(Confirm the exact current body/return shape of `WithTypeConverter` before editing — the snippet assumes it returns `builder` at the end, matching the file's other extension methods.)

- [ ] **Step 4: Convert keyframes in `Animation`'s constructor**

```csharp
// sources/IcyUI/Animations/Animation.cs
public class Animation
{
    private readonly IPropertyReference property;
    private readonly IReadOnlyList<AnimationKeyframe> keyframes;
    private TimeSpan elapsed;
    private int completedPasses;
    private bool reversed;

    public Animation(object target, Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(timeline);

        IPropertyStore store = PropertyRegistry.For(target).GetPropertyStore(target.GetType());
        if (!store.TryGetProperty(timeline.TargetProperty, out IPropertyReference? reference))
        {
            ThrowHelper.ThrowArgumentException(
                nameof(timeline),
                $"'{target.GetType()}' has no property named '{timeline.TargetProperty}' registered with {nameof(PropertyRegistry)}.");
        }

        if (reference.Metadata is UIPropertyMetadata { IsAnimationProhibited: true })
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Property '{timeline.TargetProperty}' on '{target.GetType()}' is marked non-animatable and cannot be driven by an {nameof(Animation)}.");
        }

        Target = target;
        Timeline = timeline;
        property = reference;

        // Markup-authored keyframes arrive as raw strings; hand-authored ones are already the right CLR type, in
        // which case this is a no-op (see PropertyRegistry.TypeConverter's remarks). Converted once here, and
        // cached per-Animation-instance rather than mutating the (possibly shared/reused) Timeline resource.
        ITypeConverter converter = PropertyRegistry.For(target).TypeConverter;
        keyframes = [.. timeline.Keyframes.Select(k => new AnimationKeyframe(k.Offset, converter.Convert(k.Value, reference.PropertyType)))];
    }

    // ... IsRunning/IsPaused/Target/Timeline/Start/Pause/Resume/Stop/Update unchanged ...

    private object? Evaluate(float easedTime)
    {
        if (keyframes.Count == 0)
            return null;
        if (keyframes.Count == 1)
            return keyframes[0].Value;

        AnimationKeyframe left = keyframes[0];
        AnimationKeyframe right = keyframes[^1];
        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            if (easedTime >= keyframes[i].Offset && easedTime <= keyframes[i + 1].Offset)
            {
                left = keyframes[i];
                right = keyframes[i + 1];
                break;
            }
        }

        float span = right.Offset - left.Offset;
        float localT = span > 0f ? (easedTime - left.Offset) / span : 1f;

        if (ValueInterpolator.TryLerp(left.Value, right.Value, localT, out object? interpolated))
            return interpolated;

        return localT >= 1f ? right.Value : left.Value;
    }
}
```

`ITypeConverter.Convert(object? value, Type targetType)` needs `using Icy.Data;` added to `Animation.cs` if not already present (check the file's usings first).

- [ ] **Step 5: Run the conversion tests, then the full suite**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~AnimationKeyframeConversionTests"`
Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 6: Register `Icy.Animations` for markup and write the `Timeline`-in-markup test**

```csharp
// sources/IcyUI/Markup/MarkupConfiguration.cs
public IList<string> BuiltInNamespaces { get; } = ["Icy.UI", "Icy.UI.Controls", "Icy.UI.Styles", "Icy.Animations"];
```

`Timeline`'s content property is `Keyframes` — but `Timeline` isn't a `UIElement`, and `[ContentProperty]` today is read by `ResolveContentProperty`/`ContentPropertyAttribute.GetContentPropertyName(type)` against *any* `Type`, not specifically `UIElement`-derived ones (confirm this by re-reading `ContentPropertyAttribute.GetContentPropertyName` — it takes a plain `Type`, so it already works for non-`UIElement`s; `ApplyContentChildren`/`ApplyChildren` themselves are written generically against `object instance`, not `UIElement`, so no further generalization is needed here beyond Task 2's duck-typed `Add`).

```csharp
// sources/IcyUI/Animations/Timeline.cs
[Markup.ContentProperty(nameof(Keyframes))]
public class Timeline
```

```csharp
// sources/IcyUI.Tests/Markup/TimelineMarkupTests.cs
using Icy.Animations;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Xunit;

namespace Icy.Tests.Markup
{
    public class TimelineMarkupTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void TimelineWithKeyframes_ParsesToTheEquivalentHandBuiltTimeline()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var timeline = (Timeline)loader.LoadObject(
                """
                <Timeline TargetProperty="Opacity" Duration="0:0:1" Easing="SineInOut" RepeatCount="-1" AutoReverse="True">
                  <AnimationKeyframe Offset="0" Value="0.5"/>
                  <AnimationKeyframe Offset="1" Value="1.0"/>
                </Timeline>
                """);

            Assert.Equal("Opacity", timeline.TargetProperty);
            Assert.Equal(TimeSpan.FromSeconds(1), timeline.Duration);
            Assert.Equal(Timeline.Forever, timeline.RepeatCount);
            Assert.True(timeline.AutoReverse);
            Assert.Equal(2, timeline.Keyframes.Count);
            Assert.Equal(0f, timeline.Keyframes[0].Offset);
            Assert.Equal("0.5", timeline.Keyframes[0].Value); // still a raw string pre-Animation-construction - see Task 12 design note
        }
    }
}
```

Note the last assertion: `Timeline.Keyframes[0].Value` is `"0.5"` (a string) right after loading, *not* `0.5f` — conversion happens lazily, inside `Animation`'s constructor, exactly once the target's real property type is known (Task 12 Step 4). This is intentional (a `{StaticResource}`-retrieved `Timeline` can be replayed against different targets/property types over its lifetime), and worth calling out plainly if this test's assertion looks surprising during review.

**Ruling (made during SDD pre-flight, recorded in the ledger):** `RepeatCount` is a plain `int` property; a bare `"Forever"` string won't parse via `int.Parse`, and adding a scoped `IValueConverter<string, int>` just for this one property would apply to *every* string→int conversion in the whole configuration - too broad for this milestone. `Timeline.Forever`'s markup spelling is the literal `-1` for v1, not the word `"Forever"` (the test above already uses `RepeatCount="-1"`). Add a one-line note to `Timeline.RepeatCount`'s existing XML doc comment: `/// <c>-1</c> in markup - see <see cref="Forever"/>.` No new conversion infrastructure.

- [ ] **Step 7: Run test to verify it fails, then implement, then verify it passes**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~TimelineMarkupTests"` (fails first: `Icy.Animations` isn't a built-in namespace, `Timeline`/`AnimationKeyframe` aren't constructible without Task 1+ this step's `[ContentProperty]`)

Implement Steps above, then re-run: expected PASS.

Run: `dotnet test sources/IcyUI.Tests` (full suite green)

- [ ] **Step 8: Commit**

```bash
git add sources/IcyUI/Data/Markup/PropertyRegistry.cs sources/IcyUI/Configuration/BuildingExtensions.cs sources/IcyUI/Animations/Animation.cs sources/IcyUI/Animations/Timeline.cs sources/IcyUI/Markup/MarkupConfiguration.cs sources/IcyUI.Tests/Animations/AnimationKeyframeConversionTests.cs sources/IcyUI.Tests/Markup/TimelineMarkupTests.cs
git commit -m "Support Timeline/AnimationKeyframe as markup resources, converting keyframe values against the real target property type"
```

---

## Task 13: Equivalence test — full markup-styled screen vs. hand-built C#

**Files:**
- Modify: `sources/IcyUI.Tests/Markup/MarkupEquivalenceTests.cs`

**Interfaces:** none new — this is the milestone's regression guard, per spec §6 ("An equivalence test: a markup-styled screen matching an equivalent hand-built Style/VisualStateGroup C# tree structurally").

- [ ] **Step 1: Write the test**

Add a new `[Fact]` to `MarkupEquivalenceTests.cs` building a small themed-button screen two ways — via markup (implicit style + a hover `VisualState` with a transition + a `{StaticResource}`-referenced `Timeline` sitting unused as a resource) and via the exact hand-built pattern `StylesDemo.cs` already uses — and asserting structural equivalence the same way the file's existing tests do (reuse whatever `AssertEquivalent`/property-by-property helper already exists in this file rather than inventing a new comparison approach).

- [ ] **Step 2: Run it, fix any last integration gaps it surfaces**

Run: `dotnet test sources/IcyUI.Tests --filter "FullyQualifiedName~MarkupEquivalenceTests"`

This test is likely to surface small integration issues the isolated per-task tests didn't (e.g., two features interacting) — treat any failure here as a real bug in one of Tasks 1–12's implementations, not a reason to weaken this test. Fix forward in the relevant task's file, re-run the *full* suite, and only then proceed.

- [ ] **Step 3: Run the full suite, then commit**

Run: `dotnet test sources/IcyUI.Tests`

```bash
git add sources/IcyUI.Tests/Markup/MarkupEquivalenceTests.cs
git commit -m "Add an equivalence test for a fully markup-styled screen"
```

---

## Task 14: Sample — extend the shared demo with M4 features

**Files:**
- Create: `sources/Shared Samples/MarkupStylesDemo.cs` (mirrors `MarkupDemo.cs`'s pattern: markup loaded from an inline string, per the M1–M3 precedent of avoiding a per-engine bundled asset file)
- Modify: `sources/MonoGame Sample/MonoGame Sample.csproj`, `sources/Stride Sample/Stride Sample.csproj` (add the new file to each engine sample, matching how `NavigationDemo.cs`/`StylesDemo.cs` were wired in)
- Modify: `sources/MonoGame Sample/SampleGame.cs`-equivalent switcher and the Stride sample's sibling-toggle pattern (per the M3.5 note: "Stride has no multi-sample runner... added both demo roots as siblings on the same Canvas, toggling IsVisible via a PageUp/PageDown RegisterCommand/KeyGesture") — follow the *existing* precedent exactly rather than introducing a different wiring approach.

**Interfaces:** none new — this is glue code exercising Tasks 1–13's public surface end-to-end in a real running sample.

- [ ] **Step 1: Read `MarkupDemo.cs` and `StylesDemo.cs` in full** to copy their exact structural conventions (section headers via `CreateSection`, `CreateLabel`, layout via `StackPanel`/`Grid`) before writing new code — do not invent a different visual structure for this demo.

- [ ] **Step 2: Write `MarkupStylesDemo.cs`** with markup exercising, at minimum, one instance of every M4 feature: a `<Panel.Resources>` with a merged dictionary reference (`Source=`, pointing at a second small inline-loaded... note merged dictionaries need a real file per Task 6's `Source=` design, not an inline string — add a small bundled `.xml` resource file for this sample the way `NavigationDemo` chose *not* to, since merging specifically requires a loadable path; check how sample projects bundle content files today (`ControlsDemo`/existing `.csproj` `<None Include>`/`<Content Include>` entries) and follow that pattern), an implicit `<Style TargetType="Button">` re-skinning every button in its scope with no `x:Key`, a `<Style.StateGroups>` hover/press transition with `Duration`/`Easing`, and a `<Timeline x:Key="...">` resource played from a button click via `FindControl`/`Animate`.

- [ ] **Step 3: Wire into both engine samples** following the exact `.csproj`/switcher pattern `NavigationDemo`/`StylesDemo` already established (grep both `.csproj` files and the MonoGame `SamplesRunner`/Stride sibling-toggle code for the precedent before writing).

- [ ] **Step 4: Build both sample projects**

Run: `dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj"`
Run: `dotnet build "sources/Stride Sample/Stride Sample.csproj"`
Expected: both build cleanly, 0 new warnings (matching the "core warnings unchanged at 27" discipline every prior milestone note tracked).

- [ ] **Step 5: Run the full test suite one more time** (the sample doesn't have its own automated tests, but a shared-file compile error would only surface at sample-project build time, not `dotnet test` — Step 4 already covers that; this step just re-confirms nothing else regressed while writing the sample).

Run: `dotnet test sources/IcyUI.Tests`

- [ ] **Step 6: Commit**

```bash
git add "sources/Shared Samples/MarkupStylesDemo.cs" "sources/MonoGame Sample/MonoGame Sample.csproj" "sources/Stride Sample/Stride Sample.csproj"
git commit -m "Add a shared sample exercising M4 markup styling/animation"
```

---

## Task 15: Manual both-engine smoke test (checklist, not code)

Per standing policy (outstanding since Phase 8, called out again at the end of every milestone note through M3.5) — this is Ivan's step, not something to automate away. Report back with:

- [ ] Both `MonoGame Sample` and `Stride Sample` launch and the new demo section is reachable via its PgUp/PgDown (or equivalent) toggle.
- [ ] Hover/press transitions animate smoothly (not instantly, not janky) on both engines.
- [ ] The implicit `<Style TargetType="Button">` re-skins every button in its scope with no `x:Key` on either engine.
- [ ] The merged `ResourceDictionary` (`Source=`) loads correctly and its entries are visible/usable across the two files.
- [ ] The `Timeline` resource plays correctly when triggered.

---

## Self-review notes (from writing this plan)

- **Spec coverage:** every row of the spec's Decisions table has a task — resource scoping (4, 5), implicit styles (7), merged dictionaries (6), setter syntax (8→3/10), VisualState transitions (11), Timeline-as-resource (12), `x:Key` (already generic, exercised throughout). Both of the spec's explicitly flagged "open items for implementation planning" are resolved with a concrete design (11, 12), and the one gap the spec didn't mention at all (no parameterless constructors) is resolved generically (1) per Ivan's steer rather than per-type.
- **Two things flagged during planning that are worth a short explicit confirmation from Ivan when this plan is reviewed, since they're small semantic calls this plan made without a dedicated question round:** (a) Task 7's "implicit style" and "no style" being the same state (`Style == null`) — no opt-out mechanism exists; (b) Task 12's `Timeline.RepeatCount`'s `Forever` value is spelled `-1` in markup for v1, not the word `"Forever"`, to avoid adding a scoped-to-one-property string converter this milestone doesn't otherwise need.
- **A real bug caught during planning, not yet fixed in code:** Task 7 Step 4 flags that `ResourceDictionary` (declared only as `IDictionary<string, object?>` in Task 4) will make `current is IDictionary dictionary` evaluate `false` for it in `ApplyPropertyElement`'s existing dictionary-population branch, since that check is against the *non-generic* `System.Collections.IDictionary`. Task 7 Step 4 calls this out explicitly as something to resolve with an actual test before trusting either fix path — don't skip that check when implementing.
