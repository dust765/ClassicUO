# RAM Reduction — Design Spec (Final)

**Date:** 2026-03-26
**Change:** Single component — Orphan Item Cleaner in `World.cs`
**Expected gain:** 20–100 MB (depends on session length and server activity)
**Risk level:** Low

---

## Context

The existing engine already handles most cleanup correctly:
- `Map.ClearUnusedBlocks()` — removes idle chunks every 500ms
- `World.Update()` removes out-of-range mobiles and their linked-list items via `RemoveMobile()` → `RemoveItem()` → deferred `Items.Remove()` via `_toRemove`

**The identified gap:** Items added to `World.Items` with `item.Container` pointing to a serial that was never in any mobile's `LinkedObject.Items` linked list (protocol edge cases, out-of-order packets). These items are invisible to `RemoveMobile()` and accumulate indefinitely.

---

## Solution — Orphan Item Cleaner

A periodic sweep inside `World.Update()` that finds items whose parent container no longer exists in the world.

### Trigger

Every 60 seconds via a new `static uint _orphanSweepTime` field in `World.cs`.
**Must be placed inside the existing `if (Player != null)` guard block.**

### Algorithm

After the existing `_toRemove` flush for items has completed (after line ~389), collect orphans into a **separate** `List<uint> _orphanRemove` (local or reused static — not `_toRemove`, which is already cleared by then):

```
for each kvp in Items:
    item = kvp.Value
    skip if item.IsDestroyed
    skip if item.Container == 0xFFFF_FFFF              (on ground)
    skip if !SerialHelper.IsValid(item.Container)       (zero or garbage)
    skip if item.Container == Player.Serial             (player backpack root)
    skip if Items.Contains(item.Container)              (parent item exists)
    skip if Mobiles.Contains(item.Container)            (parent mobile exists)
    skip if UIManager.GetGump<ContainerGump>(item.Container) != null
    skip if UIManager.GetGump<GridContainer>(item.Container) != null
    skip if UIManager.GetGump<PaperDollGump>(item.Container) != null
    skip if UIManager.GetGump<GridLootGump>(item.Container) != null
    skip if ItemHold.Enabled && ItemHold.Serial == item.Serial
    skip if CorpseManager.Exists(item.Container, 0)     (two-arg form, corpse slot only)
    → orphan: add item.Serial to _orphanRemove (stop collecting at 100)

for each serial in _orphanRemove:
    RemoveItem(serial, true)

_orphanRemove.Clear()
```

### State fields added to World.cs

```csharp
private static uint _orphanSweepTime = 0;
private static readonly List<uint> _orphanRemove = new List<uint>();
```

---

## Files to Modify

| File | Change |
|---|---|
| `src/ClassicUO.Client/Game/World.cs` | Add two fields + orphan sweep block in `Update()` |

No new files. No changes to `RemoveMobile`, `RemoveItem`, or any other method.

---

## Edge Cases

| Scenario | Handled by |
|---|---|
| Item on ground | `Container == 0xFFFF_FFFF` skip |
| Invalid/zero Container | `!SerialHelper.IsValid()` skip |
| Player backpack chain | `Container == Player.Serial` skip (deeper items have valid parent in `Items`) |
| Parent container still in world | `Items.Contains` skip |
| Parent mobile still in world | `Mobiles.Contains` skip |
| ContainerGump open | `GetGump<ContainerGump>` skip |
| GridContainer open | `GetGump<GridContainer>` skip |
| PaperDoll open | `GetGump<PaperDollGump>` skip |
| GridLootGump open (looting corpse) | `GetGump<GridLootGump>` skip |
| Item being dragged | `ItemHold.Serial == item.Serial` skip |
| Corpse contents | `CorpseManager.Exists(item.Container, 0)` skip |
| Already-destroyed items | `item.IsDestroyed` skip |
| Dictionary mutation during loop | Collect first, remove after loop using `_orphanRemove` |
| Player is null | Sweep inside `if (Player != null)` guard |

---

## Success Criteria

- `World.Items.Count` grows slower during long sessions with many NPCs
- No exceptions from orphan removal
- Open containers remain functional
- No items removed that are visible or interactable
