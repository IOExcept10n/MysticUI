# IcyUI

A cross-engine UI library for .NET games. The engine-agnostic core handles layout, controls, markup, styling, templating, data binding and animations. Thin integration packages connect it to a specific engine's rendering and input.

> **Status:** pre-MVP, under active development on the `platform-independent` branch. APIs may change without notice.

## Engines

| Package | Status |
|---|---|
| `IcyUI.MonoGame` | Primary integration (DesktopGL) |
| `IcyUI.Stride` | Working integration |
| `IcyUI.FNA` | Planned |

## Features

- XML markup with bindings, `{StaticResource}`, styles, visual states and control templates.
- A built-in default theme.
- Layout panels: `Grid`, `StackPanel`, `WrapGrid`, `SplitPane`, `ScrollViewer` (with virtualization).
- Controls: `Button`, `ToggleButton`, `CheckBox`, `Slider`, `ProgressBar`, `TextBox`, `ComboBox`, `ListBox`, `TabControl`, `Expander`, `Window`, `Dialog`/`MessageBox`, `ColorPicker`, `PropertyGrid`, and more.
- Drag-and-drop and an overlay layer for popups and dialogs.
- Animations and timelines.

## Building

Requires the .NET SDK.

```
dotnet build sources/IcyUI.sln
dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj
```

Sample hosts for both engines live in `sources/MonoGame Sample` and `sources/Stride Sample`. Their demos are shared from `sources/Shared Samples`.

## History

The project started as MysticUI, a Stride UI library based on [Myra](https://github.com/rds1983/Myra), and was later renamed AquaUI. It has since been rewritten as IcyUI.

## License

[MIT](LICENSE)
