# Tier-2 Phase 7 — `Dialog`

> Design spec for Phase 7 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`). The
> roadmap's own scope note described an async `Task`-returning API for message boxes and simple input
> dialogs, and flagged one open question: extend `Window` with modality, or build `Dialog` as its own
> overlay-hosted control. Confirmed with Ivan: **independent control** - `Window` stays exactly as it is
> today (a non-modal focus trap for embedded panels), untouched by this phase.

## Context

Three real findings shaped this design, each verified against the actual current source, not assumed:

1. **`Canvas.HitTest` already checks `Overlays` before `rootElements`, topmost-first, returning on the
   first hit** (`Canvas.cs`). A full-viewport, hit-testable overlay element therefore already blocks every
   pointer input to whatever's behind it - **no new blocking primitive is needed anywhere in `Canvas`**.
   `Dialog`'s backdrop reuses this exactly as-is, the same mechanism `ComboBox`/`Dropdown`'s popups and
   Phase 1's drag-ghost already use.
2. **`Canvas.Overlays`'s own doc comment already anticipates this control by name**: "a future `Dialog`'s
   backdrop" is one of its three cited motivating examples, and overlay elements are already arranged
   against the *full viewport* every frame via their own `Margin`/`HorizontalAlignment`/`VerticalAlignment`
   (the same mechanism `Diagnostics.DebugHudHost`'s screen-space HUD uses) - so `Dialog` needs no bespoke
   viewport-sizing logic at all. `Stretch` alignment on the backdrop and `Center` alignment on the inner
   dialog box, expressed entirely through a `ControlTemplate`, is sufficient.
3. **`Window` is explicitly documented as non-modal** ("No backdrop/pointer-input isolation in v1 ...
   elements behind it remain clickable") and is a plain `ContentControl` with `IsFocusScope`/`IsOpen`/
   `Show`/`Close`/`Opened`/`Closed` - a useful shape to *mirror* (focus-scope open/close lifecycle,
   `Canvas.Focus`/`Canvas.CloseFocusScope`), but not to extend or modify, since doing so would change an
   already-documented, already-shipped contract for existing `Window` consumers.
4. **Implicit theming is an exact-runtime-type-name match, not generic-definition-aware** -
   `ResourceDictionary.GetImplicitStyleKey(Type)` builds its lookup key from `Type.FullName`, and
   `UIElement`'s own style resolution looks up that key via `GetImplicitStyleKey(GetType())` - the *exact*
   runtime type. A closed generic's `FullName` embeds its full type argument
   (`Dialog\`1[[...DialogResult...]]` vs. `Dialog\`1[[...String...]]`) - two different keys for
   `Dialog<DialogResult>` vs. `Dialog<string?>`, neither matching an open-generic `Style TargetType="Dialog"`
   entry, with no generic-type-definition fallback anywhere in the lookup. A `Dialog<TResult>` directly
   deriving from `ContentControl` (this spec's first draft) would therefore never pick up a theme at all -
   caught and corrected before this spec was finalized, see the split in Decisions/§1 below.

**Cross-engine impact.** `Dialog`/`MessageBox` themselves are pure core-`IcyUI` work, same as every prior
Tier-2 phase. The one engine-touching step is the final task, registering a `DialogDemo` sample in both
`IcyUI.MonoGame`'s and `IcyUI.Stride`'s sample hosts independently (neither shares code with the other).
`IcyUI.FNA` is still an unimplemented stub with no sample host to register into, so it needs no equivalent
work *now* - but it will need the same demo registration once `IcyUI.FNA` gains a real sample host.

**Out of scope** (per the roadmap): `FileDialog` (a separate future plan - real file/folder-picker UI plus
an `IcyUI.Stride` `VirtualFileSystem`-as-`AssetContext` engine-integration gap, both unrelated to this
control), `Menu` (dropped from the roadmap entirely).

## Decisions

| Decision | Choice |
|---|---|
| Base | New, independent controls - not extending/modifying `Window`. Split in two (Context #4 forced this): **`Dialog : ContentControl`** (non-generic - the themed visual element, backdrop/focus-scope/overlay/Escape mechanics, no result concept of its own) and **`Dialog<TResult>`** (a plain wrapper class, *not* a `UIElement` - owns one `Dialog` instance plus the `TaskCompletionSource<TResult?>`). C# allows a non-generic and a generic type to share one name in the same namespace (like `Action`/`Action<T>`), so the public API surface is exactly what was approved before the split (`Dialog<TResult>.ShowAsync`/`.Close`) - only what's *underneath* changed. |
| Scope | Both a general-purpose modal-hosting primitive (`Dialog<TResult>`, hosts *any* content) and convenience helpers on top (`MessageBox.ShowAsync`/`ShowInputAsync`) that build a small content tree and call into it - not two parallel, unrelated implementations. |
| Result model | `Dialog<TResult>` is generic (`ShowAsync`/`Close` compiler-matched on the same `TResult`, no casts) - now doubly justified: it was already the fix for the earlier CA1000/type-safety discussion, and being a plain (non-`UIElement`) class means the theming mismatch (Context #4) never applies to it in the first place. The static convenience methods live on a **separate, non-generic** `MessageBox` class, not on `Dialog<TResult>` itself - avoids declaring static members on a generic type (CA1000) while keeping `Dialog<TResult>`'s own API fully type-safe. |
| `DialogResult`/`DialogButtons` | Standard WPF/WinForms-shaped: `DialogResult { None, OK, Cancel, Yes, No }`, `DialogButtons { OK, OKCancel, YesNo, YesNoCancel }` - both defined in `MessageBox.cs`, the only place that produces/consumes them (`Dialog<TResult>` itself is agnostic to what `TResult` is). |
| Pointer modality | A full-viewport backdrop (`Dialog.Chrome`, via `ControlTemplate` - see §2), added to `Canvas.Overlays` by `Dialog.Show`. Reuses `HitTest`'s existing overlay-first, topmost-wins behavior (Context #1) - no new `Canvas` mechanism. |
| Backdrop click | Swallowed (blocks the tap from reaching anything behind) but does **not** close the dialog - only an explicit button or Escape does. Deliberately different from `Dropdown`/`ComboBox`'s click-outside-closes popup convention: a dialog's in-progress input (e.g. `MessageBox.ShowInputAsync`'s typed text) shouldn't be destroyed by one stray click the way a re-openable, non-destructive dropdown selection can be. |
| Keyboard dismissal | Escape (`INavigationEvents.CloseModal`, the same event `Selector.OnNavigationCloseModal` already subscribes to) calls `Dialog.Close()` (no result - `Dialog` itself has no result concept). `Dialog<TResult>` subscribes to the underlying `Dialog.Closed` event and completes its task with `default` if `Close(TResult?)` hasn't already done so - see §1. Subscribed only while open, symmetric with `Selector`'s own `SubscribeNavigation`/`UnsubscribeNavigation` pattern. |
| Focus-scope lifecycle | `Dialog.Show`/`Close` mirror `Window.OnOpened`/`OnClosed` exactly: sets `IsFocusScope = true` and calls `Canvas.Focus(...)` (self if focusable, else first focusable descendant) on show; calls `Canvas.CloseFocusScope(this)` to restore whatever was focused before, on close. `Dialog<TResult>` doesn't touch focus/overlays itself - it delegates entirely to its owned `Dialog`. |
| Stacking | No special support needed - falls out naturally. Each `Dialog`/`Dialog<TResult>` pair is its own overlay entry and its own focus scope; `Canvas`'s existing focus-scope machinery already handles nested scopes (used today whenever a `Window` opens another `Window`). |
| Re-entrancy guards | `Dialog<TResult>.ShowAsync` while already open returns the same in-flight `Task` rather than showing a second `Dialog`. `Close` is idempotent on both classes - a second call (or a call before showing) no-ops rather than double-completing the `TaskCompletionSource`, double-removing the overlay, or double-raising `Closed`. |
| Theming | New `ControlTemplate`/`Style` entry in `DefaultTheme.xml` (backdrop + centered box + `ContentPresenter`) - the first control whose *default* visual identity requires more than a single `Border`-with-setters shape, but expressed with existing markup primitives, no new framework mechanism. |

## Detailed design

### 1. `Dialog` and `Dialog<TResult>`

New file `sources/IcyUI/UI/Controls/Dialog.cs`, both classes (Context #4 forced the split - a non-generic
themed visual element plus a plain generic wrapper, rather than one generic `ContentControl`):

```csharp
/// <summary>
/// A modal overlay - a full-viewport dimming backdrop (<see cref="Control.Chrome"/>, themed) behind a
/// centered content box, blocking pointer input to everything behind it (see the type's own remarks) and
/// trapping keyboard/gamepad focus while <see cref="IsOpen"/>. Carries no result of its own - see
/// <see cref="Dialog{TResult}"/> for the <see cref="System.Threading.Tasks.Task"/>-returning API most
/// callers actually want; this class is its non-generic (themeable, see Context #4) building block.
/// </summary>
public class Dialog : ContentControl
{
    private Canvas? shownOnCanvas;
    private INavigationEvents? subscribedNavigation;

    /// <summary>Occurs after this dialog closes (<see cref="IsOpen"/> becomes <see langword="false"/>).</summary>
    public event EventHandler? Closed;

    /// <summary>Gets a value indicating whether this dialog is currently shown.</summary>
    public bool IsOpen => shownOnCanvas != null;

    /// <summary>
    /// Shows this dialog as a modal overlay on <paramref name="canvas"/> - adds it to
    /// <see cref="Canvas.Overlays"/>, enters a focus scope (mirrors <see cref="Window.OnOpened"/>), and
    /// subscribes Escape (<see cref="INavigationEvents.CloseModal"/>) to <see cref="Close"/>. A no-op if
    /// already open.
    /// </summary>
    public void Show(Canvas canvas)
    {
        if (shownOnCanvas != null)
            return;

        shownOnCanvas = canvas;
        canvas.AddOverlay(this);

        IsFocusScope = true;
        UIElement? focusTarget = IsFocusable ? this : EnumerateVisualSubtree().FirstOrDefault(e => e != this && e.IsFocusable);
        canvas.Focus(focusTarget);

        subscribedNavigation = canvas.Configuration.Input.Events.Navigation;
        subscribedNavigation.CloseModal += OnCloseModal;
    }

    /// <summary>
    /// Closes this dialog - removes the overlay, restores whatever was focused before <see cref="Show(Canvas)"/>,
    /// and raises <see cref="Closed"/>. A no-op if this dialog isn't currently open.
    /// </summary>
    public void Close()
    {
        if (shownOnCanvas == null)
            return;

        subscribedNavigation!.CloseModal -= OnCloseModal;
        subscribedNavigation = null;

        Canvas canvas = shownOnCanvas;
        shownOnCanvas = null;
        canvas.RemoveOverlay(this);
        canvas.CloseFocusScope(this);

        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseModal(object? sender, EventArgs e) => Close();
}

/// <summary>
/// A <see cref="Dialog"/> paired with a <see cref="System.Threading.Tasks.Task{TResult}"/>-returning
/// show/close API - the general-purpose modal-hosting primitive most callers use directly (or through
/// <see cref="MessageBox"/>'s convenience methods). Not a <see cref="UI.UIElement"/> itself; wraps a plain
/// <see cref="Dialog"/> instance, which is what's actually added to a <see cref="Canvas"/> and themed - see
/// Context #4 for why a <see cref="UI.UIElement"/> can't be generic and still theme correctly.
/// </summary>
/// <typeparam name="TResult">The type of value this dialog closes with.</typeparam>
public sealed class Dialog<TResult>
{
    private readonly Dialog dialog = new();
    private TaskCompletionSource<TResult?>? completionSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dialog{TResult}"/> class.
    /// </summary>
    public Dialog()
    {
        dialog.Closed += (_, _) =>
        {
            // Escape (Dialog.OnCloseModal) or any other route to Dialog.Close() that didn't go through
            // this class's own Close(TResult?) - complete with default the same way OnCloseModal already
            // would have if this were still one generic class (see Context #4/Decisions).
            if (completionSource is { } source)
            {
                completionSource = null;
                source.SetResult(default);
            }
        };
    }

    /// <summary>Gets or sets the content this dialog displays.</summary>
    public UIElement? Content
    {
        get => dialog.Content;
        set => dialog.Content = value;
    }

    /// <summary>Gets a value indicating whether this dialog is currently shown.</summary>
    public bool IsOpen => dialog.IsOpen;

    /// <summary>
    /// Shows this dialog as a modal overlay on <paramref name="canvas"/> and returns a task that completes
    /// with whatever value <see cref="Close(TResult?)"/> is called with (or <see langword="default"/>, if
    /// dismissed via Escape). Calling this again while already open returns the same in-flight task instead
    /// of showing a second copy.
    /// </summary>
    public Task<TResult?> ShowAsync(Canvas canvas)
    {
        if (completionSource != null)
            return completionSource.Task;

        completionSource = new TaskCompletionSource<TResult?>();
        dialog.Show(canvas);
        return completionSource.Task;
    }

    /// <summary>
    /// Closes this dialog with <paramref name="result"/>, completing the task <see cref="ShowAsync(Canvas)"/>
    /// returned. A no-op if this dialog isn't currently open.
    /// </summary>
    public void Close(TResult? result)
    {
        if (completionSource is not { } source)
            return;

        completionSource = null;
        source.SetResult(result);
        dialog.Close();
    }
}
```

`Dialog`'s `GetVisualChildren`/`MeasureContent`/`ArrangeContent`/rendering are **all inherited unchanged**
from `ContentControl`/`Control` - `Chrome` (retemplated, see §2) already yields/measures/arranges/draws
exactly what a themed control needs, and `Content` flows into it exactly like every other `ContentControl`.
No override needed anywhere in either class beyond what's shown above.

**Ordering in `Dialog<TResult>.Close(TResult?)`**: `completionSource` is nulled out *before* calling
`dialog.Close()`, so the `Closed` handler registered in the constructor sees `completionSource == null` and
skips its own `SetResult` - the task is completed exactly once regardless of which path triggered the close.

**Why `EnumerateVisualSubtree`/`IsFocusable` duplicate `Window.OnOpened`'s exact logic inline** rather than
sharing a helper: the two classes don't share a base beyond `ContentControl`/`Control`, and the logic is
three lines - not worth a new abstraction for its only two call sites, matching this codebase's existing
YAGNI bar (`WrapGrid`/`ListBox` similarly duplicated the tap-to-select trio at their own creation time,
before it was worth consolidating - see the Phase 6 spec).

### 2. Theming (`DefaultTheme.xml`)

```xml
<!-- Dialog -->
<ControlTemplate x:Key="IcyDefaultDialogTemplate" TargetType="Dialog">
  <Border Background="#AA000000" HorizontalAlignment="Stretch" VerticalAlignment="Stretch">
    <Border Background="#FF2E2E38" BorderBrush="#FF56566A" BorderThickness="1" Padding="16"
            HorizontalAlignment="Center" VerticalAlignment="Center">
      <ContentPresenter Content="{TemplateBinding Content}"/>
    </Border>
  </Border>
</ControlTemplate>

<Style TargetType="Dialog" Template="{StaticResource IcyDefaultDialogTemplate}"/>
```

`TargetType="Dialog"` here refers to the plain, non-generic `Dialog : ContentControl` from §1 - the only
class this style/template needs to match, since `Dialog<TResult>` is never itself instantiated as a
`UIElement` (Context #4). Every `MessageBox`-built dialog, and any future custom `Dialog<TResult>`, shares
this one theme automatically, regardless of `TResult`.

The outer `Border`'s `#AA000000` (semi-transparent black) is the dimming backdrop; the inner `Border` is the
visible "dialog box." Both are ordinary retemplatable `ControlTemplate` content - no bespoke `Dialog`-only
rendering path.

### 3. `MessageBox`

New file `sources/IcyUI/UI/Controls/MessageBox.cs` - non-generic static class, `DialogResult`/
`DialogButtons` enums defined alongside it (the only place either is produced or consumed):

```csharp
public enum DialogResult { None, OK, Cancel, Yes, No }

public enum DialogButtons { OK, OKCancel, YesNo, YesNoCancel }

public static class MessageBox
{
    public static Task<DialogResult> ShowAsync(Canvas canvas, string message, DialogButtons buttons = DialogButtons.OK)
    {
        var dialog = new Dialog<DialogResult>();
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 12) });

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (DialogResult result in ButtonsFor(buttons))
        {
            var button = new Button { Content = result.ToString(), Margin = new Thickness(4, 0, 0, 0) };
            button.Click += (_, _) => dialog.Close(result);
            buttonRow.Children.Add(button);
        }

        panel.Children.Add(buttonRow);
        dialog.Content = panel;
        return dialog.ShowAsync(canvas);
    }

    public static Task<string?> ShowInputAsync(Canvas canvas, string prompt, string? defaultValue = null)
    {
        var dialog = new Dialog<string?>();
        var textBox = new TextBox { Text = defaultValue ?? string.Empty, Margin = new Thickness(0, 0, 0, 12) };
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(textBox);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK" };
        ok.Click += (_, _) => dialog.Close(textBox.Text);
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(4, 0, 0, 0) };
        cancel.Click += (_, _) => dialog.Close(null);
        buttonRow.Children.Add(ok);
        buttonRow.Children.Add(cancel);

        panel.Children.Add(buttonRow);
        dialog.Content = panel;
        return dialog.ShowAsync(canvas);
    }

    private static IEnumerable<DialogResult> ButtonsFor(DialogButtons buttons) => buttons switch
    {
        DialogButtons.OK => [DialogResult.OK],
        DialogButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
        DialogButtons.YesNo => [DialogResult.Yes, DialogResult.No],
        DialogButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
        _ => throw new ArgumentOutOfRangeException(nameof(buttons)),
    };
}
```

Exact button/layout markup (spacing, `TextBlock` wrapping, whether the button row uses a `Grid` instead of a
right-aligned `StackPanel`) is an implementation-time detail, not a spec-blocking decision - the shape above
establishes the pattern (`Dialog<TResult>` built once per call, content wired to call `Close` with the right
`TResult`, `ShowAsync` returned directly) that any layout polish sits on top of.

### 4. Escape / focus-scope wiring

Already shown inline in §1: `Dialog.OnCloseModal`/`Show`/`Close`'s `Canvas.Focus`/`CloseFocusScope` calls
handle Escape and focus-scope lifecycle entirely within the non-generic `Dialog` - no separate mechanism
beyond what's already there. This is the same shape `Selector.SubscribeNavigation`/`UnsubscribeNavigation`
and `Window.OnOpened`/`OnClosed` already establish independently; `Dialog` combines both existing patterns
rather than introducing a third one. `Dialog<TResult>` never touches `Canvas`/focus/navigation itself - it
only reacts to `Dialog.Closed` to complete its task (see §1's "Ordering" note).

### 5. Backdrop input-blocking

No code needed beyond what Context #1 already established: the outer backdrop `Border` (§2) is `Dialog`'s
own `Chrome`, sized to the full viewport, and `Chrome` is already hit-testable by default (no
`IsHitTestVisible = false` anywhere in this design) - `Canvas.HitTest`'s existing overlay-first check
(`Canvas.cs`) means any point over the backdrop resolves to the backdrop or a descendant (the dialog box,
its buttons), never anything behind it. No `OnTap` override is needed to "swallow" the click - simply *not
handling* it (no button, no close call) is already the correct behavior, since nothing behind the overlay
is ever reached by `HitTest` in the first place.

### 6. Samples

One new sample added to Shared Samples (same precedent as `SplitPaneDemo`/`ExpanderDemo`/`SelectorDemo`/
`WrapGridDemo`): `DialogDemo` - a few buttons exercising `MessageBox.ShowAsync` (one per `DialogButtons`
variant) and `MessageBox.ShowInputAsync`, displaying the returned result/typed value somewhere visible so
the async round-trip is visually confirmable.

## Testing

- **`Dialog`** (own new test file, instantiated directly - no test double needed, no abstract members):
  `IsOpen` before/after `Show`/`Close`; `Show` adds itself to `canvas.Overlays`, `Close` removes it; `Show`
  while already open is a no-op (doesn't add a second overlay entry); `Close` before `Show`, and a second
  `Close` while already closed, are both no-ops (no throw, `Closed` doesn't fire again); `IsFocusScope`
  becomes `true` on show; `Canvas.CloseFocusScope` is invoked on close (restores prior focus - reuse the
  same assertion pattern `WindowTests`, if one exists, already uses for `Window.Close`; otherwise mirror
  `Window`'s own remarks directly); simulated Escape (`INavigationEvents.CloseModal`, via
  `FakeInputSystem`) calls `Close` and raises `Closed`; two `Dialog` instances shown at once stack
  independently (`canvas.Overlays.Count == 2`, closing one doesn't affect the other's open state or focus
  scope).
- **`Dialog<TResult>`** (own new test file, a concrete `Dialog<int>`/`Dialog<string?>` instantiated
  directly): `IsOpen` mirrors the underlying `Dialog`'s; `ShowAsync`'s returned task completes with exactly
  the value passed to `Close(TResult?)`; calling `ShowAsync` twice while open returns the same task
  instance rather than showing a second `Dialog`; `Close` before `ShowAsync` is a no-op; `Close` called
  twice only completes the task once (no `InvalidOperationException` from a double `SetResult` on the
  second call); simulated Escape (driving the underlying `Dialog.Closed` without going through
  `Close(TResult?)`) completes the task with `default` exactly once.
- **`MessageBox`**: `ShowAsync` builds exactly the expected button set for each `DialogButtons` value
  (`OK`/`OKCancel`/`YesNo`/`YesNoCancel`) and each button's `Click` resolves the task with the matching
  `DialogResult`; `ShowInputAsync`'s OK button resolves with the `TextBox`'s current text (including a
  value the user changed from `defaultValue`), Cancel resolves with `null`; `defaultValue` pre-fills the
  `TextBox.Text`.
- **Backdrop blocking** (integration-level, via a real `Canvas`+`FakeInputSystem`, mirroring
  `SelectorThemeTests`' `CreateThemedCanvas` pattern): a control added to the canvas *before* a `Dialog` is
  shown over it no longer receives a tap at its own screen position while the dialog is open - confirms
  `Canvas.HitTest`'s existing overlay-first behavior actually blocks input for this control's real
  arranged (full-viewport) size, not just as a design claim.
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): `DialogDemo`'s message
  boxes/input prompt look correct, backdrop dims/blocks clicks to content behind it, Escape closes, and
  the returned result/typed value displays correctly after each round-trip.

## Critical files

**New:** `sources/IcyUI/UI/Controls/Dialog.cs` (both `Dialog` and `Dialog<TResult>`),
`sources/IcyUI/UI/Controls/MessageBox.cs` (incl. `DialogResult`/`DialogButtons`),
`sources/IcyUI.Tests/Controls/DialogTests.cs` (covers both classes),
`sources/IcyUI.Tests/Controls/MessageBoxTests.cs`, `sources/Shared Samples/DialogDemo.cs`.

**Modified:** `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (new `Dialog` `ControlTemplate`/`Style`,
§2); `sources/MonoGame Sample/SampleGame.cs`+`.csproj`, `sources/Stride Sample/SampleGame.cs`+`.csproj`
(sample registration, final task, same shape as every prior phase's own final task).

**Untouched (confirmed, not just assumed):** `sources/IcyUI/UI/Canvas.cs` (Context #1/#2 - the overlay
mechanism already does everything this phase needs), `sources/IcyUI/UI/Controls/Window.cs` (Context #3 -
not extended, not modified), `sources/IcyUI/UI/Controls/Selector.cs`/`Dropdown.cs`/`ComboBox.cs`.

## Open items for implementation planning (not blocking spec approval)

- §3's exact `MessageBox` layout markup (margins, button ordering/right-alignment, whether a `Grid` reads
  better than a right-aligned `StackPanel` for the button row) is a polish detail, free to adjust at
  implementation time.
- §2's backdrop/box colors (`#AA000000`/`#FF2E2E38`/`#FF56566A`) are placeholders matching the existing
  theme's palette, not carefully chosen - same status `WrapGrid.ItemWidth`'s `64f` default had in the
  Phase 6 spec.
- §1's `Dialog` only exposes `Closed`, not an `Opened` counterpart to fully mirror `Window`'s shape - added
  only because `Dialog<TResult>` needs it internally (to complete the task on an Escape-driven close);
  `Dialog<TResult>` itself doesn't re-expose either event to its own callers (its private `dialog` field is
  unreachable from outside). Whether either gap is worth closing (an `Opened` on `Dialog`, or forwarding
  events through `Dialog<TResult>` for a consumer that wants to react without awaiting `ShowAsync`'s task,
  e.g. an analytics hook) is unscoped - not requested, easy to add later without a breaking change if a
  concrete need shows up.
- `MessageBox.ShowAsync`'s button `Content` strings (`"OK"`, `"Cancel"`, `"Yes"`, `"No"`) are hardcoded
  English text in this design - localization is out of scope for this phase, same as every other Tier-2
  control's hardcoded sample/theme text to date.
