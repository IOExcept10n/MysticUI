# IcyUI Markup Specification

> **Status: M0–M3 implemented.** Sections describe the shipped behavior except where marked
> otherwise — an inline note gives the milestone something landed in, and §10 tracks what's still
> open (M4 styling/animation, M5 composition).

IcyUI markup is an XML dialect for declaring UI trees. It is deliberately XAML-familiar — anyone who
knows WPF or Avalonia should be productive immediately — but sugared so that the common cases carry
no ceremony.

```xml
<StackPanel Orientation="Vertical" Padding="24">
  <TextBlock FontSize="28">Hello</TextBlock>
  <Button x:Name="ok" Padding="12,6">Click Me</Button>

  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="1">Cell</TextBlock>
  </Grid>
</StackPanel>
```

No `xmlns` declaration is required for built-in controls, and the `x:` prefix is implicitly available.

---

## 1. Namespaces and type resolution

### 1.1 Declaring namespaces

| Form | Meaning |
|---|---|
| *(none)* | Built-in IcyUI controls. The default namespace is implicit. |
| `xmlns="https://icyui.dev/markup"` | The same built-in namespace, stated explicitly. |
| `xmlns:x="https://icyui.dev/markup/x"` | The directive namespace. **Implicitly bound to `x`** unless rebound. |
| `xmlns:game="clr-namespace:MyGame.UI;assembly=MyGame"` | A CLR namespace in a named assembly. |
| `xmlns:game="clr-namespace:MyGame.UI"` | A CLR namespace in the resolver's default assembly. |

The `clr-namespace:` form mirrors WPF. The `assembly=` part is optional; when omitted, resolution
falls back to `IAssemblyResolver.DefaultAssembly`.

### 1.2 Resolving an element name to a type

For an element `<Name>` or `<prefix:Name>`:

1. **Prefixed** — resolve the prefix to its declared namespace, then find `Namespace.Name` in the
   declared assembly via `IAssemblyResolver.FindType`.
2. **Unprefixed** — search, in order:
   1. Built-in IcyUI controls.
   2. The **short-name registry** (see below).
3. If nothing matches, raise `MarkupException` naming the tag and the namespaces searched.

Built-ins deliberately take precedence over the short-name registry, and **registering a short name
that collides with a built-in is an error at registration time**, not a silent shadow. Replacing a
built-in is still possible — do it with an explicit prefix, where the intent is visible in the file.

### 1.3 The short-name registry

An optional convenience overlay so frequently-used custom controls can be written unqualified:

```csharp
builder.ConfigureMarkup(m => m.RegisterShortName<HealthBar>());          // <HealthBar/>
builder.ConfigureMarkup(m => m.RegisterShortName<HealthBar>("Health"));  // <Health/>
```

The `clr-namespace` mechanism always works and needs no registration; this exists purely to remove
prefixes from names used constantly in a given project.

---

## 2. Attribute resolution

Every attribute on an element is resolved in this order. The **first** match wins.

1. **Namespace declaration** (`xmlns`, `xmlns:*`) — consumed by the parser, never treated as a property.
2. **Directive** (in the `x:` namespace) — see §3.
3. **Attached property** — the name contains a `.` (see §5).
4. **Property** on the target type, resolved in two steps: a property registered with
   `PropertyRegistry` (`GetPropertyStore(targetType).TryGetProperty(name)`) first, falling back to a
   public settable CLR property found by reflection.
5. **Event** with that name on the target type — the value names a handler method (see §6).
6. Otherwise → `MarkupException`.

The registered path is preferred because writing through an `IPropertyReference` puts the value into
the same precedence system styles, visual states, and animations use. The reflection fallback exists
because the registry cannot cover everything markup must reach: `PropertyRegistry` only holds
*writable* properties of types that opted in with `[RegisterReference]`, which excludes both
read-only collections (`Panel.Children`, `Grid.ColumnDefinitions` — populated, not assigned) and
plain data objects that aren't `DependencyObject`s at all (`ColumnDefinition.Width`, and any POCO an
author declares in markup). Requiring every such type to opt in would be ceremony for no benefit —
those properties have no styling or animation story to participate in.

Unknown attributes are an **error**, not silently ignored. (The previous `LayoutSerializer` stashed
unrecognised attributes into a loose `Attributes` bag, which turned every typo into a silent no-op.)

### 2.1 Assigning the value

Once the target property is known:

- If the value is a **markup extension** (§4), evaluate it and use its result.
- Otherwise convert the literal string with
  `ITypeConverter.Convert(value, reference.PropertyType)` and assign via
  `IPropertyReference.SetRawValue`.

Markup-assigned values are **local values**. `SetRawValue` on a `BindableObject` self-registers
through `PropertyChanged`, so no explicit precedence-tier call is needed; styles and animations
continue to lose to markup-set values exactly as they lose to code-set ones.

