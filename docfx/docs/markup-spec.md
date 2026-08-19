# IcyUI Markup Specification

> **Status: draft for review (Phase 9, M0).** This document defines the markup language before any
> loader code is written. Sections marked **OPEN** need a decision before the milestone they belong
> to starts.

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
4. **Property** on the target type, via
   `PropertyRegistry.GetPropertyStore(targetType).TryGetProperty(name)`.
5. **Event** with that name on the target type — the value names a handler method (see §6).
6. Otherwise → `MarkupException`.

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
| `x:Name` | any element | Sets `UIElement.Name` and registers the element in the file's **name scope**, making it findable via `UIElementExtensions.FindControl<T>(name)`. Later, the source generator emits a strongly-typed field per name. |
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

`MarkupExtensionContext` carries the target object, the target `IPropertyReference`, the namespace
scope, the name scope, and the ambient `IcyConfiguration`.

Returning `MarkupValue.Unset` means *"I handled the assignment myself; do not set the property"* —
this is what `{Binding}` does, since a binding installs itself rather than producing a one-shot value.

> This replaces the old `IExpressionResolver`, which returned `string?` where `null` meant "handled".
> That forced the loader to re-parse an already-resolved value, and made it impossible for an
> extension to return a non-string. Extensions now return typed values and receive an
> `IPropertyReference` rather than a raw `PropertyInfo`.

### 4.3 Built-in extensions

| Extension | Milestone | Notes |
|---|---|---|
| `{Binding …}` | M2 | Honors `NonBindableAttribute.AsTarget` and `UIPropertyMetadata.IsBindable`. |
| `{Resource …}` | M4 | Resource dictionary lookup. |
| `@Key` | M2 (optional) | Localization. Uses a `@` prefix rather than braces, matching the prior implementation. |

Custom extensions register through the same configuration facet as short names.

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

> **OPEN — blocks the `UIElement` text adapter.** A `TextBlock` created by the adapter has no
> `FontFamily`, and `TextBlock` renders nothing without one (`ResolveFont` returns `null` when
> `FontFamily` is empty). So `<Button>Click Me</Button>` would silently render an empty button. Two
> candidate fixes:
> 1. **Inheritable text properties** — `FontFamily`/`FontSize`/`Foreground` inherit from ancestors,
>    so setting them once on a page root covers the subtree. This is the *same ancestor-inheritance
>    mechanism* `DataContext` needs in M2, so building it once serves both.
> 2. **A configured default font** on `IcyConfiguration`, used when `FontFamily` is unset.
>
> These are not exclusive. Option 1 is the more generally useful feature and shares machinery with
> M2; option 2 is a smaller safety net. Until one lands, the text adapter for `UIElement` should stay
> disabled rather than produce invisible UI.

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

| # | Question | Blocks |
|---|---|---|
| 1 | Inheritable text properties vs. a configured default font (see §6.4) | The `UIElement` text adapter |
| 2 | Whether `{Binding}` on a non-bindable property should error or warn | M2 |
| 3 | Resource dictionary scoping, `BasedOn` syntax, `VisualStateGroup` and `Timeline` serialization | M4 — **needs its own design discussion** |
| 4 | Property-declaration syntax for markup-defined components | M5 |
