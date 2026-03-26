# RAM Reduction — Design Spec

**Date:** 2026-03-26
**Approach:** B — Chunk Unloading + World Objects Periodic Cleanup
**Expected gain:** 100–300 MB
**Risk level:** Medium

---

## Overview

Two independent components reduce RAM usage at runtime without removing assets or degrading gameplay:

1. **Chunk Unloader** — auto-recycles map chunks beyond render distance using existing pool infrastructure
2. **World Object Cleaner** — periodically removes `World.Items` and `World.Mobiles` entries outside view range

Both are orchestrated by a new `MemoryManager` static class called from `GameScene.Update()`.

---

## Architecture

```
GameScene.Update()
    └── MemoryManager.Update()
            ├── CleanChunks()    [every 5s]
            └── CleanWorldObjects()  [every 30s]
```

---

## Component A — Chunk Unloader

### Trigger
Every 5 seconds (`Time.Ticks` based timer).

### Logic
1. Get player tile position → convert to chunk coordinates (divide by 8)
2. Iterate `Map.Chunks` (existing 2D array, skip nulls)
3. For each chunk, compute distance from player in chunk-space
4. **Unload condition:** `distance > (MaxRenderDistance / 8) + 2` AND `Time.Ticks - chunk.LastAccessTime > 10_000`
5. Call `chunk.Destroy()` → returns `Land`/`Static` objects to their pools
6. Set `Map.Chunks[cx, cy] = null`

### Safety
- `LastAccessTime` is updated every time a chunk is accessed during rendering — active chunks are never removed
- Only chunks with no recently-accessed flag are eligible
- Player teleport (recall/gate): destination chunks have fresh `LastAccessTime` from the new render cycle

---

## Component B — World Object Cleaner

### Trigger
Every 30 seconds (`Time.Ticks` based timer).

### Logic

**Items cleanup:**
1. Compute safe distance = current view range + 4 tile buffer
2. Iterate `World.Items`
3. Build removal list for items where ALL are true:
   - Distance from player > safe distance
   - `item.Container == 0xFFFF_FFFF` (on ground, not inside a container)
   - No `ContainerGump` open for this item's serial
   - Parent container (if any) is not in exclusion list
4. Call `item.Destroy()` and remove from `World.Items`

**Mobiles cleanup:**
1. Same distance threshold
2. Exclude: `World.Player`, any mobile that is `World.Player.LastAttack`, any mobile attacking player
3. Call `mob.Destroy()` and remove from `World.Mobiles`

### Exclusion rules (items)
| Condition | Reason |
|---|---|
| `item.Serial == World.Player.Serial` | Never remove player |
| `item.Container == World.Player.Serial` | Equipped items |
| `UIManager.GetGump<ContainerGump>(item.Serial) != null` | Open container |
| Item inside an open container | Preserves container contents |

### Re-spawn behavior
If a removed mobile/item reappears in range, the server re-sends the spawn packet. This is identical to existing behavior on large maps and poses no gameplay risk.

---

## MemoryManager — Timer Implementation

```csharp
public static class MemoryManager
{
    private static uint _nextChunkClean = 0;
    private static uint _nextWorldClean = 0;

    public static void Update()
    {
        if (World.Player == null) return;

        if (Time.Ticks >= _nextChunkClean)
        {
            CleanChunks();
            _nextChunkClean = Time.Ticks + 5_000;
        }

        if (Time.Ticks >= _nextWorldClean)
        {
            CleanWorldObjects();
            _nextWorldClean = Time.Ticks + 30_000;
        }
    }
}
```

---

## What We Do NOT Touch

- `ArtLoader`, `GumpsLoader`, `AnimationsLoader` — asset caches (read-only indexes; removal has no practical benefit)
- `TextureCacheManager` — already has a configurable max size
- Object pool sizes — we return objects to pools, not shrink pools
- Chunks with any recently-accessed timestamp < 10s

---

## Edge Cases

| Scenario | Handled by |
|---|---|
| Player teleports | `LastAccessTime` freshness on destination chunks |
| Open container out of range | `ContainerGump` check + parent chain exclusion |
| NPC in combat with player | `LastAttack` / attacker check on mobiles |
| Item on ground being looted | 4-tile buffer + `LastAccessTime` |
| World.Player is null | Guard at top of `MemoryManager.Update()` |
| Scene unload / disconnect | `GameScene.Unload()` handles full cleanup independently |

---

## Files to Create / Modify

| File | Action |
|---|---|
| `src/ClassicUO.Client/Game/Scenes/MemoryManager.cs` | **Create** — new static class |
| `src/ClassicUO.Client/Game/Scenes/GameScene.cs` | **Modify** — call `MemoryManager.Update()` in `Update()` |
| `src/ClassicUO.Client/Game/Map.cs` | **Read-only reference** — access `Chunks` array and chunk methods |

---

## Success Criteria

- RAM usage decreases by 100–300 MB during extended play sessions
- No visible pop-in or missing objects within view range
- No crashes or null reference exceptions from removed objects
- Containers remain fully functional when open