---

## 3. Directives (`x:`)

| Directive | Valid on | Meaning |
|---|---|---|
| `x:Name` | any element | Sets `UIElement.Name` and registers the element in the file's **name scope**, making it findable via `element.FindControl<T>(name)` from anywhere in the tree. Later, the source generator emits a strongly-typed field per name. |
| `x:Class` | root element only | Names the backing type to instantiate instead of the tag's type (§7). |
| `x:Key` | element inside a dictionary | The dictionary key for this entry. Used by resource dictionaries (M4). |
| `x:DataType` | any element | Declares the expected `DataContext` type for this subtree. Parsed and retained; **not used at runtime** — it exists so tooling and the future generator can validate binding paths. |

Names must be unique within a file's name scope; duplicates are an error.

---

## 4. Markup extensions

An attribute value is a markup extension when, after trimming, it **starts with `{`** and ends with
`}`.

```
extension := '{' name ( WS arguments )? '}'
arguments := positional ( ',' named )*  |  named ( ',' named )*
named     := name '=' value
```

```xml
<TextBlock Text="{Binding UserName}"/>
<TextBlock Text="{Binding Path=UserName, Mode=OneWay}"/>
<Button Command="{Binding SubmitCommand}"/>
```

The first argument may be positional; each extension defines what it means (for `{Binding}` it is
`Path`).

### 4.1 Escaping

A literal value that genuinely begins with `{` is escaped with a leading empty brace pair, as in XAML:

```xml
<TextBlock Text="{}{not an extension}"/>
```

> The previous implementation matched extensions with `^.*\{.+\}$`, which also matched values like
> `total: {0}` that merely *contain* braces. The anchored rule above replaces it.

### 4.2 Extension contract

```csharp
public interface IMarkupExtension
{
    object? ProvideValue(MarkupExtensionContext context);
}
```

