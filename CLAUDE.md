# Orc Farm — CLAUDE.md

## Project overview

Orc Farm is a first-person dark-comedy farm management sim built in Unity 6.3 LTS URP.
The player grows orc body parts, assembles complete orcs, and decides whether to keep
them as workers or sell them for profit.

Engine: Unity 6.3 LTS
Render pipeline: URP
Namespace root: OrcFarm

---

## Working rules

- One task at a time
- Keep changes scoped to the target assembly
- Do not refactor stable systems unless a compile error forces it
- Do not touch scene YAML, prefab YAML, or project settings unless explicitly needed
- Prefer MVP-safe implementations over future-proof architecture
- Minimal HUD and prompts only
- Placeholder art and objects are fine
- After implementing any change, read each modified file back and confirm
  the change is present on disk before returning results
- Remove all temporary Debug.Log calls or wrap in [Conditional("UNITY_EDITOR")]
  before marking a task closed

---

## Git workflow

- Never push automatically after making code changes
- Before any commit or push, output this format first:

Changes in this push:
- <file>: <what changed>

Commit message:
- <proposed commit message>

Risk check:
- Compile:
- Inspector:
- Integration:

- Only run git push after explicit approval:
  "push it" / "commit and push" / "send this branch"
- Do not include unrelated files
- Do not commit broken work
- Prefer small commits
- Never touch scene YAML, prefab YAML, or project settings in commits
  unless explicitly requested

---

## Assembly structure

OrcFarm.Core          — shared types, interfaces, enums
OrcFarm.Interaction   — IInteractable, IInteractionService, InteractionDetector
OrcFarm.Player        — PlayerMover, PlayerLook, PlayerInteractor, CarryController
OrcFarm.Farming       — FarmPlot, HarvestedHead, HarvestedHeadPool, HarvestCoordinator
OrcFarm.Inventory     — PlayerInventory, ItemType enum
OrcFarm.Carry         — CarryController, HarvestedHead carry behavior
OrcFarm.Storage       — HeadStorageContainer
OrcFarm.UI            — InteractHUD, result readouts, money display
OrcFarm.Assembly      — AssemblyStation, AssembledOrc
OrcFarm.Workers       — HaulerWorker, KeepInteractable, OrcHoldingPen
OrcFarm.Economy       — BazaarPoint, PlayerWallet, OrcQuality enum
OrcFarm.Tests         — architecture smoke tests

Stable assemblies — do not touch unless a compile error forces it:
OrcFarm.Core, OrcFarm.Interaction, OrcFarm.Player, OrcFarm.Farming,
OrcFarm.Inventory, OrcFarm.Carry, OrcFarm.Storage

---

## Current build state

Completed and stable:
- First-person movement, PlayerLook, PlayerMover, PlayerInteractor
- InteractionDetector with OnTriggerEnter and fallback OverlapSphere scan
- FarmPlot state machine: Empty → Prepared → Fertilized → Planted →
  Growing → NeedsCare → ReadyToHarvest → FailedCrop
- One care checkpoint, missing care causes FailedCrop
- FailedCrop resets to Empty on interaction with no refund
- FarmPlot debug hooks: Force NeedsCare and Force ReadyToHarvest
  one-shot inspector bools in Update, not OnValidate
- Inventory with HeadSeed, Fertilizer, HarvestedHead item types
- PlayerInventory with debug refill via one-shot inspector bool
- Carry system — one head at a time, reuses same world object,
  drop with small random horizontal motion, collider disabled while carried
- Storage system — HeadStorageContainer, LIFO retrieval, StoredCount
- Harvest — head spawns at random XZ offset around plot, lands on ground,
  player picks up manually, no auto-carry
- AssemblyStation — consumes carried head, spawns AssembledOrc from prefab,
  fixed placeholder parts for torso arms legs, result readout auto-clears
- HaulerWorker on AssembledOrc GameObject alongside KeepInteractable
- Keep flow: orc walks to wait point, then begins hauling loop
- Store flow: orc walks to OrcHoldingPen standing spot
- OrcHoldingPen: serialized standing spots, TrySellOne(), StoredCount
- BazaarPoint: sells stored orcs, quality-based pricing
- PlayerWallet: bronze currency, Add(), Balance property
- OrcQuality enum: Low Normal High
- HUD: interact prompts, plot state text, seed count,
  fertilizer count, bronze balance

Not built yet:
- Leg pond farming
- Torso and arm farming
- Bazaar buy point for parts
- One normal contract
- One lord quest and Prestige counter
- Lord seed delivery system
- Trait runtime system
- Pool leak fix for assembly-consumed heads
- Save/load
- Audio
- Tutorial
- Multiple worker roles
- Additional instability types beyond movement slowdown

---

## Task prompt format

Every task prompt must include:

1. Current state summary
2. Target assembly — only one primary assembly per task
3. Allowed changes — explicit list
4. Do not touch — explicit list of stable assemblies
5. Out of scope — explicit list
6. Required behavior — numbered acceptance criteria
7. Implementation constraints — no event bus, no service locator,
   no dependency injection framework, no overengineering
8. Return format:
   - Files changed with disk verification confirmed
   - New classes added
   - Behavior summary
   - What must be manually verified in Play Mode before task is closed
   - Assumptions
   - Limitations

---

## Architecture rules (§1)

§1.1  No business logic in MonoBehaviours that serve as Views
§1.2  No UnityEngine imports in Model layer except value types
§1.3  All dependencies via VContainer constructor injection or [Inject]
§1.4  No static singletons — automatic rejection
§1.5  Controllers orchestrate — never manipulate Transforms or UI directly
§1.6  One responsibility per class — no "And" classes or multi-concern Managers
§1.7  No upward dependency flow — UI may depend on Core, Core never references UI
§1.8  Every injected service must have a corresponding interface
§1.9  States access owner controller only through a context interface

