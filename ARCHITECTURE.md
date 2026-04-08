# Orc Farm — Assembly Architecture

## Overview

All game code lives under `Assets/Scripts/`. Each folder maps 1-to-1 to an
assembly definition (`.asmdef`). `autoReferenced: false` on every assembly —
dependencies are always explicit.

---

## Assembly Map

| Assembly | Folder | Purpose |
|---|---|---|
| `OrcFarm.Core` | `Core/` | Shared contracts and value types referenced by all other assemblies. No UnityEngine beyond value types (Vector3, Quaternion, Color). |
| `OrcFarm.Interaction` | `Interaction/` | `IInteractable`, `IInteractionService`, and the runtime detector that finds targets via overlap queries. |
| `OrcFarm.Player` | `Player/` | Input reading (`PlayerInputWrapper`), player state machine, camera rig controller. **No farming or inventory logic.** |
| `OrcFarm.Farming` | `Farming/` | Plot lifecycle, growth timers, harvest triggers. Talks to Inventory for output items and Interaction for plot interactables. |
| `OrcFarm.Inventory` | `Inventory/` | Item type definitions, stack data, and the in-memory inventory model. Pure C# — no MonoBehaviours. |
| `OrcFarm.Carry` | `Carry/` | Head-carry socket, pick-up / put-down state. References Inventory for carried item identity. |
| `OrcFarm.Storage` | `Storage/` | Storage containers and transfer logic. References Carry (what is being held) and Inventory (what to store). |
| `OrcFarm.UI` | `UI/` | All screen and HUD views. **One-way dependency only** — gameplay assemblies must never import UI. |
| `OrcFarm.Tests` | `Tests/` | Edit-mode and play-mode unit tests. References all gameplay assemblies; never referenced by them. |

---

## Dependency Graph

```
                        ┌─────────┐
                        │  Core   │
                        └────┬────┘
               ┌─────────────┼──────────────────┐
               ▼             ▼                  ▼
        ┌────────────┐  ┌──────────┐     ┌───────────┐
        │Interaction │  │ Inventory│     │  (others) │
        └──────┬─────┘  └────┬─────┘     └───────────┘
               │             │
       ┌───────┼─────────────┤
       ▼       ▼             ▼
  ┌────────┐ ┌──────┐  ┌─────────┐  ┌─────────┐
  │ Player │ │Carry │  │ Farming │  │ Storage │
  └────────┘ └──────┘  └─────────┘  └─────────┘
       │          │          │            │
       └──────────┴──────────┴─────┬──────┘
                                   ▼
                               ┌──────┐
                               │  UI  │
                               └──────┘
```

### Rules
- **Core** → no OrcFarm dependencies
- **Interaction** → Core only
- **Inventory** → Core only
- **Player** → Core, Interaction
- **Farming** → Core, Interaction, Inventory
- **Carry** → Core, Interaction, Inventory
- **Storage** → Core, Interaction, Carry, Inventory
- **UI** → all gameplay assemblies (read-only fan-in)
- **Tests** → all assemblies (never referenced back)

**Upward flow is a hard rejection (§1.7).** If Core needs to call something in
Farming, introduce a Core interface and have Farming register an implementation.

---

## Conventions

- **Namespace root:** `OrcFarm.<Assembly>` (e.g. `OrcFarm.Player`)
- **Private fields:** `_camelCase`
- **Public properties:** `PascalCase`, read-only by default
- **Interfaces:** `I`-prefixed, one per file
- **One class per file**, filename matches class name exactly

---

## What Each System Should Own

### Core
- Shared enums that cross assembly boundaries (if any arise)
- Custom exception types
- Value objects with no behaviour

### Interaction
- `InteractionDetector` MonoBehaviour: overlap sphere, updates `CurrentTarget`
- `IInteractionService` implementation

### Player
- `PlayerController` (state machine entry point — no Transform manipulation directly)
- `PlayerInputWrapper` — already present
- Camera look controller

### Farming
- `FarmPlot` MonoBehaviour implementing `IInteractable`
- Growth / harvest state machine
- ScriptableObjects: `CropDefinition`

### Inventory
- `ItemType` enum
- `ItemStack` struct (`ItemType Type`, `int Count`)
- `Inventory` model class (pure C#)

### Carry
- `CarryService` — tracks what the player is currently holding
- `ICarryable` — implemented by objects that can be picked up

### Storage
- `StorageContainer` implementing `IInteractable`
- Transfer logic between `CarryService` and an `Inventory`

### UI
- HUD, interaction prompts, inventory screen
- Reads from gameplay models; never mutates them directly (use signals/events)

---

## Manual Unity Editor Steps Required

1. **Open the project in Unity 6.3 LTS** — asmdef files are recognised on import.
2. **Verify Input System backend** — confirm `Edit > Project Settings > Player > Active Input Handling` is set to **Input System Package (New)** or **Both**.
3. **Test Runner** — open `Window > General > Test Runner`, switch to *Edit Mode*, and confirm `OrcFarm.Tests` assembly appears.
4. **Add VContainer** (future) — when DI is introduced, add `"VContainer"` to the `references` array of each consuming assembly's `.asmdef`.
5. **Add UniTask** (future) — required before any async work. Add `"UniTask"` reference to affected assemblies.

---

## Next Safest Task

**Implement `InteractionDetector`** in `OrcFarm.Interaction`:
- A MonoBehaviour that uses `Physics.OverlapSphereNonAlloc` (§5.3) to find
  nearby `IInteractable` components each frame
- Implements `IInteractionService`
- No gameplay logic — purely detects and exposes `CurrentTarget`
- Can be unit-tested with fake `IInteractable` stubs in `OrcFarm.Tests`