`MarkupExtensionContext` carries the target object, the target `MarkupMember` (which exposes the
registered `IPropertyReference` when there is one — see §2.1's reflection-fallback note), the `x:Name`
scope, the ambient `IcyConfiguration`, and the source position for error reporting.

Returning `MarkupValue.Unset` means *"I handled the assignment myself; do not set the property"* —
this is what `{Binding}` does, since a binding installs itself rather than producing a one-shot value.

> This replaces the old `IExpressionResolver`, which returned `string?` where `null` meant "handled".
> That forced the loader to re-parse an already-resolved value, and made it impossible for an
> extension to return a non-string. Extensions now return typed values and receive a `MarkupMember`
> rather than a raw `PropertyInfo`.

**Implemented in M2.** The argument grammar above is exact, including one detail not obvious from the
BNF: a value may be single-quoted to contain a literal comma or brace, e.g.
`{Binding Path='A, B'}` — needed because the argument list itself is comma-separated.

### 4.3 Built-in extensions

| Extension | Milestone | Notes |
|---|---|---|
| `{Binding …}` | **M2 — done** | `Path`, `Source`, `ElementName`, `Mode`, `UpdateTargetTrigger`. No explicit `Source`/`ElementName` binds against the target's `DataContext` (§4.4) and keeps tracking it for the binding's lifetime. Binding a property that is non-bindable (`NonBindableAttribute.AsTarget`, or `UIPropertyMetadata.IsBindable == false`) is an **error**, not a warning — such properties cannot carry bindings, so silently dropping one would leave the UI wrong with no diagnostic. Binding a plain CLR property that was never registered (no `IPropertyReference`) is likewise an error: there is no precedence-system slot for the binding to write into. |
| `{Resource …}` | M4 | Resource dictionary lookup. |
| `@Key` | Deferred | Localization. Not built in M2 — nothing in the codebase currently needs it, and it can land whenever it does without touching the extension mechanism above. |

Custom extensions register through `MarkupConfiguration.RegisterExtension`, the same pattern as short
names (§1.3): `{MyExtension}` maps to a registered `IMarkupExtension` type, with the name defaulting to
the type name minus an `Extension` suffix.

### 4.4 Implicit binding source: `DataContext`

`UIElement.DataContext` is what a `{Binding}` with no explicit `Source` or `ElementName` resolves
against. It is **inherited**, not copied: an element with no `DataContext` of its own returns its
nearest ancestor's, computed by walking `Parent` on every read — there is no eager push-down of the
value itself.

What *does* propagate eagerly is a change notification: `DataContextChanged` fires on an element
whenever its effective value changes, then recurses into every child that has no `DataContext` of its
own (a child that set one keeps it and stops the cascade there). This is also raised when an element's
`Parent` changes, which matters because markup construction is bottom-up — a child's attributes,
`{Binding}` included, are resolved before it is added to its parent, so at bind time it commonly has no
effective `DataContext` yet. `{Binding}` subscribes to this event to keep tracking the inherited value
for as long as the binding lives, rather than reading it once at construction and never again.

**Implemented in M2.** Not yet implemented: the same ancestor-inheritance pattern for `FontFamily`,
`FontSize`, and `Foreground` that §6.4 layer 2 originally described as landing alongside this — it did
not make the approved M2 scope and remains unscheduled. `DataContext` inheritance is hand-written on
`UIElement` rather than built on a shared "inherited property" abstraction; revisit that only if a
second inherited property is actually added, per the project's no-premature-abstraction convention.

---

## 5. Attached properties

Written as `Owner.Property` on the target element:

```xml
<TextBlock Grid.Column="1" Grid.Row="0">Cell</TextBlock>
<Widget game:Dock.Side="Left"/>
```

Resolution: take the part before the final `.` as a **type name** and resolve it exactly as an element
name (§1.2); look the remaining name up in **that owner type's** property store; verify
`UIPropertyMetadata.IsAttached`; convert; assign.

This differs from ordinary property lookup, which searches the *target's* store. `Grid.Row` lives in
`Grid`'s store, not in `TextBlock`'s — the loader must resolve the owner type first.

---

## 6. Content, children, and property elements

### 6.1 The content property

A type designates one content property with `[ContentProperty]`:

| Type | Content property | Kind |
|---|---|---|
| `Panel` (and `StackPanel`, `Grid`) | `Children` | collection — items are **added** |
| `Border` | `Child` | single — **assigned** |
| `ContentControl` (and `Button`, `Window`, …) | `Content` | single — **assigned** |
| `TextBlock` | `Text` | single, string |

### 6.2 Child elements

For each child element of `<Foo>`:

- The tag contains a `.` and its prefix resolves to `Foo`'s type or a base of it → **property element**
  (§6.3).
- Otherwise → **content**. If the content property is a collection, add; if single-valued, assign, and
  raise an error on a second child.

### 6.3 Property elements

Property elements set a property whose value cannot be written as a string:

```xml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*"/>
  </Grid.ColumnDefinitions>
</Grid>
```

The property's current value determines the behavior: `IList` → each child is added; `IDictionary` →
each child is added under its `x:Key`; otherwise the single child is converted and assigned.

### 6.4 Bare text

When an element has non-whitespace text and no element children, the text becomes the content
property's value:

```xml
<TextBlock FontSize="28">Hello</TextBlock>
```

If the content property's type is not assignable from `string`, a registered **text adapter** for that
type is used. The built-in adapter for `UIElement` wraps the text in a `TextBlock`, which is what makes
this work:

```xml
<Button>Click Me</Button>
```

A `TextBlock` created by the adapter has no `FontFamily` of its own, and `TextBlock` renders nothing
without one — `ResolveFont` returns `null` when `FontFamily` is empty. Left unaddressed,
`<Button>Click Me</Button>` would silently render an empty button. **Resolved: three layers, so text
never silently vanishes.**

1. **A configured fallback font** — `FontSystem.FallbackFont` already exists and `GetOrLoad` already
   returns it on a miss, but `TextBlock.ResolveFont` short-circuits to `null` before ever calling
   `GetOrLoad` when `FontFamily` is empty. Fixing that short-circuit, plus a default font configurable
   on `IcyConfiguration`, is the base safety net. **Lands in M1** — it is small and unblocks the
   adapter immediately.
2. **Inheritable text properties** — `FontFamily`, `FontSize`, and `Foreground` inherit from
   ancestors, so setting them once on a page root covers the whole subtree. This was expected to share
   the ancestor-inheritance machinery `DataContext` needed (§4.4) — that part held, `DataContext`'s
   inheritance shipped in M2, but text-property inheritance itself did not make the approved M2 scope
   and is **not yet scheduled**.
3. **Default styles per control type** — a control with no explicit `Style` picks up a default one for
   its type, which can carry a font. The previous implementation did exactly this: its
   `LayoutSerializer.ActivationFactory` looked up `Stylesheet.Default["{TypeName}Style"]` on every
   activation. **Lands in M4**, with the rest of the styling design.

Layer 1 alone is enough for the adapter to be safe, which is why it is the one gating M1.

---

## 7. Backing classes (`x:Class`)

A markup file may name the type it produces:

```xml
<Page x:Class="MyGame.UI.MainMenuPage" xmlns:local="clr-namespace:MyGame.UI">
  <StackPanel>…</StackPanel>
</Page>
```

The loader resolves the named type, verifies it is assignable to the tag's type, and instantiates
**it** instead of the tag type. Without `x:Class`, the tag type is instantiated directly — so a
`<Page>` with no `x:Class` yields a plain `Page`.

This applies to **any** element, not just pages: any control can have a markup file. It generalizes
the previous `[InstanceType]` / `Page.LinkedType` mechanism, which did the same thing through a
designated property.

In M6 the source generator will emit a partial class for each `x:Class` file, carrying typed fields
for every `x:Name` and a generated construction method — which is why the loader keeps object
construction behind a single activation seam.

> `x:Class` resolution itself landed in **M1**, not M3 as originally sequenced: resolving a named type
> and instantiating it in the tag's place is a dozen lines on top of the activation seam the loader
> already needed. M3 (below) is the part that is actually about pages — `Page`, `Frame`, and the
> navigation service.

### 7.1 `Page`, `Frame`, and navigation

A `Page` is an ordinary `ContentControl` — its `Content` is the page's UI, settable via markup like
any other content property, and its markup root can carry `x:Class` exactly as §7 describes:

```xml
<Page x:Class="MyGame.UI.MainMenuPage">
  <StackPanel>…</StackPanel>
</Page>
```

What makes it *navigable* is a `Frame`: a `ContentControl` that owns an `INavigationService`
(`Frame.Navigation`), created for it in its constructor. `NavigateTo(Page)` swaps the frame's
`Content` to the given page, calling `Page.OnNavigatedFrom`/`OnNavigatedTo` as it does; `Navigate(string
path)` additionally loads the page from that path through the asset pipeline first (the same one any
other `.xml` markup file loads through — see `MarkupImporter`), returning `null` when the path doesn't
exist rather than throwing, so a caller can treat a missing page as data rather than an exception.

Back/forward history is two stacks of `Page` instances. Navigating away from a page only keeps it in
history when `Page.KeepAlive` is set — an unset page is simply dropped, so returning to that logical
page later needs a fresh `Navigate` call rather than `TryNavigateBack`/`TryNavigateNext`. A fresh
forward-navigation always clears the forward stack, the same way a browser discards "forward" once you
navigate somewhere new instead of following it.

`Page.Initialize()` runs a page's one-time setup exactly once per instance (guarded by
`Page.IsInitialized`); `Page.Prepare()` runs on every activation, including repeat visits to a
`KeepAlive` page, with no such guard.

