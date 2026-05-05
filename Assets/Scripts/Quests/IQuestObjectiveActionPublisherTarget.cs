using MessagePipe;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Implemented by scene components that publish quest objective action signals.
    /// </summary>
    public interface IQuestObjectiveActionPublisherTarget
    {
        /// <summary>Sets the publisher used to dispatch quest objective actions.</summary>
        void SetQuestActionPublisher(IPublisher<QuestObjectiveActionSignal> questActionPublisher);
    }
}