---

## Event system rules (§2)

§2.1  All inter-system communication via MessagePipe signals
§2.2  Signals are readonly structs with immutable fields
§2.3  All signal types registered in RootLifetimeScope
§2.4  Every Subscribe() must have a corresponding Dispose()
§2.5  No UnityEvent for system-to-system communication
§2.6  Signal naming: past tense for notifications, imperative for commands

---

## Memory and GC rules (§3)

§3.1  Zero heap allocations in Update, FixedUpdate, LateUpdate
§3.2  No LINQ in any per-frame code path
§3.3  No string concatenation in hot paths
§3.4  Object pooling mandatory for any object instantiated more than once
§3.5  All pooled objects implement IPoolable with OnGetFromPool,
      OnReturnToPool, ResetState
§3.6  No Instantiate or Destroy during active gameplay
§3.7  Pools must be pre-warmed during scene load
§3.8  Dictionary<TEnum,T> must use custom IEqualityComparer<TEnum>
§3.9  Cache all WaitForSeconds instances — prefer UniTask

---

## Async rules (§4)

§4.1  Zero coroutines — all async work via UniTask
§4.2  Every async method must accept and propagate CancellationToken
§4.3  Fire-and-forget must use .Forget(exceptionHandler)
§4.4  Async entry points implement IAsyncStartable
§4.5  All external async operations wrapped in try-catch
§4.6  No async void — use async UniTask or async UniTaskVoid

---

## Performance rules (§5)

§5.1  No GameObject.Find, FindObjectOfType, FindObjectsOfType anywhere
§5.2  No GetComponent<T> in Update or per-frame methods — cache in Awake
§5.3  Use Physics.OverlapSphereNonAlloc and all NonAlloc physics variants
§5.4  Use sqrMagnitude for distance comparisons, not Vector3.Distance
§5.5  Use MaterialPropertyBlock for per-instance material changes
§5.6  Animator parameters accessed via hashed IDs cached in static readonly int
§5.7  No Debug.Log in per-frame code — wrap with [Conditional("UNITY_EDITOR")]
§5.9  No hard-coded layer indices or tag strings

---

## ScriptableObject rules (§6)

§6.1  All tunable game data lives in ScriptableObjects
§6.2  [CreateAssetMenu] on all data SOs
§6.3  OnValidate implemented on all data SOs with range clamping
§6.4  Runtime Validate() method on data SOs called during initialization
§6.5  No hard-coded gameplay values in scripts
§6.6  Addressable references for prefabs and assets

---

## State machine rules (§7)

§7.1  States must not reference concrete controller types
§7.2  Terminal states must not transition to other states
§7.3  Each state class in its own file
§7.4  State classes are internal sealed unless inheritance is documented
§7.5  No state-specific logic in the controller

---

## Error handling rules (§8)

§8.1  Fail-fast on missing mandatory dependencies — throw in Awake or constructor
§8.2  No defensive ?. on dependencies that must exist
§8.3  SerializeField fields validated in Awake — if null log error and disable
§8.4  Addressables loads wrapped in try-catch
§8.5  All IDisposable objects disposed

---

## Code style rules (§9)

§9.1  Namespace: OrcFarm root, sub-namespaces by assembly
      e.g. OrcFarm.Farming, OrcFarm.Workers, OrcFarm.Economy
§9.2  Private fields: _camelCase with underscore prefix
§9.3  Public properties: PascalCase, prefer read-only
§9.4  Explicit access modifiers on all types, methods, and fields
§9.5  Braces required on all if/for/foreach/while
§9.6  One class per file, filename matches class name exactly
§9.7  XML documentation on all public APIs
§9.8  No magic numbers — use const or static readonly with descriptive names
§9.9  No commented-out code — delete dead code, use version control to recover

---

## Common rejections quick reference

| Violation                    | Rule  | Fix                                          |
|------------------------------|-------|----------------------------------------------|
| public static Instance       | §1.4  | Register in VContainer, inject via interface |
| FindObjectOfType<T>()        | §5.1  | Inject dependency via VContainer             |
| GetComponent<T>() in Update  | §5.2  | Cache in Awake or inject                     |
| renderer.material.color = x  | §5.5  | Use MaterialPropertyBlock                    |
| StartCoroutine(...)          | §4.1  | Convert to async UniTask with CancellationToken |
| new WaitForSeconds in loop   | §3.9  | Cache as static readonly field               |
| Instantiate() during gameplay| §3.6  | Use object pool                              |
| String concat in Update      | §3.1  | Cache string, rebuild only on change         |
| Debug.Log in Update          | §5.7  | Wrap with [Conditional("UNITY_EDITOR")]      |
| LINQ in per-frame code       | §3.2  | Replace with explicit loop                   |
| async void method            | §4.6  | Use async UniTask or async UniTaskVoid        |
| Bare .Forget()               | §4.3  | Add exception handler                        |
| Mutable signal class         | §2.2  | Convert to readonly struct                   |
| Hard-coded layer index       | §5.9  | Use serialized LayerMask or named constant   |

---

## Currency

bronze → silver → gold → platinum (copper may precede bronze)
Current active currency: bronze

## Quality tiers

Low = 10 bronze
Normal = 25 bronze
High = 50 bronze

## Demo scope

Two farming methods: head (pull from ground) and legs (pond fishing)
Full orc rule intact: player grows head and legs, buys torso and arms from bazaar
One worker role: hauler
One instability type: movement slowdown
Traits: Hardworking, Clumsy, Lazy, Angry, Thief, Resilient
Quality tiers: Low, Normal, High
Prestige: 2 visible ranks maximum you can use them
