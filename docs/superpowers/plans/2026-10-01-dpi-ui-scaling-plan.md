# DPI-aware UI Scaling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make IcyUI scale its UI by OS DPI, a reference resolution, and a user UI-scale setting, with crisp text and pixel-snapped edges in both MonoGame and Stride.

**Architecture:** `Canvas` computes `EffectiveScale = base(mode) × UserScale` once per frame and lays out against a logical *surface* (`physical ÷ scale`). The scale sits under the existing canvas transform. OS DPI detection lives in core (`Icy.Rendering.Display`, Clipboards-style), and engines only hand over native window info. Dynamic fonts draw device-resolution glyphs at logical pen positions, and both engines snap axis-aligned quads to whole physical pixels.

**Tech Stack:** C# / .NET 10, xUnit v2, CommunityToolkit.Mvvm (`ObservableDispatcherObject`), MonoGame 3.8.5 DesktopGL, Stride 4.3.

**Spec:** `docs/superpowers/specs/2026-10-01-dpi-ui-scaling-design.md`

## Global Constraints

- Every public API gets complete XML documentation (`<see cref>`, `<see langword>`, `<list>`, `<para>`). The DocFX site is generated from it.
- Files start with the repo copyright header and use block-scoped `namespace X { }`. StyleCop applies to `IcyUI`, `IcyUI.MonoGame` and `IcyUI.Stride`, so add no new warnings.
- OS-specific calls (`user32` etc.) live only in core `IcyUI`. Engine projects are connectors: they may call their own engine libraries (SDL2 for MonoGame), never OS APIs.
- Core `IcyUI` must not reference engine types.
- Defaults: `Mode = UIScaleMode.Dpi`, `ReferenceSize = 1920×1080`, `ReferenceFit = ReferenceFit.Fit`, `UserScale = 1f`.
- `Canvas` subscribes to **no** events (it isn't `IDisposable`). It recomputes its scale once per `Render()`.
- "Screen" keeps meaning **physical pixels** in every existing API (`PointToScreen`, `PointToLocal`, `HitTest`, `ScreenToCanvasSpace`). Surface coordinates get new, explicitly named members.
- Test commands: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`, `dotnet build "sources/IcyUI.sln"`.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Plan-level decisions (resolving the spec's open items)

1. **Screen stays physical.** Changing `CanvasToScreenSpace` to surface space would break the pinned invariant `PointToLocal(PointToScreen(p)) == p` (`CanvasHitTestFocusTests.cs:342`), because `PointToLocal` takes raw pointer positions. Instead, add `UIElement.PointToSurface(Vector2)` and `Canvas.ScreenToSurface(Point)`. Popups use those.
2. **Device glyphs need no `FontSystem` back-reference.** `DynamicFontAtlas` already keys glyphs by `(codepoint, FontSize, Style)`, so `DynamicSpriteFont` rasterizes device-size glyphs into its own shared atlas.
3. **Device glyphs are drawn in device units.** `TextureRenderingOptions.Destination` is an integer `Rectangle`, so drawing at `1/s` in logical units would round away the precision. `DrawString` temporarily composes `Scale(1/q)` into `context.Transform` and emits integer *device-unit* rectangles.
4. **One provider covers macOS and Linux.** `DrawableRatioDisplayScaleProvider` (drawable ÷ window size) serves both (YAGNI versus two identical classes).
5. **A scale change invalidates arrange, not measure.** This matches the existing `Canvas.OnResize()`; measure doesn't depend on the viewport.
6. **The MonoGame HWND comes via `SDL_GetWindowWMInfo`**, a `DllImport` into the `SDL2.dll` that DesktopGL ships. That's an engine library, so it's allowed in the connector. WindowsDX's `Window.Handle` is already an HWND.

## Review Focus

1. **Minimized window (0×0 back buffer) in `ReferenceResolution` mode.** The scale must fall back to 1 with no division by zero, NaN or exceptions. Tests: Task 1 (calculator) and Task 2 (canvas render at 0×0).
2. **Scale changing while a popup overlay is open** (user drags the UI-scale slider with a dropdown open). The overlay must be re-arranged inside the new `SurfaceSize` on the next frame. Test: Task 3.
3. **DPI combined with the user's own `Canvas.Offset`/`Scale`.** Hit-testing and `PointToLocal`/`PointToScreen` round trips must stay exact. Test: Task 3.
4. **Fractional scale (1.25×) with elements sharing an edge.** No seams, and the snapped rectangles must still touch. Test: Task 6.
5. **Multi-line text at 1.5×.** Device-glyph line positions must match logical line positions, so text doesn't drift vertically. Test: Task 5.

## File map

| File | Task | Responsibility |
|---|---|---|
| `sources/IcyUI/UI/UIScaleMode.cs`, `UI/ReferenceFit.cs` (new) | 1 | Public enums |
| `sources/IcyUI/UI/UIScaleCalculator.cs` (new, internal) | 1 | Pure scale formula |
| `sources/IcyUI/Configuration/ScalingConfiguration.cs`, `IScalingConfigurationBuilder.cs` (new) | 1 | App-wide scaling facet |
| `Configuration/IcyConfiguration.cs`, `IcyConfigurationBuilder.cs`, `IConfigurationBuilder.cs`, `BuildingExtensions.cs` | 1 | Facet wiring |
| `sources/IcyUI/Rendering/IRenderContext.cs` | 2 | `DisplayScale` and `DisplayScaleChanged` default members |
| `sources/IcyUI/UI/Canvas.cs` | 2, 3 | Scale state, surface space, transforms |
| `sources/IcyUI/UI/UIElement.cs` | 3 | `PointToSurface` |
| `UI/Controls/Selector.cs`, `UI/Controls/ColorPickerButton.cs`, `Diagnostics/DebugHudHost.cs` | 3 | Use the surface |
| `sources/IcyUI/Rendering/Display/*.cs` (new) | 4 | Platform DPI detection |
| `Rendering/Fonts/SpriteFont.cs`, `Rendering/Fonts/DynamicSpriteFont.cs` | 5 | Device-resolution glyphs |
| `sources/IcyUI/Rendering/PixelSnapping.cs` (new) | 6 | Edge snapping helper |
| `IcyUI.MonoGame/Rendering/RenderContext.cs`, `IcyUI.Stride/Rendering/RenderContext.cs` | 6, 7 | Snapping, display-scale forwarding |
| `IcyUI.MonoGame/Rendering/MonoGameNativeWindow.cs`, `IcyUI.Stride/Rendering/StrideNativeWindow.cs` (new) | 7 | Connectors |
| `MonoGameBuildingExtensions.cs`, `MonoGameIcyRenderer.cs`, `StrideBuildingExtensions.cs`, `IcyUIGameSystem.cs` | 7 | Wiring and polling |
| `Shared Samples/ScalingDemo.cs`, `MonoGame Sample/Samples/ScalingSample.cs` (new); both `SampleGame.cs` and csproj files; `Stride Sample/app.manifest` (new) | 8 | Demo |
| `IcyUI.Tests/...` (new test files per task) | 1–6 | Tests |

---

### Task 1: Scaling configuration facet and scale calculator

**Files:**
- Create: `sources/IcyUI/UI/UIScaleMode.cs`, `sources/IcyUI/UI/ReferenceFit.cs`, `sources/IcyUI/UI/UIScaleCalculator.cs`
- Create: `sources/IcyUI/Configuration/ScalingConfiguration.cs`, `sources/IcyUI/Configuration/IScalingConfigurationBuilder.cs`
- Modify: `sources/IcyUI/Configuration/IcyConfiguration.cs`, `IcyConfigurationBuilder.cs`, `IConfigurationBuilder.cs`, `BuildingExtensions.cs`
- Test: `sources/IcyUI.Tests/UI/UIScaleCalculatorTests.cs`, `sources/IcyUI.Tests/Configuration/ScalingConfigurationTests.cs`

**Interfaces:**
- Produces: `enum UIScaleMode { None, Dpi, ReferenceResolution }`, `enum ReferenceFit { Fit, Fill, MatchWidth, MatchHeight }` (namespace `Icy.UI`).
- Produces: `internal static float UIScaleCalculator.Compute(UIScaleMode mode, float displayScale, Size viewport, Size reference, ReferenceFit fit, float userScale)`.
- Produces: `public class ScalingConfiguration : ObservableDispatcherObject` with `Mode`, `ReferenceSize`, `ReferenceFit`, `UserScale`; `IcyConfiguration.Scaling` (never null); `IScalingConfigurationBuilder.Scaling`; `IConfigurationBuilder.ConfigureScaling()` / `ConfigureScaling(ScalingConfiguration)`; extension `ConfigureScaling(this IScalingConfigurationBuilder, Action<ScalingConfiguration>)`.

- [ ] **Step 1: Write the failing calculator tests**

`sources/IcyUI.Tests/UI/UIScaleCalculatorTests.cs`:

```csharp
using System.Drawing;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class UIScaleCalculatorTests
    {
        private static readonly Size Reference = new(1920, 1080);

        [Theory]
        [InlineData(UIScaleMode.None, 2f, 1f)]
        [InlineData(UIScaleMode.Dpi, 2f, 2f)]
        [InlineData(UIScaleMode.Dpi, 1.25f, 1.25f)]
        public void Compute_NoneAndDpi_UseDisplayScale(UIScaleMode mode, float display, float expected) =>
            Assert.Equal(expected, UIScaleCalculator.Compute(mode, display, new Size(2880, 1920), Reference, ReferenceFit.Fit, 1f), 3);

        [Theory]
        [InlineData(ReferenceFit.Fit, 1.5f)]
        [InlineData(ReferenceFit.Fill, 1.7778f)]
        [InlineData(ReferenceFit.MatchWidth, 1.5f)]
        [InlineData(ReferenceFit.MatchHeight, 1.7778f)]
        public void Compute_ReferenceResolution_AppliesFitPolicy(ReferenceFit fit, float expected) =>
            Assert.Equal(expected, UIScaleCalculator.Compute(UIScaleMode.ReferenceResolution, 2f, new Size(2880, 1920), Reference, fit, 1f), 3);

        [Fact]
        public void Compute_MultipliesUserScaleOnTopOfBase() =>
            Assert.Equal(3f, UIScaleCalculator.Compute(UIScaleMode.Dpi, 2f, new Size(800, 600), Reference, ReferenceFit.Fit, 1.5f), 3);

        [Fact]
        public void Compute_ZeroViewport_FallsBackToOne() =>
            Assert.Equal(1f, UIScaleCalculator.Compute(UIScaleMode.ReferenceResolution, 2f, Size.Empty, Reference, ReferenceFit.Fit, 1f));

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.PositiveInfinity)]
        public void Compute_InvalidDisplayScale_FallsBackToOne(float display) =>
            Assert.Equal(1f, UIScaleCalculator.Compute(UIScaleMode.Dpi, display, new Size(800, 600), Reference, ReferenceFit.Fit, 1f));
    }
}
```

- [ ] **Step 2: Write the failing configuration tests**

`sources/IcyUI.Tests/Configuration/ScalingConfigurationTests.cs`:

```csharp
using System.ComponentModel;
using System.Drawing;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Configuration
{
    public class ScalingConfigurationTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var scaling = new ScalingConfiguration();

            Assert.Equal(UIScaleMode.Dpi, scaling.Mode);
            Assert.Equal(new Size(1920, 1080), scaling.ReferenceSize);
            Assert.Equal(ReferenceFit.Fit, scaling.ReferenceFit);
            Assert.Equal(1f, scaling.UserScale);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.5f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void UserScale_RejectsInvalidValues(float value) =>
            Assert.ThrowsAny<ArgumentException>(() => new ScalingConfiguration().UserScale = value);

        [Fact]
        public void ReferenceSize_RejectsEmpty() =>
            Assert.ThrowsAny<ArgumentException>(() => new ScalingConfiguration().ReferenceSize = new Size(0, 1080));

        [Fact]
        public void UserScale_RaisesPropertyChanged()
        {
            var scaling = new ScalingConfiguration();
            string? changed = null;
            ((INotifyPropertyChanged)scaling).PropertyChanged += (_, e) => changed = e.PropertyName;

            scaling.UserScale = 1.5f;

            Assert.Equal(nameof(ScalingConfiguration.UserScale), changed);
        }

        [Fact]
        public void Builder_ProvidesNonNullScalingFacet()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext()).ConfigureInput(new FakeInputSystem());

            IcyConfiguration configuration = builder.Build();

            Assert.NotNull(configuration.Scaling);
        }

        [Fact]
        public void ConfigureScaling_Action_AppliesToBuiltConfiguration()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext()).ConfigureInput(new FakeInputSystem());
            builder.ConfigureScaling().ConfigureScaling(s => s.Mode = UIScaleMode.None);

            Assert.Equal(UIScaleMode.None, builder.Build().Scaling.Mode);
        }
    }
}
```

- [ ] **Step 3: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~UIScaleCalculatorTests|FullyQualifiedName~ScalingConfigurationTests"`
Expected: build FAILS (`UIScaleMode`, `UIScaleCalculator`, `ScalingConfiguration` don't exist).

- [ ] **Step 4: Add the enums**

`sources/IcyUI/UI/UIScaleMode.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Specifies how a <see cref="Canvas"/> chooses the base factor of its <see cref="Canvas.EffectiveScale"/>.
    /// </summary>
    /// <remarks>
    /// The base factor is always multiplied by <see cref="Configuration.ScalingConfiguration.UserScale"/>.
    /// <see cref="Dpi"/> and <see cref="ReferenceResolution"/> are alternatives and are never combined.
    /// </remarks>
    public enum UIScaleMode
    {
        /// <summary>
        /// No base scaling: one UI unit is one physical pixel.
        /// </summary>
        None,

        /// <summary>
        /// Follows the operating system display scale reported by <see cref="Rendering.IRenderContext.DisplayScale"/>
        /// (e.g. <c>2.0</c> on a 200 % display). A host that isn't DPI-aware reports <c>1.0</c>.
        /// </summary>
        Dpi,

        /// <summary>
        /// Scales the UI so that a fixed reference resolution fits the viewport according to a <see cref="ReferenceFit"/> policy.
        /// </summary>
        ReferenceResolution,
    }
}
```

`sources/IcyUI/UI/ReferenceFit.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Specifies how <see cref="UIScaleMode.ReferenceResolution"/> maps a reference resolution onto a viewport
    /// whose aspect ratio may differ from it.
    /// </summary>
    public enum ReferenceFit
    {
        /// <summary>
        /// Uses the smaller of the width and height ratios, so the whole reference area is always visible.
        /// </summary>
        Fit,

        /// <summary>
        /// Uses the larger of the width and height ratios, so the reference area always covers the viewport.
        /// </summary>
        Fill,

        /// <summary>
        /// Uses the width ratio only.
        /// </summary>
        MatchWidth,

        /// <summary>
        /// Uses the height ratio only.
        /// </summary>
        MatchHeight,
    }
}
```

- [ ] **Step 5: Add the calculator**

`sources/IcyUI/UI/UIScaleCalculator.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.UI
{
    /// <summary>
    /// Computes a <see cref="Canvas"/>'s effective UI scale from its scaling inputs.
    /// </summary>
    internal static class UIScaleCalculator
    {
        /// <summary>
        /// Computes <c>base(mode) × userScale</c>.
        /// </summary>
        /// <param name="mode">The scale mode that picks the base factor.</param>
        /// <param name="displayScale">The OS display scale, used by <see cref="UIScaleMode.Dpi"/>.</param>
        /// <param name="viewport">The physical viewport size, used by <see cref="UIScaleMode.ReferenceResolution"/>.</param>
        /// <param name="reference">The reference resolution, used by <see cref="UIScaleMode.ReferenceResolution"/>.</param>
        /// <param name="fit">The fit policy, used by <see cref="UIScaleMode.ReferenceResolution"/>.</param>
        /// <param name="userScale">The user preference multiplied on top of the base factor.</param>
        /// <returns>The effective scale, or <c>1</c> when the inputs don't produce a finite positive value.</returns>
        public static float Compute(UIScaleMode mode, float displayScale, Size viewport, Size reference, ReferenceFit fit, float userScale)
        {
            float baseScale = mode switch
            {
                UIScaleMode.Dpi => displayScale,
                UIScaleMode.ReferenceResolution => ComputeFit(viewport, reference, fit),
                _ => 1f,
            };

            float result = baseScale * userScale;
            return float.IsFinite(result) && result > 0 ? result : 1f;
        }

        private static float ComputeFit(Size viewport, Size reference, ReferenceFit fit)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0 || reference.Width <= 0 || reference.Height <= 0)
                return 1f;

            float widthRatio = (float)viewport.Width / reference.Width;
            float heightRatio = (float)viewport.Height / reference.Height;
            return fit switch
            {
                ReferenceFit.Fill => MathF.Max(widthRatio, heightRatio),
                ReferenceFit.MatchWidth => widthRatio,
                ReferenceFit.MatchHeight => heightRatio,
                _ => MathF.Min(widthRatio, heightRatio),
            };
        }
    }
}
```

- [ ] **Step 6: Add `ScalingConfiguration` and its builder interface**

`sources/IcyUI/Configuration/ScalingConfiguration.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Icy.Data.Bindings;
using Icy.UI;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents the application-wide UI scaling defaults.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <see cref="Canvas"/> built against the owning <see cref="IcyConfiguration"/> combines these values into its
    /// <see cref="Canvas.EffectiveScale"/>. A canvas may override <see cref="Mode"/>, <see cref="ReferenceSize"/> and
    /// <see cref="ReferenceFit"/> individually. <see cref="UserScale"/> is global, so a single options slider affects every canvas.
    /// </para>
    /// <para>
    /// Changes take effect on each canvas's next <see cref="Canvas.Render"/> call.
    /// </para>
    /// </remarks>
    public class ScalingConfiguration : ObservableDispatcherObject
    {
        private UIScaleMode mode = UIScaleMode.Dpi;
        private Size referenceSize = new(1920, 1080);
        private ReferenceFit referenceFit = ReferenceFit.Fit;
        private float userScale = 1f;

        /// <summary>
        /// Gets or sets how the base scale factor is chosen. Defaults to <see cref="UIScaleMode.Dpi"/>.
        /// </summary>
        public UIScaleMode Mode
        {
            get => mode;
            set => SetProperty(ref mode, value);
        }

        /// <summary>
        /// Gets or sets the resolution the UI is designed for, used by <see cref="UIScaleMode.ReferenceResolution"/>.
        /// Defaults to 1920×1080.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
        public Size ReferenceSize
        {
            get => referenceSize;
            set
            {
                Guard.IsGreaterThan(value.Width, 0);
                Guard.IsGreaterThan(value.Height, 0);
                SetProperty(ref referenceSize, value);
            }
        }

        /// <summary>
        /// Gets or sets how <see cref="ReferenceSize"/> is fitted to the viewport. Defaults to <see cref="UI.ReferenceFit.Fit"/>.
        /// </summary>
        public ReferenceFit ReferenceFit
        {
            get => referenceFit;
            set => SetProperty(ref referenceFit, value);
        }

        /// <summary>
        /// Gets or sets the user's UI-scale preference, multiplied on top of the base factor. Defaults to <c>1</c>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a finite number greater than zero.</exception>
        public float UserScale
        {
            get => userScale;
            set
            {
                if (!float.IsFinite(value) || value <= 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value), value, "The user scale must be a finite number greater than zero.");
                SetProperty(ref userScale, value);
            }
        }
    }
}
```

`sources/IcyUI/Configuration/IScalingConfigurationBuilder.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Configuration
{
    /// <summary>
    /// Represents a configuration building service with additional methods to build the UI scaling configuration.
    /// </summary>
    public interface IScalingConfigurationBuilder : IConfigurationBuilder
    {
        /// <summary>
        /// Gets an instance of the <see cref="ScalingConfiguration"/> to be used in application.
        /// </summary>
        public ScalingConfiguration Scaling { get; }
    }
}
```

- [ ] **Step 7: Wire the facet through the builder and configuration**

First confirm `IcyConfigurationBuilder` is the only implementer of `IConfigurationBuilder`:

Run: `grep -rn "IConfigurationBuilder\b" sources --include=*.cs | grep "class "`
Expected: only `IcyConfigurationBuilder`. If anything else appears, add the two new members to it as well.

In `sources/IcyUI/Configuration/IConfigurationBuilder.cs`, after `IThemeConfigurationBuilder ConfigureTheme();` add:

```csharp
        /// <summary>
        /// Sets the UI scaling configuration to be used by the library.
        /// </summary>
        /// <param name="scalingConfiguration">The scaling configuration instance.</param>
        /// <returns>The current builder instance for fluent configuration.</returns>
        IScalingConfigurationBuilder ConfigureScaling(ScalingConfiguration scalingConfiguration);

        /// <summary>
        /// Configures the default UI scaling settings with additional settings.
        /// </summary>
        /// <returns>An instance of <see cref="IScalingConfigurationBuilder"/> to further configure UI scaling.</returns>
        IScalingConfigurationBuilder ConfigureScaling();
```

In `IcyConfigurationBuilder.cs`:
- Add `IScalingConfigurationBuilder` to the implemented interface list (after `IThemeConfigurationBuilder`).
- Add the property after `Theme`:

```csharp
        /// <inheritdoc/>
        [NotNull]
        public ScalingConfiguration? Scaling { get; private set; }
```

- In `Build()`, call `ConfigureScaling();` after `ConfigureTheme();`, and change the return to `return new IcyConfiguration(InputSystem, Assets, RenderContext, Types, Theme, Scaling);`
- Add at the end of the class:

```csharp
        /// <inheritdoc/>
        public IScalingConfigurationBuilder ConfigureScaling(ScalingConfiguration scalingConfiguration)
        {
            Scaling = scalingConfiguration;
            return this;
        }

        /// <inheritdoc/>
        public IScalingConfigurationBuilder ConfigureScaling()
        {
            Scaling ??= new ScalingConfiguration();
            return this;
        }
```

In `IcyConfiguration.cs`, extend the constructor signature and body, and add the property:

```csharp
        /// <param name="scaling">
        /// The UI scaling defaults used by the library. Defaults to a new <see cref="ScalingConfiguration"/>
        /// (<see cref="UI.UIScaleMode.Dpi"/>) when omitted.
        /// </param>
        public IcyConfiguration(IInputSystem input, AssetConfiguration assets, IRenderContext renderContext, ReflectionConfiguration types, ThemeConfiguration? theme = null, ScalingConfiguration? scaling = null)
        {
            Input = input;
            Assets = assets;
            RenderContext = renderContext;
            Types = types;
            Theme = theme ?? new();
            Scaling = scaling ?? new();
            Fonts = new(this);
        }
```

(Keep the existing `<param>` tags, adding the new one after `theme`.) Then after the `Theme` property:

```csharp
        /// <summary>
        /// Gets the application-wide UI scaling defaults used by every <see cref="UI.Canvas"/> built against this configuration.
        /// </summary>
        public ScalingConfiguration Scaling { get; }
```

In `BuildingExtensions.cs`, after the `ConfigureTheme(this IThemeConfigurationBuilder, Action<ThemeConfiguration>)` extension, add:

```csharp
        /// <summary>
        /// Configures the UI scaling facet - the defaults every <see cref="UI.Canvas"/> uses to compute
        /// <see cref="UI.Canvas.EffectiveScale"/>.
        /// </summary>
        /// <param name="builder">The scaling configuration builder instance.</param>
        /// <param name="configure">The configuration action applied to <see cref="ScalingConfiguration"/>.</param>
        /// <returns>The current scaling configuration builder instance for fluent configuration.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.ConfigureScaling().ConfigureScaling(s => s.Mode = UIScaleMode.ReferenceResolution);
        /// </code>
        /// </example>
        public static IScalingConfigurationBuilder ConfigureScaling(this IScalingConfigurationBuilder builder, Action<ScalingConfiguration> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            configure(builder.Scaling);
            return builder;
        }
```

(The XML docs reference `Canvas.EffectiveScale`, which Task 2 adds. Until then expect one CS1574 warning; it disappears in Task 2.)

- [ ] **Step 8: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~UIScaleCalculatorTests|FullyQualifiedName~ScalingConfigurationTests"`
Expected: PASS. Then run the full suite: all tests pass (901 existing + new).

- [ ] **Step 9: Commit**

```bash
git add sources/IcyUI/UI/UIScaleMode.cs sources/IcyUI/UI/ReferenceFit.cs sources/IcyUI/UI/UIScaleCalculator.cs sources/IcyUI/Configuration sources/IcyUI.Tests/UI/UIScaleCalculatorTests.cs sources/IcyUI.Tests/Configuration/ScalingConfigurationTests.cs
git commit -m "Add UI scaling configuration facet and scale calculator" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Display scale on `IRenderContext` and canvas scale state

**Files:**
- Modify: `sources/IcyUI/Rendering/IRenderContext.cs`
- Modify: `sources/IcyUI.Tests/Rendering/FakeRenderContext.cs`
- Modify: `sources/IcyUI/UI/Canvas.cs`
- Test: `sources/IcyUI.Tests/UI/CanvasScalingTests.cs`

**Interfaces:**
- Consumes: `UIScaleCalculator.Compute(...)`, `IcyConfiguration.Scaling` (Task 1).
- Produces: `IRenderContext.DisplayScale` (default `1f`), `IRenderContext.DisplayScaleChanged`.
- Produces on `Canvas`: `UIScaleMode? ScaleMode`, `Size? ReferenceSize`, `ReferenceFit? ReferenceFit`, `float EffectiveScale { get; }`, `Size SurfaceSize { get; }`, `void RefreshScale()`. `ContentBounds` becomes surface-sized.
- Produces on `FakeRenderContext`: settable `float DisplayScale`, `void NotifyDisplayScaleChanged()`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/UI/CanvasScalingTests.cs`:

```csharp
using System.Drawing;
using System.Runtime.CompilerServices;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasScalingTests
    {
        internal static (Canvas Canvas, FakeRenderContext Context, IcyConfiguration Configuration) Create(float displayScale = 1f, Size? viewport = null)
        {
            var context = new FakeRenderContext { DisplayScale = displayScale, ViewportSize = viewport ?? new Size(800, 600) };
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), context, new ReflectionConfiguration());
            return (new Canvas(configuration), context, configuration);
        }

        private static UIElement Stretch() => new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

        [Fact]
        public void DefaultDpiMode_UsesDisplayScale_AndShrinksSurface()
        {
            var (canvas, _, _) = Create(displayScale: 2f);
            UIElement root = Stretch();
            canvas.Add(root);

            canvas.Render();

            Assert.Equal(2f, canvas.EffectiveScale);
            Assert.Equal(new Size(400, 300), canvas.SurfaceSize);
            Assert.Equal(new Rectangle(0, 0, 400, 300), canvas.ContentBounds);
            Assert.Equal(new Size(400, 300), root.ActualBounds.Size);
        }

        [Fact]
        public void ScaleModeOverride_None_IgnoresDisplayScale()
        {
            var (canvas, _, _) = Create(displayScale: 2f);
            canvas.ScaleMode = UIScaleMode.None;

            canvas.Render();

            Assert.Equal(1f, canvas.EffectiveScale);
            Assert.Equal(new Size(800, 600), canvas.SurfaceSize);
        }

        [Fact]
        public void ConfigurationChange_IsPickedUpOnNextRender_AndReArrangesRoots()
        {
            var (canvas, _, configuration) = Create(displayScale: 1f);
            UIElement root = Stretch();
            canvas.Add(root);
            canvas.Render();

            configuration.Scaling.UserScale = 2f;
            canvas.Render();

            Assert.Equal(2f, canvas.EffectiveScale);
            Assert.Equal(new Size(400, 300), root.ActualBounds.Size);
        }

        [Fact]
        public void DisplayScaleChange_IsPickedUpOnNextRender()
        {
            var (canvas, context, _) = Create(displayScale: 1f);
            canvas.Render();

            context.DisplayScale = 1.5f;
            canvas.Render();

            Assert.Equal(1.5f, canvas.EffectiveScale);
            Assert.Equal(new Size(533, 400), canvas.SurfaceSize);
        }

        [Fact]
        public void ReferenceResolutionOverride_FitsReference()
        {
            var (canvas, _, _) = Create(displayScale: 2f, viewport: new Size(2880, 1920));
            canvas.ScaleMode = UIScaleMode.ReferenceResolution;
            canvas.ReferenceSize = new Size(1920, 1080);

            canvas.Render();

            Assert.Equal(1.5f, canvas.EffectiveScale, 3);
            Assert.Equal(new Size(1920, 1280), canvas.SurfaceSize);
        }

        [Fact]
        public void EffectiveScaleChange_RaisesPropertyChanged()
        {
            var (canvas, context, _) = Create(displayScale: 1f);
            canvas.Render();
            var changed = new List<string?>();
            canvas.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            context.DisplayScale = 2f;
            canvas.Render();

            Assert.Contains(nameof(Canvas.EffectiveScale), changed);
            Assert.Contains(nameof(Canvas.SurfaceSize), changed);
        }

        [Fact]
        public void ZeroViewport_ReferenceMode_RendersWithoutThrowing()
        {
            var (canvas, _, _) = Create(displayScale: 2f, viewport: Size.Empty);
            canvas.ScaleMode = UIScaleMode.ReferenceResolution;
            canvas.Add(Stretch());

            canvas.Render();

            Assert.Equal(1f, canvas.EffectiveScale);
            Assert.Equal(Size.Empty, canvas.SurfaceSize);
        }

        [Fact]
        public void DiscardedCanvas_IsNotKeptAliveByConfiguration()
        {
            var (_, _, configuration) = Create();
            WeakReference weak = CreateAndDropCanvas(configuration);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weak.IsAlive);
            GC.KeepAlive(configuration);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndDropCanvas(IcyConfiguration configuration)
        {
            var canvas = new Canvas(configuration);
            canvas.Render();
            return new WeakReference(canvas);
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasScalingTests"`
Expected: build FAILS (`FakeRenderContext.DisplayScale`, `Canvas.EffectiveScale` etc. missing).

- [ ] **Step 3: Add the default members to `IRenderContext`**

In `sources/IcyUI/Rendering/IRenderContext.cs`, after the `ViewportResize` event:

```csharp
        /// <summary>
        /// Occurs when <see cref="DisplayScale"/> changes, e.g. when the window moves to a monitor with a different DPI.
        /// </summary>
        /// <remarks>
        /// Provided for application code. <see cref="UI.Canvas"/> doesn't subscribe to it; it re-reads
        /// <see cref="DisplayScale"/> on every <see cref="UI.Canvas.Render"/> call instead.
        /// The default implementation never raises the event.
        /// </remarks>
        event EventHandler? DisplayScaleChanged
        {
            add { }
            remove { }
        }
```

and after `ViewportSize`:

```csharp
        /// <summary>
        /// Gets the operating system display scale of the surface this context renders into.
        /// </summary>
        /// <remarks>
        /// <c>1.0</c> means 96 DPI (100 %), <c>2.0</c> means 192 DPI (200 %). A host process that isn't DPI-aware
        /// always reports <c>1.0</c>, because the OS scales its window itself. The default implementation returns <c>1.0</c>.
        /// </remarks>
        float DisplayScale => 1f;
```

- [ ] **Step 4: Extend `FakeRenderContext`**

In `sources/IcyUI.Tests/Rendering/FakeRenderContext.cs`, after the `ViewportSize` property add:

```csharp
        /// <inheritdoc/>
        public event EventHandler? DisplayScaleChanged;

        /// <inheritdoc/>
        public float DisplayScale { get; set; } = 1f;
```

and after `NotifyViewportResize`:

```csharp
        /// <summary>
        /// Raises <see cref="DisplayScaleChanged"/>, for tests that exercise display-scale change notifications.
        /// </summary>
        public void NotifyDisplayScaleChanged() => DisplayScaleChanged?.Invoke(this, EventArgs.Empty);
```

- [ ] **Step 5: Add scale state to `Canvas`**

In `sources/IcyUI/UI/Canvas.cs`:

1. Add the field block (alphabetical with the existing fields):

```csharp
        private const float ScaleEpsilon = 0.0001f;
        private float effectiveScale = 1f;
        private ReferenceFit? referenceFit;
        private Size? referenceSize;
        private UIScaleMode? scaleMode;
```

(`ScaleEpsilon` goes above the instance fields, per StyleCop ordering: constants first.)

2. Replace the `ContentBounds` property with:

```csharp
        /// <summary>
        /// Gets the bounds of the content area of the canvas, in surface units (see <see cref="SurfaceSize"/>).
        /// </summary>
        public Rectangle ContentBounds => new(Point.Empty, SurfaceSize);
```

3. Add these public members after `ContentBounds`:

```csharp
        /// <summary>
        /// Gets the scale factor between surface units and physical pixels, as computed by the last
        /// <see cref="RefreshScale"/> call.
        /// </summary>
        /// <remarks>
        /// <c>EffectiveScale = base × </c><see cref="ScalingConfiguration.UserScale"/>, where the base factor comes from
        /// <see cref="ScaleMode"/> (or <see cref="ScalingConfiguration.Mode"/> when unset):
        /// <list type="bullet">
        /// <item><description><see cref="UIScaleMode.None"/>: <c>1</c>.</description></item>
        /// <item><description><see cref="UIScaleMode.Dpi"/>: <see cref="IRenderContext.DisplayScale"/>.</description></item>
        /// <item><description><see cref="UIScaleMode.ReferenceResolution"/>: the viewport-to-<see cref="ReferenceSize"/> ratio picked by <see cref="ReferenceFit"/>.</description></item>
        /// </list>
        /// Recomputed at the start of every <see cref="Render"/>. Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> when it changes.
        /// </remarks>
        public float EffectiveScale => effectiveScale;

        /// <summary>
        /// Gets the size of the logical drawing surface: the physical viewport divided by <see cref="EffectiveScale"/>,
        /// rounded down to whole units.
        /// </summary>
        /// <remarks>
        /// Root elements, overlays and the debug HUD are all laid out against this size.
        /// </remarks>
        public Size SurfaceSize
        {
            get
            {
                Size physical = Configuration.RenderContext.ViewportSize;
                return new((int)MathF.Floor(physical.Width / effectiveScale), (int)MathF.Floor(physical.Height / effectiveScale));
            }
        }

        /// <summary>
        /// Gets or sets the scale mode of this canvas, overriding <see cref="ScalingConfiguration.Mode"/>.
        /// <see langword="null"/> (the default) inherits the configuration value.
        /// </summary>
        public UIScaleMode? ScaleMode
        {
            get => scaleMode;
            set
            {
                if (SetProperty(ref scaleMode, value))
                    RefreshScale();
            }
        }

        /// <summary>
        /// Gets or sets the reference resolution of this canvas, overriding <see cref="ScalingConfiguration.ReferenceSize"/>.
        /// <see langword="null"/> (the default) inherits the configuration value.
        /// </summary>
        public Size? ReferenceSize
        {
            get => referenceSize;
            set
            {
                if (SetProperty(ref referenceSize, value))
                    RefreshScale();
            }
        }

        /// <summary>
        /// Gets or sets the reference fit policy of this canvas, overriding <see cref="ScalingConfiguration.ReferenceFit"/>.
        /// <see langword="null"/> (the default) inherits the configuration value.
        /// </summary>
        public ReferenceFit? ReferenceFit
        {
            get => referenceFit;
            set
            {
                if (SetProperty(ref referenceFit, value))
                    RefreshScale();
            }
        }
```

4. Add the public method (next to `InvalidateTransform`):

```csharp
        /// <summary>
        /// Recomputes <see cref="EffectiveScale"/> from the current configuration, display scale, viewport and overrides,
        /// and re-arranges all content if it changed.
        /// </summary>
        /// <remarks>
        /// Called automatically at the start of every <see cref="Render"/> and when an override changes. The canvas
        /// deliberately doesn't subscribe to configuration or render-context events: it has no disposal point, and a
        /// subscription would keep every discarded canvas alive.
        /// </remarks>
        public void RefreshScale()
        {
            ScalingConfiguration scaling = Configuration.Scaling;
            IRenderContext context = Configuration.RenderContext;
            float newScale = UIScaleCalculator.Compute(
                ScaleMode ?? scaling.Mode,
                context.DisplayScale,
                context.ViewportSize,
                ReferenceSize ?? scaling.ReferenceSize,
                ReferenceFit ?? scaling.ReferenceFit,
                scaling.UserScale);

            if (MathF.Abs(newScale - effectiveScale) < ScaleEpsilon)
                return;

            effectiveScale = newScale;
            InvalidateTransform();
            foreach (UIElement element in rootElements)
                element.InvalidateArrange();
            foreach (UIElement overlay in overlayElements)
                overlay.InvalidateArrange();

            OnPropertyChanged(nameof(EffectiveScale));
            OnPropertyChanged(nameof(SurfaceSize));
        }
```

5. In `Render()`, make the first two lines:

```csharp
            frameTime.Stop();
            RefreshScale();
            if (isTransformInvalid)
                UpdateTransform();
```

6. In `UpdateTransform()`, replace `Configuration.RenderContext.ViewportSize.AsVector()` with `SurfaceSize.AsVector()`. (Task 3 rewrites this method fully; this keeps the build green now.)

7. Add `using Icy.Configuration;` if it isn't already present (it is: `Canvas` takes `IcyConfiguration`).

- [ ] **Step 6: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasScalingTests"`
Expected: PASS. Then the full suite: all pass. The fake's default `DisplayScale = 1` keeps every existing canvas test at scale 1.

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/Rendering/IRenderContext.cs sources/IcyUI/UI/Canvas.cs sources/IcyUI.Tests/Rendering/FakeRenderContext.cs sources/IcyUI.Tests/UI/CanvasScalingTests.cs
git commit -m "Add display scale to IRenderContext and effective scale to Canvas" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Surface space — transforms, overlays, hit-testing, popups, debug HUD

**Files:**
- Modify: `sources/IcyUI/UI/Canvas.cs` (`UpdateTransform`, `HitTest`, `RenderVisual`, `UpdateLayout`, new internal helpers)
- Modify: `sources/IcyUI/UI/UIElement.cs` (new `PointToSurface`, next to `PointToScreen`)
- Modify: `sources/IcyUI/UI/Controls/Selector.cs` (`PositionPopup`, ~line 540)
- Modify: `sources/IcyUI/UI/Controls/ColorPickerButton.cs` (`PositionPopup`, ~line 183)
- Modify: `sources/IcyUI/Diagnostics/DebugHudHost.cs` (`Render`, ~line 34)
- Test: `sources/IcyUI.Tests/UI/CanvasSurfaceSpaceTests.cs`

**Interfaces:**
- Consumes: `Canvas.EffectiveScale`, `Canvas.SurfaceSize` (Task 2).
- Produces: `internal Transform2D Canvas.SurfaceTransform { get; }`, `internal Vector2 Canvas.ScreenToSurface(Point screenPoint)`, `internal Vector2 Canvas.CanvasToSurfaceSpace(Vector2 canvasLocalPoint)`, `public Point UIElement.PointToSurface(Vector2 localPoint)`.
- Unchanged meaning: `HitTest(Point)`, `ScreenToCanvasSpace`, `CanvasToScreenSpace`, `PointToLocal`, `PointToScreen` all stay in **physical** pixels.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/UI/CanvasSurfaceSpaceTests.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasSurfaceSpaceTests
    {
        private static UIElement TopLeftBox(int size) => new()
        {
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        [Fact]
        public void HitTest_RootElement_UsesPhysicalPointsAtScale2()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            UIElement box = TopLeftBox(100);
            canvas.Add(box);
            canvas.Render();

            Assert.Same(box, canvas.HitTest(new Point(150, 150)));
            Assert.Null(canvas.HitTest(new Point(250, 250)));
        }

        [Fact]
        public void HitTest_Overlay_UsesSurfaceSpaceAtScale2()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            UIElement overlay = TopLeftBox(100);
            canvas.AddOverlay(overlay);
            canvas.Render();

            Assert.Same(overlay, canvas.HitTest(new Point(150, 150)));
            Assert.Null(canvas.HitTest(new Point(250, 250)));
        }

        [Fact]
        public void Overlay_IsDrawnWithSurfaceTransform()
        {
            var (canvas, context, _) = CanvasScalingTests.Create(displayScale: 2f);
            canvas.AddOverlay(new Icy.UI.Border { Width = 10, Height = 10, Background = new Icy.Rendering.Brushes.SolidColorBrush(Color.Red), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

            canvas.Render();

            var overlayDraw = context.DrawCalls.Last();
            Assert.Equal(new Vector2(2f, 2f), overlayDraw.TransformAtDrawTime.Scale);
        }

        [Fact]
        public void PointToLocal_PointToScreen_RoundTrip_WithDpiAndCanvasTransform()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            canvas.Offset = new Vector2(30, 20);
            canvas.Scale = new Vector2(1.5f, 1.5f);
            UIElement box = TopLeftBox(200);
            canvas.Add(box);
            canvas.Render();

            var physical = new Point(301, 207);
            Vector2 local = box.PointToLocal(physical);
            Point back = box.PointToScreen(local);

            Assert.InRange(back.X, physical.X - 1, physical.X + 1);
            Assert.InRange(back.Y, physical.Y - 1, physical.Y + 1);
            Assert.Same(box, canvas.HitTest(physical));
        }

        [Fact]
        public void PointToSurface_IsPhysicalDividedByScale()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            canvas.Offset = new Vector2(10, 10);
            UIElement box = TopLeftBox(100);
            canvas.Add(box);
            canvas.Render();

            Point screen = box.PointToScreen(new Vector2(40, 40));
            Point surface = box.PointToSurface(new Vector2(40, 40));

            Assert.Equal(new Point(50, 50), surface);
            Assert.Equal(new Point(100, 100), screen);
        }

        [Fact]
        public void Overlay_IsReArrangedInsideNewSurface_WhenScaleChangesWhileOpen()
        {
            var (canvas, _, configuration) = CanvasScalingTests.Create(displayScale: 1f);
            var overlay = new UIElement { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            canvas.AddOverlay(overlay);
            canvas.Render();
            Assert.Equal(new Size(800, 600), overlay.ActualBounds.Size);

            configuration.Scaling.UserScale = 2f;
            canvas.Render();

            Assert.Equal(new Size(400, 300), overlay.ActualBounds.Size);
        }
    }
}
```

(`Border` is `Icy.UI.Border` and draws its `Background` through `IBrush.Draw`; `SolidColorBrush` is `Icy.Rendering.Brushes.SolidColorBrush`.)

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasSurfaceSpaceTests"`
Expected: build FAILS on `PointToSurface`. Comment that test out temporarily to see the others fail on assertions: root hit at (150,150) misses, and the overlay transform scale is (1,1).

- [ ] **Step 3: Rework the canvas transforms**

In `Canvas.cs`:

1. Add fields: `private Transform2D contentTransform;` and `private Transform2D surfaceTransform = Transform2D.Identity;`.

2. Replace `UpdateTransform()` with:

```csharp
        private void UpdateTransform()
        {
            // Content (Offset/Rotation/Scale, in surface units) is applied first, then the surface scale maps to
            // physical pixels. AddTransform(other) applies `other` before the existing matrix.
            contentTransform = Transform2D.Create(Offset, Rotation, TransformOrigin * SurfaceSize.AsVector(), Scale);
            surfaceTransform = Transform2D.Create(Matrix3x2.CreateScale(effectiveScale));
            transform = surfaceTransform;
            transform.AddTransform(contentTransform);
            if (Matrix3x2.Invert(transform.Matrix, out Matrix3x2 inverse))
                inverseTransform = Transform2D.Create(inverse);
            isTransformInvalid = false;
        }
```

3. Add internal helpers next to `ScreenToCanvasSpace`:

```csharp
        /// <summary>
        /// Gets the transform from surface units to physical pixels (a uniform <see cref="EffectiveScale"/> scale).
        /// Overlays and the debug HUD draw with it.
        /// </summary>
        internal Transform2D SurfaceTransform
        {
            get
            {
                if (isTransformInvalid)
                    UpdateTransform();
                return surfaceTransform;
            }
        }

        /// <summary>
        /// Converts a physical screen point (where pointer input arrives) into surface units.
        /// </summary>
        /// <param name="screenPoint">A point in physical screen/window pixels.</param>
        /// <returns>The same point in surface units.</returns>
        internal Vector2 ScreenToSurface(Point screenPoint) => new(screenPoint.X / effectiveScale, screenPoint.Y / effectiveScale);

        /// <summary>
        /// Converts a point in this canvas's local content space into surface units, i.e. applies only the canvas's own
        /// <see cref="Offset"/>/<see cref="Rotation"/>/<see cref="Scale"/>, not the surface scale.
        /// </summary>
        /// <param name="canvasLocalPoint">A point in canvas-local content space.</param>
        /// <returns>The same point in surface units.</returns>
        internal Vector2 CanvasToSurfaceSpace(Vector2 canvasLocalPoint)
        {
            if (isTransformInvalid)
                UpdateTransform();
            return contentTransform.Apply(canvasLocalPoint);
        }
```

4. In `HitTest(Point screenPoint)`, replace the overlay loop's argument:

```csharp
            Vector2 surfacePoint = ScreenToSurface(screenPoint);
            for (int i = overlayElements.Count - 1; i >= 0; i--)
            {
                UIElement? hit = overlayElements[i].HitTest(surfacePoint);
                if (hit != null)
                    return hit;
            }
```

Update the comment above the loop to say that overlays are tested in surface space (physical point ÷ `EffectiveScale`).

5. In `RenderVisual()`, in the overlay block, replace `context.Transform = Transform2D.Identity;` with `context.Transform = surfaceTransform;`. Keep the scissor as the physical viewport. Update the comment: overlays are drawn in surface space, not part of the content transform.

6. In `UpdateLayout()`, replace `var viewport = new Rectangle(Point.Empty, Configuration.RenderContext.ViewportSize);` with `var viewport = new Rectangle(Point.Empty, SurfaceSize);`.

- [ ] **Step 4: Add `UIElement.PointToSurface`**

In `UIElement.cs`, directly after `PointToScreen`:

```csharp
        /// <summary>
        /// Converts a point in this element's own local space into the canvas's surface space - the logical,
        /// <see cref="UI.Canvas.EffectiveScale"/>-independent coordinates that overlays and popups are laid out in.
        /// </summary>
        /// <param name="localPoint">A point in this element's own local space.</param>
        /// <returns>
        /// The equivalent point in surface units, or <see cref="Point.Empty"/> if this element isn't currently attached
        /// to a <see cref="UI.Canvas"/>.
        /// </returns>
        /// <remarks>
        /// Equals <see cref="PointToScreen(Vector2)"/> divided by <see cref="UI.Canvas.EffectiveScale"/>. Use it to position
        /// overlay content (dropdowns, popups) relative to this element; use <see cref="PointToScreen(Vector2)"/> when
        /// comparing against raw pointer positions.
        /// </remarks>
        public Point PointToSurface(Vector2 localPoint)
        {
            if (Canvas == null)
                return Point.Empty;

            Vector2 point = localPoint;
            for (UIElement? element = this; element != null; element = element.Parent)
            {
                if (element.IsTransformInvalid)
                    element.UpdateTransformMatrix();
                point = element.layoutTransform.Apply(point);
            }

            Vector2 surfacePoint = Canvas.CanvasToSurfaceSpace(point);
            return new Point((int)surfacePoint.X, (int)surfacePoint.Y);
        }
```

- [ ] **Step 5: Switch the popups and the debug HUD to the surface**

In `Selector.PositionPopup()` and in `ColorPickerButton.PositionPopup()`, replace:

```csharp
            Point topLeft = PointToScreen(Vector2.Zero);
            Point bottomLeft = PointToScreen(new Vector2(0, ActualBounds.Height));
            Size viewport = Canvas.Configuration.RenderContext.ViewportSize;
```

with:

```csharp
            // Popups are overlays, which live in surface space (see Canvas.SurfaceSize), not physical pixels.
            Point topLeft = PointToSurface(Vector2.Zero);
            Point bottomLeft = PointToSurface(new Vector2(0, ActualBounds.Height));
            Size viewport = Canvas.SurfaceSize;
```

In `DebugHudHost.Render`, replace:

```csharp
            root.Arrange(new Rectangle(Point.Empty, context.ViewportSize));
```

with `root.Arrange(new Rectangle(Point.Empty, canvas.SurfaceSize));`. Replace `context.Transform = Transform2D.Identity;` with `context.Transform = canvas.SurfaceTransform;`, and keep the scissor line as is (physical). Update the adjacent comment to say the HUD is drawn in surface space, so it scales with the UI.

- [ ] **Step 6: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: all pass, including `CanvasSurfaceSpaceTests`, the existing `CanvasOverlayTests`, `CanvasHitTestFocusTests.PointToScreen_IsTheInverseOfPointToLocal`, `ComboBoxTests` and `ColorPickerButtonTests` (all at scale 1, unchanged behavior).

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Canvas.cs sources/IcyUI/UI/UIElement.cs sources/IcyUI/UI/Controls/Selector.cs sources/IcyUI/UI/Controls/ColorPickerButton.cs sources/IcyUI/Diagnostics/DebugHudHost.cs sources/IcyUI.Tests/UI/CanvasSurfaceSpaceTests.cs
git commit -m "Lay out overlays, popups and the debug HUD in canvas surface space" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Platform display-scale detection in core (`Icy.Rendering.Display`)

**Files:**
- Create in `sources/IcyUI/Rendering/Display/`: `NativeWindowInfo.cs`, `IDisplayScaleProvider.cs`, `WindowsDisplayScaleProvider.cs`, `DrawableRatioDisplayScaleProvider.cs`, `FixedDisplayScaleProvider.cs`, `DisplayScales.cs`, `DisplayScaleTracker.cs`
- Test: `sources/IcyUI.Tests/Rendering/DisplayScaleTests.cs`

**Interfaces:**
- Produces: `public readonly record struct NativeWindowInfo(nint Handle, Size WindowSize, Size DrawableSize)`.
- Produces: `public interface IDisplayScaleProvider { float GetScale(in NativeWindowInfo window); }`.
- Produces: `public sealed class FixedDisplayScaleProvider(float scale = 1f)`.
- Produces: `public static class DisplayScales` with `GetPlatformProvider()`, `TryGetPlatformProvider(out IDisplayScaleProvider?)`, `GetProvider()`.
- Produces: `public sealed class DisplayScaleTracker(IDisplayScaleProvider provider, Func<NativeWindowInfo> windowSource)` with `float Scale`, `event EventHandler? Changed`, `bool Poll()`.
- Internal: `WindowsDisplayScaleProvider`, `DrawableRatioDisplayScaleProvider`.

This mirrors `Icy.Input.Clipboard` (`Clipboards` / `IClipboard` / `WindowsClipboard` / `VirtualClipboard`).

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Rendering/DisplayScaleTests.cs`:

```csharp
using System.Drawing;
using Icy.Rendering.Display;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class DisplayScaleTests
    {
        private sealed class SequenceProvider(params float[] values) : IDisplayScaleProvider
        {
            private int index;

            public float GetScale(in NativeWindowInfo window) => values[Math.Min(index++, values.Length - 1)];
        }

        [Fact]
        public void Tracker_RaisesChangedOnlyWhenValueChanges()
        {
            var tracker = new DisplayScaleTracker(new SequenceProvider(2f, 2f, 1.5f), () => default);
            int raised = 0;
            tracker.Changed += (_, _) => raised++;

            Assert.True(tracker.Poll());
            Assert.False(tracker.Poll());
            Assert.True(tracker.Poll());

            Assert.Equal(2, raised);
            Assert.Equal(1.5f, tracker.Scale);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Tracker_TreatsInvalidProviderValuesAsOne(float value)
        {
            var tracker = new DisplayScaleTracker(new SequenceProvider(2f, value), () => default);
            tracker.Poll();

            tracker.Poll();

            Assert.Equal(1f, tracker.Scale);
        }

        [Fact]
        public void Tracker_StartsAtOneBeforeFirstPoll() =>
            Assert.Equal(1f, new DisplayScaleTracker(new FixedDisplayScaleProvider(2f), () => default).Scale);

        [Fact]
        public void FixedProvider_ReturnsItsValue() =>
            Assert.Equal(1.25f, new FixedDisplayScaleProvider(1.25f).GetScale(default));

        [Theory]
        [InlineData(0f)]
        [InlineData(float.NaN)]
        public void FixedProvider_RejectsInvalidScale(float value) =>
            Assert.ThrowsAny<ArgumentException>(() => new FixedDisplayScaleProvider(value));

        [Fact]
        public void GetProvider_NeverReturnsNull() => Assert.NotNull(DisplayScales.GetProvider());

        [Fact]
        public void DrawableRatioProvider_UsesDrawableToWindowRatio()
        {
            var provider = new DrawableRatioDisplayScaleProvider();

            Assert.Equal(2f, provider.GetScale(new NativeWindowInfo(0, new Size(800, 600), new Size(1600, 1200))));
            Assert.Equal(1f, provider.GetScale(new NativeWindowInfo(0, Size.Empty, new Size(1600, 1200))));
        }

        [Fact]
        public void WindowsProvider_WithoutHandle_FallsBackToSystemDpi()
        {
            if (!OperatingSystem.IsWindows())
                return;

            float scale = new WindowsDisplayScaleProvider().GetScale(default);

            Assert.True(scale >= 1f, $"System DPI scale was {scale}");
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DisplayScaleTests"`
Expected: build FAILS (namespace `Icy.Rendering.Display` doesn't exist).

- [ ] **Step 3: Implement the namespace**

`NativeWindowInfo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Describes the native window an engine renders into - the only data an engine integration supplies for display-scale detection.
    /// </summary>
    /// <param name="Handle">
    /// The native window handle: an <c>HWND</c> on Windows, or <c>0</c> when unavailable (Windows then falls back to the system DPI).
    /// Other platforms ignore it.
    /// </param>
    /// <param name="WindowSize">The window client size in OS window units (points on macOS).</param>
    /// <param name="DrawableSize">The back-buffer size in physical pixels.</param>
    public readonly record struct NativeWindowInfo(nint Handle, Size WindowSize, Size DrawableSize);
}
```

`IDisplayScaleProvider.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// Represents a platform-specific way to read the display scale of a native window.
    /// </summary>
    /// <remarks>
    /// Obtain the implementation for the current platform from <see cref="DisplayScales.GetProvider"/>.
    /// </remarks>
    public interface IDisplayScaleProvider
    {
        /// <summary>
        /// Gets the display scale of the specified window, where <c>1.0</c> means 96 DPI (100 %).
        /// </summary>
        /// <param name="window">The native window to query.</param>
        /// <returns>The display scale. Implementations must not throw; they return <c>1.0</c> when the scale can't be determined.</returns>
        float GetScale(in NativeWindowInfo window);
    }
}
```

`WindowsDisplayScaleProvider.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.InteropServices;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Reads the display scale on Windows through <c>GetDpiForWindow</c>, falling back to <c>GetDpiForSystem</c>.
    /// </summary>
    /// <remarks>
    /// Both functions respect the calling process's DPI awareness: a process that isn't DPI-aware always gets 96 DPI
    /// (scale <c>1.0</c>) while Windows stretches its window, so OS scaling and IcyUI scaling never stack.
    /// </remarks>
    internal sealed partial class WindowsDisplayScaleProvider : IDisplayScaleProvider
    {
        private const float DefaultDpi = 96f;

        /// <inheritdoc/>
        public float GetScale(in NativeWindowInfo window)
        {
            uint dpi = window.Handle != 0 ? GetDpiForWindow(window.Handle) : 0;
            if (dpi == 0)
                dpi = GetDpiForSystem();
            return dpi == 0 ? 1f : dpi / DefaultDpi;
        }

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForWindow(nint hwnd);

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForSystem();
    }
}
```

`DrawableRatioDisplayScaleProvider.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// Derives the display scale from the ratio of the drawable (back-buffer) width to the window width, as on macOS
    /// Retina displays and Linux compositors that expose high-DPI drawables.
    /// </summary>
    internal sealed class DrawableRatioDisplayScaleProvider : IDisplayScaleProvider
    {
        /// <inheritdoc/>
        public float GetScale(in NativeWindowInfo window) =>
            window.WindowSize.Width > 0 && window.DrawableSize.Width > 0
                ? (float)window.DrawableSize.Width / window.WindowSize.Width
                : 1f;
    }
}
```

`FixedDisplayScaleProvider.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// An <see cref="IDisplayScaleProvider"/> that always reports the same scale.
    /// </summary>
    /// <remarks>
    /// Used as the fallback on platforms without a dedicated provider (see <see cref="DisplayScales.GetProvider"/>), and
    /// useful for tests or for forcing a scale.
    /// </remarks>
    public sealed class FixedDisplayScaleProvider : IDisplayScaleProvider
    {
        private readonly float scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedDisplayScaleProvider"/> class.
        /// </summary>
        /// <param name="scale">The scale to report. Defaults to <c>1.0</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is not a finite number greater than zero.</exception>
        public FixedDisplayScaleProvider(float scale = 1f)
        {
            if (!float.IsFinite(scale) || scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "The display scale must be a finite number greater than zero.");
            this.scale = scale;
        }

        /// <inheritdoc/>
        public float GetScale(in NativeWindowInfo window) => scale;
    }
}
```

`DisplayScales.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Provides static methods to get the display-scale provider for the current platform.
    /// </summary>
    public static class DisplayScales
    {
        /// <summary>
        /// Gets the display-scale provider for the current platform.
        /// </summary>
        /// <returns>An instance of the <see cref="IDisplayScaleProvider"/> interface for the current platform.</returns>
        /// <exception cref="PlatformNotSupportedException">The current platform has no display-scale provider.</exception>
        public static IDisplayScaleProvider GetPlatformProvider()
        {
            if (!TryGetPlatformProvider(out var provider))
                throw new PlatformNotSupportedException();
            return provider;
        }

        /// <summary>
        /// Tries to get the display-scale provider for the current platform.
        /// </summary>
        /// <param name="provider">The provider for the current platform, or <see langword="null"/> if there is none.</param>
        /// <returns><see langword="true"/> if the current platform has a provider; otherwise <see langword="false"/>.</returns>
        public static bool TryGetPlatformProvider([NotNullWhen(true)] out IDisplayScaleProvider? provider)
        {
            provider = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                provider = new WindowsDisplayScaleProvider();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                provider = new DrawableRatioDisplayScaleProvider();

            return provider != null;
        }

        /// <summary>
        /// Gets the display-scale provider for the current platform, or a <see cref="FixedDisplayScaleProvider"/>
        /// reporting <c>1.0</c> when the platform has none.
        /// </summary>
        /// <returns>A display-scale provider; never <see langword="null"/>.</returns>
        public static IDisplayScaleProvider GetProvider() =>
            TryGetPlatformProvider(out var provider) ? provider : new FixedDisplayScaleProvider();
    }
}
```

`DisplayScaleTracker.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// Tracks the display scale of a native window by polling an <see cref="IDisplayScaleProvider"/>.
    /// </summary>
    /// <remarks>
    /// Engine integrations create one per render context and call <see cref="Poll"/> once per update. Polling is a
    /// single cheap OS call and avoids competing with the engine for its window event queue.
    /// </remarks>
    public sealed class DisplayScaleTracker
    {
        private const float Epsilon = 0.001f;
        private readonly IDisplayScaleProvider provider;
        private readonly Func<NativeWindowInfo> windowSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayScaleTracker"/> class.
        /// </summary>
        /// <param name="provider">The provider used to read the scale, typically <see cref="DisplayScales.GetProvider"/>.</param>
        /// <param name="windowSource">A callback returning the current native window info. It must not throw.</param>
        public DisplayScaleTracker(IDisplayScaleProvider provider, Func<NativeWindowInfo> windowSource)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(windowSource);
            this.provider = provider;
            this.windowSource = windowSource;
        }

        /// <summary>
        /// Occurs when <see cref="Scale"/> changes during a <see cref="Poll"/> call.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets the display scale read by the last <see cref="Poll"/> call; <c>1.0</c> before the first poll.
        /// </summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>
        /// Reads the current display scale and raises <see cref="Changed"/> if it differs from <see cref="Scale"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the scale changed; otherwise <see langword="false"/>.</returns>
        /// <remarks>Values that aren't finite and greater than zero are treated as <c>1.0</c>.</remarks>
        public bool Poll()
        {
            float value = provider.GetScale(windowSource());
            if (!float.IsFinite(value) || value <= 0)
                value = 1f;

            if (MathF.Abs(value - Scale) < Epsilon)
                return false;

            Scale = value;
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DisplayScaleTests"`
Expected: PASS (on this machine, `WindowsProvider_WithoutHandle_FallsBackToSystemDpi` reads ≥ 1). `IcyUI` already sets `AllowUnsafeBlocks`, which `LibraryImport` requires.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/Rendering/Display sources/IcyUI.Tests/Rendering/DisplayScaleTests.cs
git commit -m "Add core display-scale detection (Icy.Rendering.Display)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Crisp text through device-resolution glyphs

**Files:**
- Modify: `sources/IcyUI/Rendering/Fonts/SpriteFont.cs` (`DrawString(IRenderContext, ReadOnlySpan<char>, in FontRenderingOptions)`, new helpers)
- Modify: `sources/IcyUI/Rendering/Fonts/DynamicSpriteFont.cs` (`GetGlyph`, new override)
- Test: `sources/IcyUI.Tests/Rendering/DeviceGlyphTests.cs`

**Interfaces:**
- Produces: `internal static float SpriteFont.GetDeviceScale(in Transform2D transform)`. It returns `1` (meaning "no device path") or a scale quantized to 0.25 steps.
- Produces: `protected virtual bool SpriteFont.TryGetDeviceGlyph(int codepoint, float deviceScale, out FontGlyph glyph)` (base returns `false`), overridden in `DynamicSpriteFont`.

**How it works:**
- When `context.Transform` has a uniform, unrotated scale `s` with `|s − 1| ≥ 0.01`, and the text itself is unrotated and unscaled, `DrawString` temporarily sets `context.Transform = Scale(1/q) × outer`, where `q` is `s` rounded to 0.25.
- It then emits each glyph as an integer rectangle in *device units*: `pen × q + deviceBearing`, sized to the device glyph bitmap. The device glyph is rasterized at `Info.Size × q`.
- Pen positions (advance, kerning, line height, baseline) still come from the logical font, so drawn width equals measured width.
- Fallback-font glyphs and static bitmap fonts keep the logical bitmap, with their rectangle multiplied by `q`.
- `context.Transform` is always restored.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Rendering/DeviceGlyphTests.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.Configuration;
using Icy.Rendering;
using Icy.Rendering.Fonts;
using Icy.Tests.Input;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class DeviceGlyphTests
    {
        private static (SpriteFont Font, FakeRenderContext Context) LoadFont()
        {
            var context = new FakeRenderContext();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(context).ConfigureInput(new FakeInputSystem()).ConfigureTypes().ConfigureAssets().AddBasicFontSupport();
            IcyConfiguration configuration = builder.Build();
            configuration.Fonts.ImportFont(configuration.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            var font = (SpriteFont)configuration.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular))!;
            return (font, context);
        }

        private static FontRenderingOptions Options() => default(FontRenderingOptions) with { Color = Color.White };

        private static List<Rectangle> Draw(SpriteFont font, FakeRenderContext context, float scale, string text)
        {
            context.DrawCalls.Clear();
            context.Transform = Transform2D.Create(Matrix3x2.CreateScale(scale));
            font.DrawString(context, text, Options());
            return context.DrawCalls.Select(call => call.Options.Destination).ToList();
        }

        [Theory]
        [InlineData(1f, 1f)]
        [InlineData(1.005f, 1f)]
        [InlineData(1.37f, 1.25f)]
        [InlineData(1.5f, 1.5f)]
        [InlineData(2f, 2f)]
        public void GetDeviceScale_QuantizesUniformScale(float scale, float expected) =>
            Assert.Equal(expected, SpriteFont.GetDeviceScale(Transform2D.Create(Matrix3x2.CreateScale(scale))));

        [Fact]
        public void GetDeviceScale_NonUniformOrRotated_ReturnsOne()
        {
            Assert.Equal(1f, SpriteFont.GetDeviceScale(Transform2D.Create(Matrix3x2.CreateScale(2f, 1f))));
            Assert.Equal(1f, SpriteFont.GetDeviceScale(Transform2D.Create(Matrix3x2.CreateScale(2f) * Matrix3x2.CreateRotation(0.3f))));
        }

        [Fact]
        public void DrawString_AtScale_UsesDeviceGlyphsAtLogicalPenPositions()
        {
            var (font, context) = LoadFont();
            List<Rectangle> logical = Draw(font, context, 1f, "Hello, World");
            List<Rectangle> device = Draw(font, context, 1.5f, "Hello, World");

            Assert.Equal(logical.Count, device.Count);
            for (int i = 0; i < logical.Count; i++)
            {
                // Same pen position (device units ÷ 1.5 ≈ logical units), but a bitmap rasterized at 1.5× the size.
                Assert.InRange(device[i].X / 1.5f, logical[i].X - 1.5f, logical[i].X + 1.5f);
                Assert.InRange(device[i].Height, (logical[i].Height * 1.5f) - 3, (logical[i].Height * 1.5f) + 3);
            }
        }

        [Fact]
        public void DrawString_AtScale_DrawsWithCompensatedTransform_AndRestoresIt()
        {
            var (font, context) = LoadFont();
            Draw(font, context, 2f, "Hi");

            Assert.All(context.DrawCalls, call => Assert.Equal(1f, call.TransformAtDrawTime.Scale.X, 3));
            Assert.Equal(2f, context.Transform.Scale.X, 3);
        }

        [Fact]
        public void DrawString_MultiLine_KeepsLinePositionsAtScale()
        {
            var (font, context) = LoadFont();
            List<Rectangle> logical = Draw(font, context, 1f, "A\nA\nA");
            List<Rectangle> device = Draw(font, context, 1.5f, "A\nA\nA");

            Assert.Equal(3, device.Count);
            for (int i = 0; i < 3; i++)
            {
                float deviceTop = device[i].Y / 1.5f;
                Assert.InRange(deviceTop, logical[i].Y - 1.5f, logical[i].Y + 1.5f);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DeviceGlyphTests"`
Expected: build FAILS (`SpriteFont.GetDeviceScale` doesn't exist).

- [ ] **Step 3: Add the device path to `SpriteFont`**

In `SpriteFont.cs`, add after `GetGlyphTexture`:

```csharp
        /// <summary>
        /// Gets the uniform device scale to rasterize glyphs at for the specified render transform.
        /// </summary>
        /// <param name="transform">The render context's current transform.</param>
        /// <returns>
        /// <c>1</c> when glyphs should be drawn at their logical size (scale within 1 % of 1, non-uniform, rotated or
        /// degenerate); otherwise the scale rounded to the nearest 0.25, which keeps animated scales from filling the
        /// atlas with many slightly different sizes.
        /// </returns>
        internal static float GetDeviceScale(in Transform2D transform)
        {
            Vector2 scale = transform.Scale;
            if (MathF.Abs(transform.Rotation) > 0.0001f || scale.X <= 0 || MathF.Abs(scale.X - scale.Y) > 0.01f)
                return 1f;
            if (MathF.Abs(scale.X - 1f) < 0.01f)
                return 1f;

            float quantized = MathF.Round(scale.X * 4f, MidpointRounding.AwayFromZero) / 4f;
            return quantized < 0.25f ? 1f : quantized;
        }

        /// <summary>
        /// Tries to get a glyph rasterized for the specified device scale, so text drawn under a scaling transform
        /// stays crisp.
        /// </summary>
        /// <param name="codepoint">The codepoint to get the glyph for.</param>
        /// <param name="deviceScale">The device scale, as returned by <see cref="GetDeviceScale(in Transform2D)"/>.</param>
        /// <param name="glyph">The device-resolution glyph, with its size, bearing and texture region in device pixels.</param>
        /// <returns>
        /// <see langword="true"/> if the font can rasterize at arbitrary sizes and produced a glyph; otherwise <see langword="false"/>.
        /// The base implementation returns <see langword="false"/> (bitmap fonts are simply scaled).
        /// </returns>
        protected virtual bool TryGetDeviceGlyph(int codepoint, float deviceScale, out FontGlyph glyph)
        {
            glyph = FontGlyph.None;
            return false;
        }
```

Replace the body of `DrawString(IRenderContext context, ReadOnlySpan<char> text, in FontRenderingOptions options)` with:

```csharp
            if (text.IsEmpty)
                return;

            var localOptions = options;
            Transform2D transform = CreateTransform(localOptions);
            Prepare(text, localOptions, out int baseline, out int lineHeight);
            BoundsInfo renderBounds = new(new Vector2(0, baseline), localOptions.Position.X);

            // Under a scaling render transform (e.g. a Canvas at 200 % DPI), draw glyphs rasterized at the device size
            // in device units, under a transform compensated by 1/deviceScale. Pen positions still come from this
            // (logical) font, so drawn text keeps exactly its measured width.
            float deviceScale = GetDeviceScale(context.Transform);
            bool useDevice = deviceScale != 1f && IsTranslationOnly(localOptions);
            Transform2D outerTransform = context.Transform;
            if (useDevice)
                context.Transform = Transform2D.Create(Matrix3x2.CreateScale(1f / deviceScale) * outerTransform.Matrix);

            try
            {
                ProcessText(text, localOptions, lineHeight, ref renderBounds, (glyph, font, glyphPos) =>
                {
                    if (glyph.IsEmpty)
                        return;

                    if (useDevice && font == null && TryGetDeviceGlyph(glyph.Codepoint, deviceScale, out FontGlyph deviceGlyph) && !deviceGlyph.IsEmpty)
                    {
                        Vector2 pen = transform.Apply(glyphPos - glyph.Bearing) * deviceScale;
                        Rectangle deviceBounds = new(
                            (int)MathF.Round(pen.X + deviceGlyph.Bearing.X),
                            (int)MathF.Round(pen.Y + deviceGlyph.Bearing.Y),
                            deviceGlyph.Size.Width,
                            deviceGlyph.Size.Height);
                        context.Draw(GetGlyphTexture(deviceGlyph), new TextureRenderingOptions(deviceBounds, deviceGlyph.TextureRegion, localOptions.Color, 0f, Vector2.Zero, localOptions.Depth));
                        return;
                    }

                    Rectangle glyphBounds = new((int)glyphPos.X, (int)glyphPos.Y, glyph.Size.Width, glyph.Size.Height);
                    Rectangle renderGlyphBounds = transform.Apply(glyphBounds);
                    if (useDevice)
                        renderGlyphBounds = ScaleRectangle(renderGlyphBounds, deviceScale);

                    var glyphTexture = ((font as SpriteFont) ?? this)?.GetGlyphTexture(glyph);

                    // Unfortunately, we can't support fonts that can't provide a texture for the specified glyph.
                    if (glyphTexture == null)
                        return;

                    TextureRenderingOptions renderOptions = new(
                        Destination: renderGlyphBounds,
                        Source: glyph.TextureRegion,
                        Color: localOptions.Color,
                        Rotation: localOptions.Rotation,
                        Origin: localOptions.Origin,
                        Depth: localOptions.Depth);

                    context.Draw(glyphTexture, renderOptions);
                });
            }
            finally
            {
                context.Transform = outerTransform;
            }
```

Add the two private helpers next to `CreateTransform`:

```csharp
        private static Rectangle ScaleRectangle(Rectangle rectangle, float scale) => Rectangle.FromLTRB(
            (int)MathF.Round(rectangle.Left * scale),
            (int)MathF.Round(rectangle.Top * scale),
            (int)MathF.Round(rectangle.Right * scale),
            (int)MathF.Round(rectangle.Bottom * scale));

        private bool IsTranslationOnly(in FontRenderingOptions options) =>
            options.Rotation == 0 && (options.Scale ?? Vector2.One) == Vector2.One && RenderSizeMultiplier == 1f;
```

(`ProcessText` runs synchronously, so the lambda capturing `context`/`localOptions` is fine, exactly as before. Check the file's `using`s include `System.Numerics` for `Matrix3x2`; it already uses `Vector2`.)

- [ ] **Step 4: Rasterize device-size glyphs in `DynamicSpriteFont`**

In `DynamicSpriteFont.cs`, replace `GetGlyph(int codepoint)` with a size-parameterized version plus the override:

```csharp
        /// <inheritdoc/>
        public override FontGlyph GetGlyph(int codepoint) => GetGlyph(codepoint, Info);

        /// <inheritdoc/>
        /// <remarks>
        /// Rasterizes at <c><see cref="FontInfo.Size"/> × <paramref name="deviceScale"/></c> into the same shared atlas,
        /// which keys glyphs by size, so device-size glyphs are cached alongside the logical ones.
        /// </remarks>
        protected override bool TryGetDeviceGlyph(int codepoint, float deviceScale, out FontGlyph glyph)
        {
            glyph = GetGlyph(codepoint, Info.WithSize(Info.Size * deviceScale));
            return glyph != FontGlyph.None;
        }

        private FontGlyph GetGlyph(int codepoint, FontInfo sized)
        {
            // Check atlas first
            if (atlas.GetGlyph(new StyledGlyphDefinition(codepoint, sized.Size, sized.Style)) is FontGlyph glyph)
                return glyph;

            // Get glyph metrics and rasterize
            var metrics = rasterizer.GetGlyphMetrics(codepoint, sized.Size, sized.Style);
            if (metrics.IsEmpty)
            {
                if (metrics.Advance != 0)
                {
                    atlas.AddEmptyGlyph(metrics, sized);
                    return metrics;
                }

                return FontGlyph.None;
            }

            using var pixels = rasterizer.RasterizeGlyph(codepoint, sized.Size, sized.Style);
            if (pixels is null)
                return FontGlyph.None;

            return atlas.TryAddGlyph(metrics, pixels.Memory, sized);
        }
```

(The logical path is behavior-identical: `GetStyledGlyph(codepoint)` was `new(codepoint, Info.Size, Info.Style)`.)

- [ ] **Step 5: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: all pass. Existing font and text tests are unaffected because `FakeRenderContext.Transform` defaults to a zero matrix (scale 0 → `GetDeviceScale` returns 1) or identity.

If `DrawString_AtScale_UsesDeviceGlyphsAtLogicalPenPositions` fails only on the height tolerance for one glyph, print both lists and check the rasterizer's hinting before widening the tolerance. Don't change the pen math.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/Rendering/Fonts/SpriteFont.cs sources/IcyUI/Rendering/Fonts/DynamicSpriteFont.cs sources/IcyUI.Tests/Rendering/DeviceGlyphTests.cs
git commit -m "Rasterize dynamic-font glyphs at device resolution under scaling transforms" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Edge-based pixel snapping (core helper + both engines)

**Files:**
- Create: `sources/IcyUI/Rendering/PixelSnapping.cs`
- Modify: `sources/IcyUI.MonoGame/Rendering/RenderContext.cs` (`Draw`)
- Modify: `sources/IcyUI.Stride/Rendering/RenderContext.cs` (`Draw`)
- Test: `sources/IcyUI.Tests/Rendering/PixelSnappingTests.cs`

**Interfaces:**
- Produces: `public static bool PixelSnapping.TrySnap(in Transform2D transform, Rectangle destination, float rotation, Vector2 origin, out Vector2 position, out Vector2 size)`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Rendering/PixelSnappingTests.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.Rendering;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class PixelSnappingTests
    {
        private static Transform2D ScaleBy(float s) => Transform2D.Create(Matrix3x2.CreateScale(s));

        [Fact]
        public void TrySnap_RoundsEdgesToWholePixels()
        {
            Assert.True(PixelSnapping.TrySnap(ScaleBy(1.5f), new Rectangle(1, 1, 3, 3), 0f, Vector2.Zero, out Vector2 position, out Vector2 size));

            Assert.Equal(new Vector2(2f, 2f), position); // 1.5 → 2
            Assert.Equal(new Vector2(4f, 4f), size);     // right edge 6 → 6
        }

        [Fact]
        public void TrySnap_SharedEdgesStayShared_At125Percent()
        {
            var transform = ScaleBy(1.25f);
            for (int x = 0; x < 20; x++)
            {
                PixelSnapping.TrySnap(transform, new Rectangle(x, 0, 1, 1), 0f, Vector2.Zero, out Vector2 leftPos, out Vector2 leftSize);
                PixelSnapping.TrySnap(transform, new Rectangle(x + 1, 0, 1, 1), 0f, Vector2.Zero, out Vector2 rightPos, out _);

                Assert.Equal(leftPos.X + leftSize.X, rightPos.X);
            }
        }

        [Fact]
        public void TrySnap_Rotated_DoesNotSnap()
        {
            Assert.False(PixelSnapping.TrySnap(ScaleBy(1f), new Rectangle(0, 0, 5, 5), 0.5f, Vector2.Zero, out _, out _));
            Assert.False(PixelSnapping.TrySnap(Transform2D.Create(Matrix3x2.CreateRotation(0.5f)), new Rectangle(0, 0, 5, 5), 0f, Vector2.Zero, out _, out _));
        }

        [Fact]
        public void TrySnap_NonZeroOrigin_DoesNotSnap() =>
            Assert.False(PixelSnapping.TrySnap(ScaleBy(1f), new Rectangle(0, 0, 5, 5), 0f, new Vector2(2, 2), out _, out _));

        [Fact]
        public void TrySnap_IdentityIntegerRect_IsUnchanged()
        {
            Assert.True(PixelSnapping.TrySnap(ScaleBy(1f), new Rectangle(3, 4, 10, 20), 0f, Vector2.Zero, out Vector2 position, out Vector2 size));

            Assert.Equal(new Vector2(3, 4), position);
            Assert.Equal(new Vector2(10, 20), size);
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PixelSnappingTests"`
Expected: build FAILS (`PixelSnapping` missing).

- [ ] **Step 3: Implement the helper**

`sources/IcyUI/Rendering/PixelSnapping.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Provides pixel snapping for axis-aligned quads, shared by every engine's <see cref="IRenderContext.Draw"/> implementation.
    /// </summary>
    /// <remarks>
    /// Snapping rounds the quad's <em>edges</em> (left/top and right/bottom) to whole physical pixels, rather than its position
    /// and size separately. Two quads that share an edge in logical units therefore share it in physical pixels at any
    /// scale (e.g. 125 % or 150 %), so no seams or 1-pixel gaps appear, and linearly sampled textures aren't blurred by
    /// sub-pixel offsets.
    /// </remarks>
    public static class PixelSnapping
    {
        private const float RotationEpsilon = 0.0001f;

        /// <summary>
        /// Tries to snap a destination rectangle, transformed by <paramref name="transform"/>, to whole physical pixels.
        /// </summary>
        /// <param name="transform">The render context's current transform.</param>
        /// <param name="destination">The destination rectangle before <paramref name="transform"/> is applied.</param>
        /// <param name="rotation">The per-draw rotation in radians, added to the transform's own rotation.</param>
        /// <param name="origin">The per-draw origin; snapping only applies when it is <see cref="Vector2.Zero"/>.</param>
        /// <param name="position">The snapped top-left corner in physical pixels.</param>
        /// <param name="size">The snapped size in physical pixels.</param>
        /// <returns>
        /// <see langword="true"/> if the quad is axis-aligned with a positive scale and was snapped; <see langword="false"/>
        /// (and default outputs) when it is rotated, mirrored or has a non-zero origin, in which case the caller draws it unsnapped.
        /// </returns>
        public static bool TrySnap(in Transform2D transform, Rectangle destination, float rotation, Vector2 origin, out Vector2 position, out Vector2 size)
        {
            position = default;
            size = default;
            Vector2 scale = transform.Scale;
            if (MathF.Abs(rotation + transform.Rotation) > RotationEpsilon || origin != Vector2.Zero || scale.X <= 0 || scale.Y <= 0)
                return false;

            Vector2 topLeft = Round(transform.Apply(new Vector2(destination.Left, destination.Top)));
            Vector2 bottomRight = Round(transform.Apply(new Vector2(destination.Right, destination.Bottom)));
            position = topLeft;
            size = bottomRight - topLeft;
            return true;
        }

        private static Vector2 Round(Vector2 value) => new(
            MathF.Round(value.X, MidpointRounding.AwayFromZero),
            MathF.Round(value.Y, MidpointRounding.AwayFromZero));
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PixelSnappingTests"`
Expected: PASS.

- [ ] **Step 5: Use the helper in both engines**

MonoGame (`sources/IcyUI.MonoGame/Rendering/RenderContext.cs`, `Draw`). After `size` is computed and before `var pos = ...`, insert:

```csharp
            if (PixelSnapping.TrySnap(Transform, options.Destination, options.Rotation, options.Origin, out System.Numerics.Vector2 snappedPosition, out System.Numerics.Vector2 snappedSize))
            {
                spriteBatch.Draw(
                    tex,
                    snappedPosition,
                    options.Source?.AsEngineRectangle(),
                    options.Color.AsEngineColor() * Options.Opacity,
                    0f,
                    Vector2.Zero,
                    new Vector2(snappedSize.X / size.X, snappedSize.Y / size.Y),
                    SpriteEffects.None,
                    options.Depth);
                return;
            }
```

(`Vector2` in this file is `Microsoft.Xna.Framework.Vector2`. Passing a `System.Numerics.Vector2` position relies on the same implicit conversion the existing `Transform.Apply(...)` argument uses. Ensure `using Icy.Rendering;` is present.)

Stride (`sources/IcyUI.Stride/Rendering/RenderContext.cs`, `Draw`). Same position:

```csharp
            if (PixelSnapping.TrySnap(Transform, options.Destination, options.Rotation, options.Origin, out System.Numerics.Vector2 snappedPosition, out System.Numerics.Vector2 snappedSize))
            {
                spriteBatch.Draw(
                    tex,
                    snappedPosition.AsEngineVector(),
                    options.Source?.AsEngineRectangle(),
                    options.Color.AsEngineColor() * Options.Opacity,
                    0f,
                    Vector2.Zero,
                    new Vector2(snappedSize.X / size.X, snappedSize.Y / size.Y),
                    SpriteEffects.None,
                    ImageOrientation.AsIs,
                    options.Depth);
                return;
            }
```

(Stride's existing unsnapped path ignores `options.Origin` and always passes `Vector2.Zero`. `TrySnap` only snaps when the origin is zero, so both engines snap exactly the same quads.)

- [ ] **Step 6: Build and test**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental` and `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 errors, no new warnings (`IcyUI.Stride` still at 0), all tests pass.

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/Rendering/PixelSnapping.cs sources/IcyUI.Tests/Rendering/PixelSnappingTests.cs sources/IcyUI.MonoGame/Rendering/RenderContext.cs sources/IcyUI.Stride/Rendering/RenderContext.cs
git commit -m "Snap axis-aligned quads to whole physical pixels in both engines" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Engine connectors (native window info, display-scale forwarding, polling)

**Files:**
- Create: `sources/IcyUI.MonoGame/Rendering/MonoGameNativeWindow.cs`, `sources/IcyUI.Stride/Rendering/StrideNativeWindow.cs`
- Modify: both `RenderContext.cs` files (display-scale members)
- Modify: `sources/IcyUI.MonoGame/Configuration/MonoGameBuildingExtensions.cs` (`WithDefaultMonoGameConfiguration`), `MonoGameIcyRenderer.cs` (`Update`)
- Modify: `sources/IcyUI.Stride/Configuration/StrideBuildingExtensions.cs` (`WithDefaultStrideConfiguration`), `IcyUIGameSystem.cs` (`Update`)

**Interfaces:**
- Consumes: `DisplayScaleTracker`, `DisplayScales.GetProvider()`, `NativeWindowInfo` (Task 4).
- Produces on both engine `RenderContext` classes: `float DisplayScale`, `event EventHandler? DisplayScaleChanged`, `void AttachDisplayScaleTracker(DisplayScaleTracker tracker)`, `void PollDisplayScale()`.

No OS calls go in these projects. The only native call is MonoGame's `SDL_GetWindowWMInfo`, which belongs to the engine's own SDL2 library.

- [ ] **Step 1: Add display-scale members to both engine render contexts**

Add to **both** `IcyUI.MonoGame/Rendering/RenderContext.cs` and `IcyUI.Stride/Rendering/RenderContext.cs` (field next to the other private fields; members after `ViewportSize`; add `using Icy.Rendering.Display;`):

```csharp
        private DisplayScaleTracker? displayScaleTracker;
```

```csharp
        /// <inheritdoc/>
        public event EventHandler? DisplayScaleChanged;

        /// <inheritdoc/>
        /// <remarks>
        /// Reads the value from the <see cref="DisplayScaleTracker"/> attached through <see cref="AttachDisplayScaleTracker"/>,
        /// as of its last <see cref="PollDisplayScale"/> call; <c>1.0</c> when none is attached.
        /// </remarks>
        public float DisplayScale => displayScaleTracker?.Scale ?? 1f;

        /// <summary>
        /// Attaches the tracker that reports this context's display scale, and forwards its changes to <see cref="DisplayScaleChanged"/>.
        /// </summary>
        /// <param name="tracker">The tracker for the window this context renders into.</param>
        public void AttachDisplayScaleTracker(DisplayScaleTracker tracker)
        {
            ArgumentNullException.ThrowIfNull(tracker);
            displayScaleTracker = tracker;
            tracker.Changed += (_, e) => DisplayScaleChanged?.Invoke(this, e);
            tracker.Poll();
        }

        /// <summary>
        /// Re-reads the display scale. Called once per update by the engine integration.
        /// </summary>
        public void PollDisplayScale() => displayScaleTracker?.Poll();
```

- [ ] **Step 2: MonoGame connector**

`sources/IcyUI.MonoGame/Rendering/MonoGameNativeWindow.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.InteropServices;
using Icy.Rendering.Display;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Builds <see cref="NativeWindowInfo"/> for a MonoGame window, for core display-scale detection.
    /// </summary>
    /// <remarks>
    /// On DesktopGL, <see cref="GameWindow.Handle"/> is an <c>SDL_Window*</c>; the Win32 <c>HWND</c> comes from
    /// <c>SDL_GetWindowWMInfo</c> in the <c>SDL2</c> library DesktopGL ships. On WindowsDX the handle already is an <c>HWND</c>.
    /// Any failure yields <c>Handle = 0</c>, and core then falls back to the system DPI.
    /// </remarks>
    internal static class MonoGameNativeWindow
    {
        private const int SdlSysWmWindows = 1;
        private const int SdlSubsystemOffset = 4;
        private const int SdlWindowHandleOffset = 8;

        public static NativeWindowInfo GetInfo(Game game)
        {
            GameWindow window = game.Window;
            var parameters = game.GraphicsDevice.PresentationParameters;
            return new NativeWindowInfo(
                GetHwnd(window),
                new System.Drawing.Size(window.ClientBounds.Width, window.ClientBounds.Height),
                new System.Drawing.Size(parameters.BackBufferWidth, parameters.BackBufferHeight));
        }

        private static nint GetHwnd(GameWindow window)
        {
            if (!OperatingSystem.IsWindows() || window.Handle == 0)
                return 0;
            if (window.GetType().Name != "SdlGameWindow")
                return window.Handle;

            // SDL_SysWMinfo: { SDL_version version (3 bytes, padded to 4); int subsystem; union { HWND window; ... } }.
            // On 64-bit the union is 8-byte aligned, so the HWND sits at offset 8.
            byte[] info = new byte[256];
            info[0] = 2; // SDL_MAJOR_VERSION - SDL only checks that the caller's major version is supported.
            try
            {
                if (SDL_GetWindowWMInfo(window.Handle, info) == 0 || BitConverter.ToInt32(info, SdlSubsystemOffset) != SdlSysWmWindows)
                    return 0;
            }
            catch (DllNotFoundException)
            {
                return 0;
            }
            catch (EntryPointNotFoundException)
            {
                return 0;
            }

            return (nint)BitConverter.ToInt64(info, SdlWindowHandleOffset);
        }

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_GetWindowWMInfo(nint window, [In, Out] byte[] info);
    }
}
```

In `MonoGameBuildingExtensions.WithDefaultMonoGameConfiguration`, convert to a block body (keep the XML docs):

```csharp
        public static IConfigurationBuilder WithDefaultMonoGameConfiguration(this IConfigurationBuilder builder, Game game)
        {
            var renderContext = new RenderContext(game.GraphicsDevice);
            renderContext.AttachDisplayScaleTracker(new DisplayScaleTracker(DisplayScales.GetProvider(), () => MonoGameNativeWindow.GetInfo(game)));
            return builder.ConfigureRendering(renderContext)
                   .ConfigureInput(new InputSystem(game))
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .WithAssetContextFactory(new MonoGameAssetContextFactory(game))
                   .AddBasicFontSupport()
                   .UseMonoGameImporters(game);
        }
```

(Add `using Icy.Rendering.Display;`.) In `MonoGameIcyRenderer.Update`, before `base.Update(gameTime);`:

```csharp
            (Configuration.RenderContext as RenderContext)?.PollDisplayScale();
```

(Add `using Icy.MonoGame.Rendering;`.)

- [ ] **Step 3: Stride connector**

`sources/IcyUI.Stride/Rendering/StrideNativeWindow.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering.Display;
using Stride.Engine;

namespace Icy.Stride.Rendering
{
    /// <summary>
    /// Builds <see cref="NativeWindowInfo"/> for a Stride game window, for core display-scale detection.
    /// </summary>
    internal static class StrideNativeWindow
    {
        public static NativeWindowInfo GetInfo(Game game)
        {
            var window = game.Window;
            var client = window.ClientBounds;
            var backBuffer = game.GraphicsDevice.Presenter?.BackBuffer;
            nint handle = OperatingSystem.IsWindows() ? window.NativeWindow?.Handle ?? 0 : 0;
            return new NativeWindowInfo(
                handle,
                new System.Drawing.Size(client.Width, client.Height),
                new System.Drawing.Size(backBuffer?.Width ?? client.Width, backBuffer?.Height ?? client.Height));
        }
    }
}
```

**Spike check (do this before moving on):** build `IcyUI.Stride`. If `window.NativeWindow?.Handle` doesn't compile under Stride 4.3, run

```bash
grep -rl "class WindowHandle" ~/.nuget/packages/stride.games/4.3.0.2507/lib/net10.0/*.xml
grep -A3 "P:Stride.Games.WindowHandle" ~/.nuget/packages/stride.games/4.3.0.2507/lib/net10.0/Stride.Games.xml
```

to find the handle member, and use it. If there's no HWND-bearing member, pass `0`. The Windows provider then uses `GetDpiForSystem`; record "Stride: per-monitor DPI changes not detected" under Known issues in `CLAUDE.md`.

In `StrideBuildingExtensions.WithDefaultStrideConfiguration`, convert to a block body the same way:

```csharp
        public static IConfigurationBuilder WithDefaultStrideConfiguration(this IConfigurationBuilder builder, Game game)
        {
            var renderContext = new Rendering.RenderContext(game.GraphicsDevice);
            renderContext.AttachDisplayScaleTracker(new DisplayScaleTracker(DisplayScales.GetProvider(), () => StrideNativeWindow.GetInfo(game)));
            return builder.ConfigureRendering(renderContext)
                   .ConfigureInput(new Input.InputSystem(
                       game.Services.GetService<global::Stride.Input.InputManager>()
                       ?? ThrowHelper.ThrowInvalidOperationException<global::Stride.Input.InputManager>("The game has no InputManager service registered. Configure IcyUI after the game has been initialized.")))
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .WithAssetContextFactory(new StrideAssetContextFactory(game))
                   .AddBasicFontSupport()
                   .UseStrideImporters(game);
        }
```

(Add `using Icy.Rendering.Display;` and `using Icy.Stride.Rendering;`.) In `IcyUIGameSystem.Update`, before `base.Update(gameTime);`:

```csharp
            (Configuration.RenderContext as Rendering.RenderContext)?.PollDisplayScale();
```

- [ ] **Step 4: Build and test**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental` and `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 errors, no new warnings, all tests pass. Runtime verification happens in Task 8 via the demo's `DisplayScale` readout.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.MonoGame sources/IcyUI.Stride
git commit -m "Connect MonoGame and Stride windows to core display-scale detection" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `ScalingDemo`, Stride DPI manifest, documentation

**Files:**
- Create: `sources/Shared Samples/ScalingDemo.cs`, `sources/MonoGame Sample/Samples/ScalingSample.cs`, `sources/Stride Sample/app.manifest`
- Modify: `sources/MonoGame Sample/SampleGame.cs`, `sources/MonoGame Sample/MonoGame Sample.csproj`
- Modify: `sources/Stride Sample/SampleGame.cs`, `sources/Stride Sample/Stride Sample.csproj`
- Modify: `CLAUDE.md` (Known issues), `.claude/skills/engine-integration/SKILL.md`

**Interfaces:**
- Consumes: `IcyConfiguration.Scaling`, `Canvas.EffectiveScale`/`SurfaceSize`, `IRenderContext.DisplayScale`.
- Produces: `public static UIElement ScalingDemo.Build(IcyConfiguration configuration, string fontFamily)` (namespace `Icy.SharedSamples`).

- [ ] **Step 1: Write the shared demo**

`sources/Shared Samples/ScalingDemo.cs`. It's compiled into `MonoGame Sample`, which has nullable disabled, so don't use `?` annotations:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Interactive demo of UI scaling: switch <see cref="UIScaleMode"/>/<see cref="ReferenceFit"/>, drag the user scale,
    /// and watch the live readout. Contains a <see cref="ComboBox"/> and a <see cref="ColorPickerButton"/> so popup
    /// placement can be checked under scaling.
    /// </summary>
    public static class ScalingDemo
    {
        /// <summary>
        /// Builds the demo's root element.
        /// </summary>
        /// <param name="configuration">The library configuration whose <see cref="IcyConfiguration.Scaling"/> the demo edits.</param>
        /// <param name="fontFamily">The font family every text element resolves.</param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;
            ScalingConfiguration scaling = configuration.Scaling;

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
                Width = 420,
            };

            var readout = new TextBlock { Text = "(not attached)" };

            var modeBox = new ComboBox { ItemsSource = Enum.GetValues<UIScaleMode>(), SelectedItem = scaling.Mode, Width = 260 };
            modeBox.SelectionChanged += (_, _) =>
            {
                if (modeBox.SelectedItem is UIScaleMode mode)
                    scaling.Mode = mode;
            };

            var fitBox = new ComboBox { ItemsSource = Enum.GetValues<ReferenceFit>(), SelectedItem = scaling.ReferenceFit, Width = 260 };
            fitBox.SelectionChanged += (_, _) =>
            {
                if (fitBox.SelectedItem is ReferenceFit fit)
                    scaling.ReferenceFit = fit;
            };

            var userScale = new Slider { Minimum = 0.5f, Maximum = 2f, Value = scaling.UserScale, Width = 260 };
            userScale.ValueChanged += (_, _) => scaling.UserScale = userScale.Value;

            root.Children.Add(new TextBlock { Text = "UI scaling" });
            root.Children.Add(readout);
            root.Children.Add(new TextBlock { Text = "Scale mode" });
            root.Children.Add(modeBox);
            root.Children.Add(new TextBlock { Text = "Reference fit (1920x1080)" });
            root.Children.Add(fitBox);
            root.Children.Add(new TextBlock { Text = "User scale (0.5 - 2.0)" });
            root.Children.Add(userScale);
            root.Children.Add(new TextBlock { Text = "Popup placement check" });
            root.Children.Add(new ColorPickerButton());

            void Refresh()
            {
                Canvas canvas = root.Canvas;
                readout.Text = canvas == null
                    ? "(not attached)"
                    : $"Display {configuration.RenderContext.DisplayScale:0.00}x | Effective {canvas.EffectiveScale:0.00}x | Surface {canvas.SurfaceSize.Width}x{canvas.SurfaceSize.Height}";
            }

            Canvas subscribed = null;
            root.Attached += (_, _) =>
            {
                if (root.Canvas != null && !ReferenceEquals(root.Canvas, subscribed))
                {
                    subscribed = root.Canvas;
                    subscribed.PropertyChanged += (_, _) => Refresh();
                }

                Refresh();
            };

            return root;
        }
    }
}
```

(`Thickness` has a uniform `Thickness(int)` constructor.)

- [ ] **Step 2: Register in MonoGame Sample**

`sources/MonoGame Sample/Samples/ScalingSample.cs`, mirroring `ColorPickerSample.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="ScalingDemo"/> - live UI-scaling controls and readout - as its own selectable sample.
    /// </summary>
    internal class ScalingSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public ScalingSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Scaling Demo")
        {
            VisibleChanged += (_, _) =>
            {
                if (demoRoot != null)
                    demoRoot.IsVisible = Visible;
            };
        }

        protected override void LoadContent()
        {
            UIConfiguration.Fonts.ImportFont(UIConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            Canvas.IsInputEnabled = true;

            demoRoot = ScalingDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

In `sources/MonoGame Sample/SampleGame.cs`, append `new ScalingSample(this, uiConfiguration, canvas)` after `new PropertyGridSample(this, uiConfiguration, canvas)` in the `samplesRunner.Prepare([...])` list (add the comma).

In `MonoGame Sample.csproj`, after the `PropertyGridDemo.cs` link:

```xml
    <Compile Include="..\Shared Samples\ScalingDemo.cs" Link="Samples\ScalingDemo.cs" />
```

- [ ] **Step 3: Register in Stride Sample and make it DPI-aware**

In `sources/Stride Sample/SampleGame.cs`:
- Add the field `private UIElement? scalingRoot;` after `propertyGridRoot`.
- After `propertyGridRoot = PropertyGridDemo.Build(configuration, "Airfool");`, add `scalingRoot = ScalingDemo.Build(configuration, "Airfool");`.
- After `canvas.Add(propertyGridRoot);`, add `canvas.Add(scalingRoot);`.
- Change `selectedDemo = (selectedDemo + 1) % 16;` to `% 17`.
- In `UpdateSelectedDemo()`, add `scalingRoot!.IsVisible = selectedDemo == 16;`.
- Update the class's XML `<summary>` list of demos to mention `ScalingDemo`.

In `Stride Sample.csproj`, after the `PropertyGridDemo.cs` link:

```xml
    <Compile Include="..\Shared Samples\ScalingDemo.cs" Link="ScalingDemo.cs" />
```

and in the first `<PropertyGroup>`:

```xml
    <ApplicationManifest>app.manifest</ApplicationManifest>
```

Create `sources/Stride Sample/app.manifest` as a copy of `sources/MonoGame Sample/app.manifest`, changing only the identity name:

```bash
sed 's/name="MonoGame_Sample"/name="Stride_Sample"/' "sources/MonoGame Sample/app.manifest" > "sources/Stride Sample/app.manifest"
```

- [ ] **Step 4: Update the documentation**

In `CLAUDE.md` → `## Known issues`, replace the bullet that begins with "IcyUI has no DPI awareness yet" with:

```markdown
- UI scaling (`Canvas.EffectiveScale`, `IcyConfiguration.Scaling`) follows the host's DPI awareness: a host without a PerMonitorV2 `app.manifest` reports `DisplayScale = 1` and is bitmap-stretched by Windows. Both sample hosts ship the manifest.
```

In `.claude/skills/engine-integration/SKILL.md`, add under "When editing engine-integration code":

```markdown
- OS-specific code lives in core (e.g. `Icy.Rendering.Display`, `Icy.Input.Clipboard`); engine projects are thin connectors. For display scale, an engine only builds a `NativeWindowInfo`, attaches a `DisplayScaleTracker` to its `RenderContext`, and calls `PollDisplayScale()` once per update. A future `IcyUI.FNA` needs exactly that connector (FNA is SDL-based, so it can reuse the MonoGame `SDL_GetWindowWMInfo` approach).
```

- [ ] **Step 5: Build**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: 0 errors, and no warnings from `ScalingDemo.cs` in either host (in particular no CS8632 in `MonoGame Sample`).

- [ ] **Step 6: Commit**

```bash
git add "sources/Shared Samples/ScalingDemo.cs" "sources/MonoGame Sample" "sources/Stride Sample" CLAUDE.md .claude/skills/engine-integration/SKILL.md
git commit -m "Add ScalingDemo to both sample hosts and make Stride Sample DPI-aware" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Whole-branch verification

- [ ] **Step 1: Full rebuild and warning count**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"`
Expected: `0 Error(s)`, and the warning count ≤ the pre-feature baseline (85 on the ARM64 laptop, including duplicated NU1900 lines). `IcyUI.Stride` has 0 warnings.

- [ ] **Step 2: Full test suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: all pass (901 baseline + the new tests).

- [ ] **Step 3: Whole-branch code review**

Request a review of all commits from this plan (superpowers:requesting-code-review). Focus on the Review Focus list at the top of this plan, and on the `Canvas` transform composition order.

- [ ] **Step 4: Hand over for the manual smoke test (Ivan runs it; never claim it)**

Ask Ivan to run, on the 200 % ARM64 laptop and then on the 100 % x64 desktop:
1. **Both samples, any demo:** text and controls have the same physical size as in other apps (2× on the laptop), and text is crisp, not blurry.
2. **Scaling Demo:** the readout shows `Display 2.00x` on the laptop (`1.00x` on the desktop). If Stride shows `1.00x` on the laptop, the Stride handle path fell back; check the Task 7 spike note.
3. **Mode `None`:** the UI shrinks to physical size. **`ReferenceResolution`** with each fit: the UI fills the window as described.
4. **Drag `User scale`:** the UI resizes live and remains clickable at the right spots.
5. **Open the mode ComboBox and the ColorPickerButton popup at 2× and at 0.5×:** the popups appear attached to their controls and stay on-screen.
6. **F1/F2 debug HUD:** it scales with the UI.
7. If a second monitor with a different scale is available, drag the window across: `Display` updates.

Remember the known MonoGame-on-ARM64 shutdown hang (`CLAUDE.md`); kill the process after closing if needed. It's unrelated to this feature.
