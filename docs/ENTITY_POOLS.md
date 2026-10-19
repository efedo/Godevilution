# Entity pool contract

Godevilution keeps the native fixed-size entity arrays available while the
server-authoritative runtime replaces their gameplay ownership. The pool types
in `legacy-native/utils/container/entity_pool.hpp` own storage and expose active
iteration; the game-specific adapters preserve the legacy array names and
save/network index contracts.

## Allocation policies

- `SparseAllocationPolicy` keeps a pool index stable until that entity is
  released. Use it when a tile, save record, or network projection refers to an
  entity by slot.
- `DenseAllocationPolicy` packs active entries by swapping the final active
  entry into a released slot. Use it only when callers can tolerate index
  changes or remap references after release.

Every pool starts empty with its sparse free-list initialized. `allocate()`
returns `nullptr` at capacity. `deallocate(index)` ignores an out-of-range or
already inactive index, so repeated release calls cannot corrupt bookkeeping.

## Native compatibility adapters

`Items`, `Monsters`, and `Objects` are aliases into pool-owned storage. The
legacy active-index arrays remain the authoritative compatibility view for
native code, so a connected presentation path must use the corresponding
adapter operations and then reconcile stale slots against the authoritative
snapshot.

Do not mix direct `DenseEntityPool::allocate()`/`deallocate()` calls with the
object adapter's `ReserveObjectSlot`/`ReleaseObjectSlot` helpers. The adapter
maintains the legacy active and available arrays explicitly; mixing both APIs
would make those bookkeeping views diverge. A future migration may replace
the adapter internals, but it must preserve slot stability and the existing
array aliases until all native compatibility consumers are removed.

## Reset and presentation lifecycle

`InitializeItemPool`, `InitializeMonsterPool`, and `InitializeObjectPool` clear
pool storage at level/session boundaries. `ServerBackedWorldPresentation::Apply`
claims only slots represented by the current authoritative snapshot and removes
unclaimed compatibility entities before rendering or interaction. `Clear()`
releases every mapped slot and remote presentation, and is required before a
connected session changes the active native level.

The compatibility pools are presentation storage, not a second simulation. The
authoritative server owns entity identity, movement, combat, random rolls,
world mutation, and persistence.
