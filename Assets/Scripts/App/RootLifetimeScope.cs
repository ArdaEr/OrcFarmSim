using System;
using MessagePipe;
using OrcFarm.Carry;
using OrcFarm.Farming;
using OrcFarm.Interaction;
using OrcFarm.Inventory;
using OrcFarm.Player;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;

namespace OrcFarm.App
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [Header("Scene service instances")]
        [SerializeField] private PlayerInputSource _inputSource;
        [SerializeField] private InteractionDetector _interactionDetector;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private CarryController _carryController;

        [Header("Injected scene consumers")]
        [SerializeField] private PlayerInteractor _playerInteractor;
        [SerializeField] private PlayerMover _playerMover;
        [SerializeField] private PlayerLook _playerLook;
        [SerializeField] private FarmPlot[] _farmPlots;

        [Header("Injected scene consumers (continued)")]
        [SerializeField] private OrcFarm.UI.InteractHUD _interactHud;
        [SerializeField] private OrcFarm.Storage.HeadStorageContainer[] _storageContainers;

        [Header("HarvestedHead pool")]
        [SerializeField] private AssetReferenceT<GameObject> _harvestedHeadRef;
        [SerializeField] private Transform _headPoolRoot;
        [SerializeField] private int _headPoolSize = 8;

        protected override void Configure(IContainerBuilder builder)
        {
            ResolveSceneReferences();
            ValidateSceneReferences();

            var pipeOptions = builder.RegisterMessagePipe();
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));
            builder.RegisterMessageBroker<CropHarvestedSignal>(pipeOptions);

            builder.RegisterComponent<IPlayerInputProvider>(_inputSource);
            builder.RegisterComponent<IInteractionService>(_interactionDetector);
            builder.RegisterComponent<IPlayerInventory>(_playerInventory);
            builder.RegisterComponent<ICarryController>(_carryController);

            builder.RegisterComponent(_playerInteractor);
            builder.RegisterComponent(_playerMover);
            builder.RegisterComponent(_playerLook);
            builder.RegisterComponent(_interactHud);

            for (int i = 0; i < _farmPlots.Length; i++)
            {
                if (_farmPlots[i] != null)
                    builder.RegisterComponent(_farmPlots[i]);
            }

            builder.Register<HarvestedHeadPool>(Lifetime.Singleton)
                   .WithParameter(_harvestedHeadRef)
                   .WithParameter(_headPoolRoot)
                   .WithParameter(_headPoolSize)
                   .AsImplementedInterfaces()
                   .AsSelf();

            builder.RegisterEntryPoint<HarvestCoordinator>(Lifetime.Singleton);

            if (_storageContainers != null)
            {
                for (int i = 0; i < _storageContainers.Length; i++)
                {
                    if (_storageContainers[i] != null)
                        builder.RegisterComponent(_storageContainers[i]);
                }
            }
        }
        private void ResolveSceneReferences()
        {
            _inputSource ??= FindFirstObjectByType<PlayerInputSource>(FindObjectsInactive.Include);
            _interactionDetector ??= FindFirstObjectByType<InteractionDetector>(FindObjectsInactive.Include);
            _playerInventory ??= FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            _carryController ??= FindFirstObjectByType<CarryController>(FindObjectsInactive.Include);

            _playerInteractor ??= FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            _playerMover ??= FindFirstObjectByType<PlayerMover>(FindObjectsInactive.Include);
            _playerLook ??= FindFirstObjectByType<PlayerLook>(FindObjectsInactive.Include);

            if (_farmPlots == null || _farmPlots.Length == 0)
                _farmPlots = FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
            if (_farmPlots == null || _farmPlots.Length == 0)
                throw new InvalidOperationException("[RootLifetimeScope] Missing FarmPlot.");
            if (_harvestedHeadRef == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing HarvestedHead AssetReference.");
            if (_headPoolRoot == null)
                throw new InvalidOperationException("[RootLifetimeScope] Missing head pool root Transform.");
        }
    }
}