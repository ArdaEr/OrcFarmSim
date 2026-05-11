using System;
using MessagePipe;
using OrcFarm.Carry;
using OrcFarm.Core;
using OrcFarm.Farming;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using OrcFarm.Player;
using OrcFarm.Quests;
using OrcFarm.Storage;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace OrcFarm.App
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [Header("Scene service instances")]
        [SerializeField] private PlayerInputSource    _inputSource;
        [SerializeField] private InteractionDetector  _interactionDetector;
        [SerializeField] private PlayerInventory      _playerInventory;
        [SerializeField] private CarryController      _carryController;

        [Header("Injected scene consumers")]
        [SerializeField] private PlayerInteractor _playerInteractor;
        [SerializeField] private PlayerMover      _playerMover;
        [SerializeField] private PlayerLook       _playerLook;
        [SerializeField] private HeadFarmPlot[]   _headFarmPlots;

        [Header("Injected scene consumers (continued)")]
        [SerializeField] private OrcFarm.UI.InteractHUD          _interactHud;
        [SerializeField] private HeadStorageContainer[]           _headStorageContainers;
        [SerializeField] private LegStorageContainer[]            _legStorageContainers;
        [SerializeField] private LegPond[]                        _legPonds;

        [Header("HarvestedHead pool")]
        [Tooltip("HarvestedHeadPool MonoBehaviour in the scene. " +
                 "Assign after adding the component to an appropriate GameObject.")]
        [SerializeField] private HarvestedHeadPool _headPool;

        [Header("Hotbar item presenter")]
        [Tooltip("HotbarItemPresenter MonoBehaviour on the player. " +
                 "Auto-resolved from the scene if left unassigned.")]
        [SerializeField] private HotbarItemPresenter _hotbarItemPresenter;

        [Header("Quest definitions")]
        [Tooltip("Quest definitions registered at scene start.")]
        [SerializeField] private QuestDefinition[] _questDefinitions;

        [Tooltip("Prototype helper. Starts every assigned quest definition when the scene scope builds.")]
        [SerializeField] private bool _startAssignedQuestDefinitions = true;

        [Header("Quest action publishers")]
        [Tooltip("Scene components that implement IQuestObjectiveActionPublisherTarget.")]
        [SerializeField] private MonoBehaviour[] _questActionPublisherTargets;

        protected override void Configure(IContainerBuilder builder)
        {
            ResolveSceneReferences();
            ValidateSceneReferences();

            var pipeOptions = builder.RegisterMessagePipe();
            builder.RegisterBuildCallback(c =>
            {
                GlobalMessagePipe.SetProvider(c.AsServiceProvider());

                // Wire the pool into CarryController after the container is built.
                // CarryController is in OrcFarm.Carry which cannot reference OrcFarm.Farming
                // (circular dependency), so the pool is set via a public setter rather than
                // [Inject] — no change to Carry.asmdef required.
                _carryController.SetPool(c.Resolve<IHarvestedHeadPool>());

                // Wire inventory and inventory-full callback into HotbarItemPresenter.
                // SetInventory must be called first — it creates the drop pools that
                // SetInventoryFullCallback iterates.
                if (_hotbarItemPresenter != null)
                {
                    _hotbarItemPresenter.SetInventory(c.Resolve<IPlayerInventory>());
                    _hotbarItemPresenter.SetInventoryFullCallback(
                        () => _interactHud.ShowInventoryFullWarning());
                }

                IPublisher<QuestObjectiveActionSignal> questActionPublisher =
                    c.Resolve<IPublisher<QuestObjectiveActionSignal>>();

                if (_questActionPublisherTargets != null)
                {
                    for (int i = 0; i < _questActionPublisherTargets.Length; i++)
                    {
                        if (_questActionPublisherTargets[i] is IQuestObjectiveActionPublisherTarget target)
                        {
                            target.SetQuestActionPublisher(questActionPublisher);
                        }
                    }
                }

                InjectMultiInstanceSceneComponents(c);

                c.Resolve<QuestProgressProxy>().Start();
            });

            builder.RegisterMessageBroker<CropHarvestedSignal>(pipeOptions);
            builder.RegisterMessageBroker<QuestProgressSignal>(pipeOptions);
            builder.RegisterMessageBroker<QuestObjectiveActionSignal>(pipeOptions);

            QuestService questService =
                new QuestService(_questDefinitions ?? Array.Empty<QuestDefinition>());

            if (_startAssignedQuestDefinitions && _questDefinitions != null)
            {
                for (int i = 0; i < _questDefinitions.Length; i++)
                {
                    if (_questDefinitions[i] != null)
                    {
                        questService.TryStartQuest(_questDefinitions[i].QuestId);
                    }
                }
            }

            builder.RegisterInstance<IQuestService>(questService);
            builder.Register<QuestProgressProxy>(Lifetime.Singleton).AsSelf();

            builder.RegisterComponent<IPlayerInputProvider>(_inputSource);
            builder.RegisterComponent<IInteractionService>(_interactionDetector);
            builder.RegisterComponent<IPlayerInventory>(_playerInventory);
            builder.RegisterComponent<ICarryController>(_carryController);

            builder.RegisterComponent(_playerInteractor);
            builder.RegisterComponent(_playerMover);
            builder.RegisterComponent(_playerLook);
            builder.RegisterComponent(_interactHud);

            // Register the scene-placed MonoBehaviour pool as IHarvestedHeadPool.
            // Both HarvestCoordinator (constructor injection) and CarryController
            // (RegisterBuildCallback above) receive this instance.
            builder.RegisterComponent<IHarvestedHeadPool>(_headPool);

            builder.RegisterEntryPoint<HarvestCoordinator>(Lifetime.Singleton);

        }

        private void InjectMultiInstanceSceneComponents(IObjectResolver container)
        {
            if (_legPonds != null)
            {
                for (int i = 0; i < _legPonds.Length; i++)
                {
                    if (_legPonds[i] != null)
                    {
                        container.Inject(_legPonds[i]);
                    }
                }
            }

            if (_headStorageContainers != null)
            {
                for (int i = 0; i < _headStorageContainers.Length; i++)
                {
                    if (_headStorageContainers[i] != null)
                    {
                        container.Inject(_headStorageContainers[i]);
                    }
                }
            }

            if (_legStorageContainers != null)
            {
                for (int i = 0; i < _legStorageContainers.Length; i++)
                {
                    if (_legStorageContainers[i] != null)
                    {
                        container.Inject(_legStorageContainers[i]);
                    }
                }
            }
        }

        private void ResolveSceneReferences()
        {
            _inputSource         ??= FindFirstObjectByType<PlayerInputSource>(FindObjectsInactive.Include);
            _interactionDetector ??= FindFirstObjectByType<InteractionDetector>(FindObjectsInactive.Include);
            _playerInventory     ??= FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            _carryController     ??= FindFirstObjectByType<CarryController>(FindObjectsInactive.Include);

            _playerInteractor ??= FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            _playerMover      ??= FindFirstObjectByType<PlayerMover>(FindObjectsInactive.Include);
            _playerLook       ??= FindFirstObjectByType<PlayerLook>(FindObjectsInactive.Include);

            if (_headFarmPlots == null || _headFarmPlots.Length == 0)
                _headFarmPlots = FindObjectsByType<HeadFarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (_legPonds == null || _legPonds.Length == 0)
                _legPonds = FindObjectsByType<LegPond>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (_headStorageContainers == null || _headStorageContainers.Length == 0)
                _headStorageContainers = FindObjectsByType<HeadStorageContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (_legStorageContainers == null || _legStorageContainers.Length == 0)
                _legStorageContainers = FindObjectsByType<LegStorageContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (_questActionPublisherTargets == null || _questActionPublisherTargets.Length == 0)
            {
                ResolveQuestActionPublisherTargets();
            }

            _headPool ??= FindFirstObjectByType<HarvestedHeadPool>(FindObjectsInactive.Include);
            _hotbarItemPresenter ??= FindFirstObjectByType<HotbarItemPresenter>(FindObjectsInactive.Include);
        }

        private void ResolveQuestActionPublisherTargets()
        {
            MonoBehaviour[] behaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int targetCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IQuestObjectiveActionPublisherTarget)
                {
                    targetCount++;
                }
            }

            _questActionPublisherTargets = new MonoBehaviour[targetCount];

            int targetIndex = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IQuestObjectiveActionPublisherTarget)
                {
                    _questActionPublisherTargets[targetIndex] = behaviours[i];
                    targetIndex++;
                }
            }
        }

        private void ValidateSceneReferences()
        {
            if (_inputSource == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing PlayerInputSource.");
            if (_interactionDetector == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing InteractionDetector.");
            if (_playerInventory == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing PlayerInventory.");
            if (_carryController == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing CarryController.");
            if (_playerInteractor == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing PlayerInteractor.");
            if (_playerMover == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing PlayerMover.");
            if (_playerLook == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing PlayerLook.");
            if (_headFarmPlots == null || _headFarmPlots.Length == 0)
                throw new InvalidOperationException("[RootLifetimeScope] Missing HeadFarmPlot.");
            if (_headPool == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing HarvestedHeadPool.");
        }
    }
}
