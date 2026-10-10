# IcyUI.MonoGame

MonoGame integration for [IcyUI](https://www.nuget.org/packages/IcyUI).

```
dotnet add package IcyUI.MonoGame --prerelease
```

**Pick a MonoGame backend yourself.** This package doesn't depend on one. Reference `MonoGame.Framework.DesktopGL`
or `MonoGame.Framework.WindowsDX`, version **3.8.5.1 or later**. Older MonoGame versions fail at load time with a `FileLoadException`.

```csharp
using Icy.Configuration;
using Icy.MonoGame.Configuration;
using Icy.UI;

// In Game.Initialize():
this.UseIcyUI();
var configuration = this.GetIcyConfiguration();
configuration.UseDefaultTheme();
var canvas = new Canvas(configuration) { IsInputEnabled = true };

// In Game.Draw(), after clearing:
canvas.Render();
```

**Known issues (DesktopGL):**
- On Windows-on-ARM64, `Game.Dispose()` can hang on shutdown (OpenGL runs through `OpenGLOn12`).
- No touch input: DesktopGL doesn't fill `TouchPanel`.

**Status:** early alpha (0.1.0-alpha). APIs may change between alphas.

Source and samples: https://github.com/IOExcept10n/IcyUI/tree/platform-independent
Issues: https://github.com/IOExcept10n/IcyUI/issues