> **Deviation from the original plan:** the plan described porting the pre-rewrite `MysticUI`'s
> `IPage`/`INavigationService` pair, including a `NavigateTo(Uri url)` overload. Neither survived
> unchanged: `IPage` added nothing that `ContentControl`'s existing content property and `UIElement`'s
> attach/detach machinery didn't already cover once `Page` was reworked as a plain `ContentControl`, so
> `INavigationService` operates on the concrete `Page` type directly. The `Uri` overload was dropped
> because nothing else in the asset system — `MarkupImporter`, `IAssetContext`, `AssetResolver` — takes
> a `Uri`; every path is a plain string, and adding a `Uri`-based sibling would have been fidelity to
> the old interface at the cost of consistency with the current one. Both calls were confirmed with the
> maintainer before implementation, not decided unilaterally.

---

## 8. Error model

All markup errors raise `MarkupException`, carrying the source path, line, and column. The parser
loads documents with `LoadOptions.SetLineInfo` so every node has position information.

```
MyGame/UI/MainMenu.xml(14,6): Unknown property 'Bakground' on 'Button'. Did you mean 'Background'?
```

Rules:
- Fail on the **first** error; do not partially construct a tree.
- Always name the file, position, offending symbol, and containing type.
- Suggest a near-match for unknown property and type names — typos are the most common failure and
  the cheapest to make actionable.

---

## 9. Schema (`.xsd`)

A schema gives editors completion and inline validation with no IcyUI-specific tooling.

Because the element vocabulary is whatever types are registered, a *useful* schema must be
**generated** by reflecting over the available controls and their registered properties. That
generator is M6 work. M0 ships only a hand-written skeleton — `icyui-markup.xsd` — covering the fixed
parts of the language: the `x:` directive attributes, and the overall document shape. Treat it as a
placeholder that proves the namespace layout, not as a validating schema.

---

## 10. Open questions

### Resolved

| # | Question | Resolution |
|---|---|---|
| 1 | How to keep the `UIElement` text adapter from producing invisible text (§6.4) | Layer 1, the fallback font, shipped in M1 and is sufficient on its own for the adapter to be safe. Layer 2 (inheritable text properties) did not make M2's approved scope and is unscheduled; layer 3 (default per-type styles) lands in M4. |
| 2 | Should `{Binding}` on a non-bindable property error or warn? | **Error.** Such properties cannot carry bindings; a warning would leave the UI silently wrong. |

### Still open

| # | Question | Blocks |
|---|---|---|
| 3 | Resource dictionary scoping, `BasedOn` syntax, `VisualStateGroup` and `Timeline` serialization, default per-type styles | M4 — **needs its own design discussion before implementation** |
| 4 | Property-declaration syntax for markup-defined components | M5 — revisit when M5 starts |
