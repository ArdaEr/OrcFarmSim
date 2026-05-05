using System;
using MessagePipe;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Subscribes to generic quest progress signals and forwards them to the quest service.
    /// </summary>
    public sealed class QuestProgressProxy : IDisposable
    {
        private readonly IQuestService _questService;
        private readonly ISubscriber<QuestProgressSignal> _progressSubscriber;
        private readonly ISubscriber<QuestObjectiveActionSignal> _actionSubscriber;
        private IDisposable _progressSubscription;
        private IDisposable _actionSubscription;

        /// <summary>
        /// Creates a proxy that bridges MessagePipe progress signals to the quest service.
        /// </summary>
        public QuestProgressProxy(
            IQuestService questService,
            ISubscriber<QuestProgressSignal> progressSubscriber,
            ISubscriber<QuestObjectiveActionSignal> actionSubscriber)
        {
            if (questService == null)
            {
                throw new ArgumentNullException(nameof(questService));
            }

            if (progressSubscriber == null)
            {
                throw new ArgumentNullException(nameof(progressSubscriber));
            }

            if (actionSubscriber == null)
            {
                throw new ArgumentNullException(nameof(actionSubscriber));
            }

            _questService = questService;
            _progressSubscriber = progressSubscriber;
            _actionSubscriber = actionSubscriber;
        }

        /// <summary>Subscribes to quest progress signals.</summary>
        public void Start()
        {
            _progressSubscription = _progressSubscriber.Subscribe(OnQuestProgressed);
            _actionSubscription = _actionSubscriber.Subscribe(OnQuestObjectiveAction);
        }

        /// <summary>Disposes the progress subscription.</summary>
        public void Dispose()
        {
            _progressSubscription?.Dispose();
            _actionSubscription?.Dispose();
        }

        private void OnQuestProgressed(QuestProgressSignal signal)
        {
            bool changed = _questService.TryRecordProgress(signal.ProgressKey, signal.Amount);
            LogProgress(signal.ProgressKey, signal.Amount, changed);
        }

        private void OnQuestObjectiveAction(QuestObjectiveActionSignal signal)
        {
            LogAction(signal.ActionKey, signal.Amount);

            if (signal.ActionKey == QuestObjectiveActionKeys.OrcCraftedWithHeadAndLeg)
            {
                bool changed = _questService.TryRecordProgress(
                    QuestObjectiveActionKeys.OrcCraftedWithHeadAndLeg,
                    signal.Amount);
                LogProgress(QuestObjectiveActionKeys.OrcCraftedWithHeadAndLeg, signal.Amount, changed);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogAction(string actionKey, int amount)
        {
            UnityEngine.Debug.Log(
                $"[QuestProgressProxy] Action received: {actionKey} x{amount}.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogProgress(string progressKey, int amount, bool changed)
        {
            UnityEngine.Debug.Log(
                $"[QuestProgressProxy] Progress {(changed ? "applied" : "ignored")}: " +
                $"{progressKey} x{amount}.");
        }
    }
}
