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
- Hotbar system with 5 slots, number key selection, scroll selection, and deselect by pressing selected number again
- Hotbar items: HeadSeed, Fertilizer, FeedItem, LegFry, WaterItem
- HarvestedHead and HarvestedLeg are physical world objects, not hotbar items
- FarmActionUI with crosshair, FarmFocusDetector, tile highlight, and F/W/C action buttons
- HeadFarmTile system with 3x3 HeadFarmPlot grid
- HeadFarmTile flow: Empty → Tilled → Seeded → Covered → Growing → ReadyToHarvest
- HeadFarmTile death flow: Growing → Dead → Empty when cleared
- HeadFarmTile tracks FeedScore, WaterScore, and CareScore
- LegPond system with LegFry stocking, per-fish FeedScore, CareScore, and IsAlive tracking
- LegPond supports Growing, NeedsCare, ReadyToHarvest, and Dead states
- LegPond harvests one HarvestedLeg per E interaction from alive fish only
- HUD/readout supports interact prompts and temporary result messages
- Harvest result readout shows harvested part type and quality
- AssemblyStation accepts direct head and leg deposits with two internal slots
- AssemblyStation allows taking back deposited parts before assembly
- AssemblyStation assembles one orc from deposited head and leg
- Editor-only LegPond debug panel exists and must be preserved

Recently scoped / in progress:
- Trait Assignment V1
  - Core trait enums and influence flags
  - Part-local trait candidates stored on HarvestedHead and HarvestedLeg
  - Assembly chooses final trait from head and leg candidates
  - Final trait displayed in result UI
  - No runtime worker behavior changes yet

Not built yet:
- Trait runtime behavior effects
- Hauler trait behavior integration
- Bazaar buy point for torso and arms
- One normal contract
- One lord quest and Prestige counter
- Lord seed delivery system
- Save/load
- Audio
- Tutorial
- Multiple worker roles
- Additional instability types beyond current movement slowdown
- Torso farming
- Arm farming
- Seasons
- Attacks/events
- Ring/artifact progression

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

§1.1  Keep architecture small and concrete
§1.2  Do not introduce VContainer, dependency injection, service locators, or event buses
§1.3  Do not introduce MessagePipe or new signal systems
§1.4  No static singleton managers
§1.5  Keep changes scoped to the requested assembly
§1.6  Do not refactor stable systems unless a compile error forces it
§1.7  UI may read simple public properties or explicit read-only methods, but must not own gameplay state
§1.8  Core may contain shared enums, value structs, interfaces, and helper methods only
§1.9  Avoid generic frameworks unless the task explicitly asks for one

---

## Event / communication rules (§2)

§2.1  Prefer direct serialized references or explicit method calls for this prototype
§2.2  Do not add an event bus
§2.3  Do not add MessagePipe
§2.4  Do not add UnityEvent-based system communication unless explicitly requested
§2.5  Keep cross-assembly communication minimal and readable

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

§4.1  Avoid async systems unless the task explicitly requires them
§4.2  Coroutines are allowed for simple UI auto-clear timers if already used by the project
§4.3  Do not add UniTask unless it already exists in the project and the task explicitly needs it
§4.4  Do not convert existing simple coroutine/UI timing code into async code during unrelated tasks

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

## Layer rules (§5.10)

§5.10.1 Never use hard-coded layer indices
§5.10.2 Use LayerMask.NameToLayer("LayerName") or serialized LayerMask fields
§5.10.3 Approved layers:
- Default (0)
- Player (8)
- Interactable (9)
- Carriable (10)
- Worker (11)
- FarmTile (12)
- HoldingPen (13)
§5.10.4 If a task requires a layer not listed here, stop and report it in Assumptions
§5.10.5 Never set the Physics collision matrix via code
§5.10.6 Note required collision matrix changes under Inspector risks

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

Two farming methods: head and legs
Head method: tile/grid farming, pull harvest
Leg method: pond stocking and leg harvest
Full orc rule remains the design direction: head, torso, arms, legs
Current prototype assembly uses head + leg only as a temporary implementation step
One worker role: hauler
One instability type for demo: movement slowdown first
Quality tiers: Low, Normal, High
Trait Assignment V1 traits:
- Brutish
- Resilient
- Diligent
- Bone-Idle
- Clumsy
- Twitchy
Trait runtime behavior effects are not active yet
Prestige: 2 visible ranks maximum later, not active in current prototype


