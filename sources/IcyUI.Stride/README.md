# IcyUI.Stride

Stride integration for [IcyUI](https://www.nuget.org/packages/IcyUI). Requires Stride 4.3 or later.

```
dotnet add package IcyUI.Stride --prerelease
```

```csharp
using Icy.Configuration;
using Icy.Stride.Configuration;
using Icy.UI;

// In Game.BeginRun(), after the scene system is set up:
IcyUISceneRenderer overlay = this.UseIcyUI();
var configuration = this.GetIcyConfiguration();
configuration.UseDefaultTheme();
var canvas = new Canvas(configuration) { IsInputEnabled = true };
overlay.Canvases.Add(canvas);
```

**Status:** early alpha (0.1.0-alpha). APIs may change between alphas.

Source and samples: https://github.com/IOExcept10n/IcyUI/tree/platform-independent
Issues: https://github.com/IOExcept10n/IcyUI/issues
