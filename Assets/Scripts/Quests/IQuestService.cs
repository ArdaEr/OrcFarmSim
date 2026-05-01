using System.Collections.Generic;

namespace OrcFarm.Quests
{
    /// <summary>
    /// Runtime API for quest registration, activation, progress, and read models.
    /// </summary>
    public interface IQuestService
    {
        /// <summary>Registers a quest definition before it can be started.</summary>
        void RegisterQuestDefinition(QuestDefinition definition);

        /// <summary>Attempts to start a registered quest.</summary>
        bool TryStartQuest(string questId);

        /// <summary>Attempts to apply objective progress to all active quests.</summary>
        bool TryRecordProgress(string progressKey, int amount);

        /// <summary>Attempts to manually complete a quest whose objectives are complete.</summary>
        bool TryCompleteQuest(string questId);

        /// <summary>Returns the current status for a quest id.</summary>
        QuestStatus GetQuestStatus(string questId);

        /// <summary>Attempts to get a read model for a registered quest.</summary>
        bool TryGetQuestSnapshot(string questId, out QuestSnapshot snapshot);

        /// <summary>Returns read models for all active or turn-in-ready quests.</summary>
        IReadOnlyList<QuestSnapshot> GetActiveQuestSnapshots();

        /// <summary>Returns read models for every quest with runtime state.</summary>
        IReadOnlyList<QuestSnapshot> GetAllQuestSnapshots();
    }
}
