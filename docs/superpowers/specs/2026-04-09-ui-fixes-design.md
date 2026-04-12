# UI Fixes — Design Spec
**Date:** 2026-04-09  
**Branch:** feature/net-core-10  
**Scope:** Three independent UX/bug fixes in ClassicUO.Client UI layer

---

## Fix 1 — Gump Positions Not Persisting

### Problem

Two subsystems fail to persist gump positions across sessions:

**XmlGump** (`XmlGumpHandler.cs`):
- `saveFileAfter = Time.Ticks + 10000` — 10-second delay. If the game closes in that window, position is lost.
- `Task.Run(SaveFile)` — background thread may not complete before process exits.
- No save triggered on `Dispose()` — moving and immediately closing loses position.

**Standard gumps (`gumps.xml`):**
- `Profile.Save()` is called only on `GameScene` unload (`GameScene.cs:520`). A crash or force-close means zero saves since last logout.
- No periodic in-session save.

### Changes

**`XmlGumpHandler.cs` — class `XmlGump`:**
1. Reduce timer: `saveFileAfter = Time.Ticks + 2000` (2 s).
2. In `SaveFile()`, capture `int snapshotX = X; int snapshotY = Y;` before `Task.Run` to avoid race condition.
3. Add `Dispose()` override: if `SavePosition` is true and `saveFileAfter != uint.MaxValue` (i.e., unsaved changes pending), call `SaveFile()` synchronously (no `Task.Run`). Call `base.Dispose()` **after** `SaveFile()` so that `X`, `Y`, and `FilePath` are still valid during the save.

**`GameScene.cs`:** No changes needed. `Profile.Save()` at line 520 is already fully synchronous — `ConfigurationResolver.Save` uses `File.WriteAllText` and `SaveGumps` uses `XmlTextWriter`, both blocking. Standard gumps positions are correctly saved on normal close/logout already.

---

## Fix 2 — Scrollbar Accessible From Any Point on the Lateral Area

### Problem

**`ScrollFlag.Contains`** (`ScrollFlag.cs:174`) uses pixel-perfect hit-testing on the small flag icon:
```csharp
return Client.Game.Gumps.PixelCheck(BUTTON_FLAG, x, y);
```
Only the exact non-transparent pixels of the tiny flag image register a click. Clicking anywhere else in the scroll track (the full height of the area) does nothing.

**`ScrollArea`** (`ScrollArea.cs`) does not forward clicks that land slightly outside the scrollbar child's pixel bounds.

### Changes

**`ScrollFlag.cs` — `Contains`:**
Replace pixel-perfect check with full bounding-box:
```csharp
public override bool Contains(int x, int y)
{
    return x >= 0 && x <= Width && y >= 0 && y <= Height;
}
```

**`ScrollArea.cs` — `OnMouseDown`:**
Add override so that a left-click anywhere in the right-hand strip (X ≥ `_scrollBar.X - 4`) is forwarded to the scrollbar as a position-set click, using Y relative to the scrollbar origin. This catches clicks that fall in the margin between content and scrollbar.

```csharp
protected override void OnMouseDown(int x, int y, MouseButtonType button)
{
    if (button == MouseButtonType.Left && x >= _scrollBar.X - 4 && _scrollBar.IsVisible)
    {
        // Pass raw ScrollArea-local coords; InvokeMouseDown subtracts _scrollBar.X/Y internally
        _scrollBar.InvokeMouseDown(new Point(x, y), button);
        return;
    }
    base.OnMouseDown(x, y, button);
}
```

---

## Fix 3 — Move Border Too Thin + Resize Button Outside Window

### Problem

**Move border:**
`BorderControl` (`WorldViewportGump.cs:382`) renders a visual border of `_borderSize = 4` pixels. Because content controls (added after `BorderControl`) intercept clicks over the content area, the effective drag zone for moving is only those 4px strips — very hard to grab.

**Resize button:**
In `ResizableGump.OnResize` (`ResizableGump.cs:215`):
```csharp
_button.X = Width - (_button.Width >> 0) + 2;   // 2px outside right edge
_button.Y = Height - (_button.Height >> 0) + 2;  // 2px outside bottom edge
```
The button overflows the gump bounds by 2px in both axes. If the parent clips children to its bounds, the button is partially or fully invisible/unclickable.

### Changes

**`BorderControl` — wider hit area without changing visual:**
Override `Contains` to return `true` for the border strip area plus an 8 px inward margin, while keeping the visual border at 4 px:
```csharp
public override bool Contains(int x, int y)
{
    if (x < 0 || x > Width || y < 0 || y > Height)
        return false;
    const int hitMargin = 8;
    // top, bottom, left, right strips
    return y < hitMargin || y > Height - hitMargin
        || x < hitMargin || x > Width - hitMargin;
}
```
Content controls sitting entirely within the inner area still win by z-order. This only widens the grabbable strip from 4 px to 8 px.

**`ResizableGump.OnResize` — fix resize button position:**
Change `+ 2` to `- 2` so the button lands 2px inside the gump:
```csharp
_button.X = Width - _button.Width - 2;
_button.Y = Height - _button.Height - 2;
```

---

## Files Changed

| File | Change |
|------|--------|
| `src/ClassicUO.Client/Game/UI/XmlGumpHandler.cs` | Timer 2 s, snapshot X/Y, save on Dispose |
| `src/ClassicUO.Client/Game/UI/Controls/ScrollFlag.cs` | Contains → bounding box |
| `src/ClassicUO.Client/Game/UI/Controls/ScrollArea.cs` | OnMouseDown → forward right-strip clicks |
| `src/ClassicUO.Client/Game/UI/Gumps/WorldViewportGump.cs` | BorderControl.Contains → 8 px hit margin |
| `src/ClassicUO.Client/Game/UI/Gumps/ResizableGump.cs` | Resize button position |

---

## Out of Scope

- Making the right window border (BorderControl 4 px edge) scroll instead of move. This requires gump-specific knowledge and would conflict with the move handle. Users can still scroll via mouse wheel.
- Changing the visual size of the border.
- Saving scroll positions between sessions.
