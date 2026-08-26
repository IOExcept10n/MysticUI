# Phase 9 M4 — Styling & Animation in Markup

> Design spec for M4 of the IcyUI Phase 9 markup plan (`hello-as-you-can-valiant-crane.md`). The
> plan calls M4 out as requiring its own design discussion before implementation - this is that
> discussion, written up. Supersedes M4's placeholder section in the plan once approved.

## Context

Phase 9 M1-M3.5 built the markup object graph, extensions/bindings/DataContext, and
Page/Frame/navigation. `Style`/`Style<T>`, `VisualStateGroup`/`VisualState`, and `Timeline`/`Easing`/
`Animation` already work fully in C# (`sources/Shared Samples/StylesDemo.cs`), but nothing serializes
them from markup yet - every styled screen is still hand-built with C# object initializers.

**What M4 does not cover, by explicit scope decision:** `ControlTemplate`/`DataTemplate` (replacing a
control's whole visual tree, or templating list items). The current architecture has no
replaceable-template hook - `Control` always hardcodes `Chrome` (a `Border`) -> `Content`, and there is
no `ItemsControl` yet to consume a `DataTemplate`. Real templating is deferred to a later milestone,
once there's an actual consumer or a reason to restructure `Control`'s composition.

## Decisions

| Decision | Choice |
|---|---|
| Scope | Style + VisualStateGroup/VisualState + resource dictionaries + Timeline-as-resource + VisualState transitions. No ControlTemplate/DataTemplate. |
| Resource scoping | Hierarchical, per-element - any `UIElement` can declare `Resources`; `{StaticResource}` walks up `Parent`/`LogicalParent` to the root. |
| Implicit styles | A keyless (`x:Key`-less) `<Style TargetType="X">` auto-applies to every exact-type `X` in that dictionary's scope. Exact-type match only, not base-type walk - matches WPF's default; `BasedOn` remains how setters share across a hierarchy. |
| Merged dictionaries | Supported in V1: `<ResourceDictionary.MergedDictionaries>` with `<ResourceDictionary Source="..."/>` entries, resolved through the same `IAssetContext` pipeline `Page`/`Frame` navigation already uses, loaded eagerly at parse time. |
| Setter syntax | Attributes-as-setters is primary: any attribute on `<Style>`/`<VisualState>` that isn't a reserved name (`TargetType`/`BasedOn`/`x:Key` on `Style`; `Name`/`Duration`/`Easing` on `VisualState`) resolves as a setter through the same attribute-assignment pipeline every element already uses. `<Setter Property="X" Value="Y"/>` child elements are the fallback for markup-extension values or name collisions. No new `Setter` runtime type - it's a markup-loader convention for populating a `Dictionary<string, object?>`-typed target, the same way `<Grid.ColumnDefinitions>` already populates a list. |
| VisualState transitions | `VisualState` gains optional `Duration`/`Easing`. When set, entering the state animates each setter from the element's current effective value to the target via the existing `Animation`/`ValueInterpolator` machinery (current value stands in for an explicit "from" keyframe). Unset = today's instant snap, unchanged. |
| Timeline in markup | `Timeline` becomes a resource-dictionary-addressable, markup-serializable object (`Keyframes` as its content property). Purely a serialization target for the existing `Animate(Timeline)` API - no new triggering/event system this milestone. |
| `x:Key` | Already exists as a recognized directive from M1 (generic dictionary-population support) - `Resources` just needs to be a dictionary-shaped property; no new directive parsing needed. |

## Detailed design

### 1. Resources & lookup

- New `UIElement.Resources` property: a `ResourceDictionary` (string-keyed, insertion-ordered; keyed
  entries populate the same way M1's `IDictionary`/`x:Key` population already works for
  `<Grid.ColumnDefinitions>`-style collections). In markup:
  `<Panel.Resources><Style x:Key="AccentButton" TargetType="Button" .../></Panel.Resources>`.
- `ResourceDictionary.MergedDictionaries`: a list of other `ResourceDictionary` instances, loadable via
  `<ResourceDictionary Source="Themes/DefaultTheme.xml"/>`, resolved through `IAssetContext` and loaded
  eagerly at parse time (matches `MarkupLoader` being synchronous throughout).
- `{StaticResource Key}` (a new `IMarkupExtension`, M2's mechanism) resolves by walking from the
  requesting element up through `Parent`/`LogicalParent` to the root, checking each element's own
  `Resources` first, then its `MergedDictionaries`. A local entry always shadows a same-key merged one;
  among multiple merged dictionaries, a later one shadows an earlier one on collision - both are
  intentional (theme layering/overriding), not errors. An unresolvable key is a `MarkupException` at
  load time.
- **Implicit styles**: a keyless `<Style TargetType="Button">` registers under a reserved internal key
  scheme keyed by `typeof(Button)` and auto-applies to every exact-type `Button` in scope. A subclass
  (`ToggleButton`) is not implicitly matched by a `Button`-targeted style - only `BasedOn` shares setters
  across a hierarchy.

### 2. Style markup surface

- `<Style TargetType="Button" x:Key="AccentButton" BasedOn="{StaticResource Base}" Background="Blue"
  BorderThickness="2">` - any attribute other than `TargetType`/`BasedOn`/`x:Key` resolves as a setter
  through `TargetType`'s `PropertyRegistry` store (same attribute -> `IPropertyReference` ->
  `ITypeConverter` pipeline every element already uses). An unresolvable name is a `MarkupException`,
  same as today's behavior for a bad attribute on any regular element.
- `<Setter Property="Background" Value="{StaticResource AccentBrush}"/>` as a child element is the
  fallback for a markup-extension value or a reserved-name collision.
- `<Style.StateGroups><VisualStateGroup Name="CommonStates"><VisualState Name="MouseOver"
  Duration="0:0:0.15" Easing="CubicOut" Background="LightBlue"/></VisualStateGroup></Style.StateGroups>`
  - `VisualState` gets the identical attributes-as-setters rule (reserved: `Name`, `Duration`,
  `Easing`), for consistency with `Style`.

### 3. VisualState transitions

- `VisualState` (`sources/IcyUI/UI/Styles/VisualState.cs`) gains `TimeSpan? Duration` and
  `EasingFunction? Easing` - new C# properties; today `VisualState` is setters-only and always instant.
- When a state group's active state changes (`UIElement.RegisterStateGroup`/`ApplyBestMatchingState`)
  and the newly-active state has `Duration` set, each of its setters animates from the element's current
  effective value to the target via the existing `Animation`/`ValueInterpolator` machinery - the current
  value stands in for an explicit "from" keyframe, so no keyframe authoring is needed for this path.
  `Duration` unset preserves today's instant-snap behavior exactly.
- Rapid re-triggering (e.g. hover on/off before a transition finishes) is expected to rely on
  `Animation`'s existing same-property replace-in-place behavior. This spec does not re-design that -
  confirming the exact mechanics is implementation-planning work, not a new design decision.

### 4. Timeline as a resource

- `<Timeline x:Key="Pulse" TargetProperty="Opacity" Duration="0:0:1" Easing="SineInOut"
  RepeatCount="Forever" AutoReverse="True"><AnimationKeyframe Offset="0" Value="0.5"/>
  <AnimationKeyframe Offset="1" Value="1.0"/></Timeline>` - `Keyframes` is `Timeline`'s content
  property, each entry a plain `<AnimationKeyframe Offset="..." Value="..."/>`.
- Purely a serialization target for the existing `AnimationExtensions.Animate(Timeline)` API - retrieved
  via `{StaticResource Pulse}` and played from C#. No event/trigger system is introduced this milestone;
  this path exists for multi-keyframe animation that #3's current-to-target model can't express (e.g. a
  three-stop color cycle), independent of any `VisualState`.

