# OOP Refactoring Opportunities

> Historical document. This July 2026 native-code analysis predates the current
> Godevilution server/client migration status. Its observations are retained as
> refactoring context, not as a current roadmap.
> Use [the completion plan](../completion-plan.md) for current priorities;
> native OOP refactoring is not an independent completion requirement.

Analysis of the Godevilution native codebase for opportunities to refactor free functions into object-oriented patterns.

## Overview

The codebase is in a mid-transition state. Several areas have been modernized (Actor/Monster hierarchy, Level class, World class, AnimationInfo, Options system), but most modules remain heavily C-like: free functions operating on global arrays of plain structs. Approximately 700+ free functions exist across the codebase.

## Methodology

Each module was evaluated on:
- Number of free functions
- Quantity of global state
- How tightly coupled functions are to specific data structures
- Whether clear grouping patterns exist

---

## Tier 1: Highest Value

### 1. `game/missiles/` (~90 free functions)

**Current state:** Massive jump-table of ~55 `Add*` and ~35 `Process*` functions dispatched via function pointer arrays. The `Missile` struct stores weakly-typed `var1`-`var7` fields. Each missile type (arrow, firebolt, fireball, lightning, guardian, teleport, etc.) has its own `Add*` and `Process*` function.

**Completed (2025-06):** Replaced raw `var1`-`var7` fields with a typed anonymous union providing named struct accessors per missile family (projectile, guardian, portal, rune, wall, chain lightning, inferno, nova, firewall, lightning, golem). Backward-compatible: `var1`-`var7` remain accessible directly.

**Future target:** Polymorphic missile class hierarchy with `virtual add()`, `process()`, and `collision()` methods.

**Benefits:**
- Eliminates the giant function pointer dispatch tables
- Replaces `var1`-`var7` with typed member variables per missile type
- Each missile type becomes a self-contained class
- New missile types can be added without modifying dispatch logic

---

### 2. `engine/` lighting (~19 free functions, ~15 globals)

**Current state:** Functions like `DoLighting()`, `DoUnLight()`, `AddLight()`, `AddUnLight()`, `ChangeLightRadius()`, `ChangeLightXY()`, `ChangeLightOffset()`, `ProcessLightList()`, `ProcessVisionList()`, `InitLighting()`, `MakeLightTable()`, `LoadTrns()`, `ActivateVision()`, `ChangeVisionRadius()`, `ChangeVisionXY()`, `SavePreLighting()`, `lighting_color_cycling()` operate on global `Lights[]`, `VisionList[]`, `ActiveLights[]`, `LightTables[]` arrays.

**Refactoring target:** `LightManager` class encapsulating all lighting state and operations.

**Benefits:**
- Eliminates ~15 global arrays/variables
- Clear lifecycle (init, process, render)
- Testable without global state

---

### 3. `game/levels/` theme rooms + triggers (~55 free functions + 20 inline accessors)

**Current state:**
- `themes.h`: `numthemes`, `themes[]`, `armorFlag`, `weaponFlag`, `zharlib` are module-level globals
- `triggers.h`: `trigs[]`, `numtrigs`, `trigflag` are module-level globals
- `crypt.h`: `UberRow`, `UberCol`, `IsUberRoomOpened`, etc. are module-level globals
- `dungeon_common.h`: ~20 inline accessor functions to `Level` member variables

**Completed (2025-06):** Moved all 12 scalar globals (numthemes, armorFlag, weaponFlag, zharlib, trigflag, numtrigs, TWarpFrom, UberRow, UberCol, IsUberRoomOpened, IsUberLeverActivated, UberDiabloMonsterIndex) into `Level` class with inline accessor functions in `dungeon_common.h`. Removed extern declarations from `themes.h`, `triggers.h`, `crypt.h`. Arrays `themes[]` and `trigs[]` remain as namespace globals due to circular include constraints.

**Benefits:**
- Each `Level` instance carries its own state (important for future multi-level support)
- Eliminates module-level global arrays (partial - arrays remain)
- Clear ownership of data

---

## Tier 2: Good Candidates

### 4. `engine/` cursor (~10 free functions, ~10 globals)

**Current state:** `InitCursor()`, `NewCursor()`, `CheckCursMove()`, etc. operate on `pcurs`, `cursPosition`, `ObjectUnderCursor`, etc.

**Refactoring target:** `CursorManager` class.

### 5. `qol/` visual_store (~25 free functions, 5+ globals)

**Current state:** `InitVisualStore()`, `OpenVisualStore()`, `DrawVisualStore()`, etc. operate on `VisualStore`, `IsVisualStoreOpen`, `pcursstoreitem`, etc.

**Refactoring target:** `VisualStore` class with methods.

### 6. `game/stores/` (~30 free functions)

**Current state:** Vendor management functions for Smith, Witch, Boy, Healer, etc.

**Completed (2025-06):** Refactored into `StoreManager` class with singleton `CurrentStoreManager`. 19 module-level globals + 27 public functions moved into class. Updated ~30 caller files.

### 7. `ui/` automap (~15 free functions, 5+ globals)

**Current state:** `InitAutomap()`, `DrawAutomap()`, `StartAutomap()`, etc. with `AutomapActive`, `AutoMapScale`, `AutomapOffset`, etc.

**Completed (2026-07):** Refactored into `AutomapManager` class with singleton `CurrentAutomapManager`. 8 globals + 13 public functions moved into class. Updated ~18 caller files.

---

## Tier 3: Lower Priority

| Module | Free Functions | Notes |
|--------|---------------|-------|
| `game/items/` | ~70 | Large surface but mid-refactor (ItemPoolAdapter exists) |
| `game/monsters/` | ~50 | Monster struct itself is excellent OOP with ~40 methods |
| `controls/` | ~35 | Already has namespace organization |
| `persistence/` | ~30 | Inherently procedural; Options system is good OOP |
| `game/objects/` | ~30 (+20 in ObjectManager) | ObjectManager namespace is already a facade |
| `network/` | ~20 | abstract_net transport is polymorphic |
| `game/quests/` | ~20 | Small modules, low impact |
| `game/spells/` | ~10 | Small module |

## Not Worth Refactoring

| Module | Free Functions | Reason |
|--------|---------------|--------|
| `data/` | ~8 | Already well-OOP (RecordReader, ValueReader classes) |
| `game/players/` | ~25 | Player struct has ~80 methods, most modern in codebase |
| `engine/` random | ~10 | Already has DiabloGenerator, SplitMix64, etc. classes |
| `game/towners/` | ~10 | Small, self-contained |
| `game/portals/` | ~10 | Small, self-contained |

## Architectural Observations

1. **Entity management pattern**: Items, monsters, and objects all follow a similar pattern (global array + active-indices + pool template). Consistent candidate for a generalized entity management system.

2. **Global state is the primary problem**: Nearly all free functions operate on implicit global state (`currentLevel()`, `MyPlayer`, `Players[]`, `Monsters[]`, `Items[]`, `ActiveItems[]`). The missiles module is the worst offender.

3. **Player struct as a model**: The `Player` struct with ~80 methods and well-organized inventory/belt iterator system serves as a model for how other modules should be structured.