### 5. Error handling

- Unresolved `{StaticResource Key}` -> `MarkupException` at load.
- An attribute-as-setter name that doesn't resolve on `TargetType`'s `PropertyRegistry` ->
  `MarkupException`, matching M1's existing behavior for any other element's bad attribute.
- Duplicate `x:Key` within one dictionary's own direct entries -> `MarkupException` (an authoring
  mistake, not a valid override). Local-shadows-merged and later-merge-shadows-earlier-merge are both
  intentional and silent (see Resources & lookup above).
- **Bundled fix to existing code**: `Style.Apply`'s `BasedOn?.Apply(control)` recursion
  (`sources/IcyUI/UI/Styles/Style.cs`) has no cycle guard today - a `BasedOn` cycle stack-overflows if
  constructed by hand in C#. Low-risk while `BasedOn` chains are hand-written and rare; markup makes an
  accidental cycle (two named resources `BasedOn`-ing each other) much easier to create by mistake. This
  milestone adds a visited-set guard to `Style.Apply` itself, throwing a `DiagnosticsException`-shaped
  error (mirroring the M3.5 `DebugToolRegistry` collision-detection convention) rather than crashing.

### 6. Testing

- Unit tests following the M1/M2 convention (`TheoryData`+`[MemberData]` or `[Fact]`, inline private
  fakes, Arrange/Act/Assert): `ResourceDictionary` lookup/precedence/merge, implicit-style application
  (exact-type match, no subclass match), attribute-as-setter and `<Setter>` element parsing,
  `BasedOn`-cycle detection, `VisualState` transition animation via a frame-stepped `FakeRenderContext`.
- An equivalence test: a markup-styled screen matching an equivalent hand-built `Style`/
  `VisualStateGroup` C# tree structurally - the same regression guard M1 used for the control tree.
- Manual smoke test on both engines at milestone boundary: hover/press transitions animate smoothly,
  implicit theme reskinning works from a single `<Style TargetType="Button">` with no `x:Key`, a merged
  dictionary loads correctly across two sample markup files.

## Critical files

**New:** `sources/IcyUI/UI/ResourceDictionary.cs`, a `{StaticResource}` `IMarkupExtension`
(`sources/IcyUI/Markup/Extensions/`), `sources/IcyUI.Tests/Markup/` additions for the above.

**Modified:** `sources/IcyUI/UI/UIElement.cs` (`Resources` property), `sources/IcyUI/UI/Styles/
VisualState.cs` (`Duration`/`Easing`), `sources/IcyUI/UI/Styles/Style.cs` (`BasedOn` cycle guard),
`UIElement`'s state-group application path (animate setters when `Duration` is set),
`sources/IcyUI/Animations/Timeline.cs` (markup-friendly content-property shape, if not already
compatible), `sources/IcyUI/Markup/` (namespace/extension registration).

## Open items for implementation planning (not blocking spec approval)

- The exact mechanics of interrupting/replacing an in-flight per-property `Animation` when a
  `VisualState` transition re-triggers before finishing - confirm against `Animation`'s current
  same-target behavior during planning.
- Whether `Timeline`'s current shape needs any adjustment to serialize cleanly as `Setter`/content-
  property markup (e.g. `AnimationKeyframe`'s constructor/settability), or is already compatible as-is.
