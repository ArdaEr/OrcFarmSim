using System;
using System.Reflection;
using NUnit.Framework;
using OrcFarm.Quests;
using UnityEngine;

namespace OrcFarm.Tests
{
    /// <summary>
    /// Unit tests for the in-memory quest runtime.
    /// </summary>
    [TestFixture]
    internal sealed class QuestServiceTests
    {
        [Test]
        public void TryRecordProgress_CompletesAutoQuest()
        {
            QuestDefinition quest = CreateCounterQuest(
                "contract.sell_one",
                "sell_one",
                "orc.sold",
                1,
                QuestCompletionMode.AutoComplete,
                false);

            QuestService service = new QuestService();
            service.RegisterQuestDefinition(quest);

            Assert.IsTrue(service.TryStartQuest("contract.sell_one"));
            Assert.IsTrue(service.TryRecordProgress("orc.sold", 1));

            Assert.AreEqual(QuestStatus.Completed, service.GetQuestStatus("contract.sell_one"));
        }

        [Test]
        public void TryRecordProgress_ManualTurnInQuestBecomesReadyToComplete()
        {
            QuestDefinition quest = CreateCounterQuest(
                "lord.seed_delivery",
                "deliver_seed",
                "seed.delivered",
                2,
                QuestCompletionMode.ManualTurnIn,
                false);

            QuestService service = new QuestService();
            service.RegisterQuestDefinition(quest);

            Assert.IsTrue(service.TryStartQuest("lord.seed_delivery"));
            Assert.IsTrue(service.TryRecordProgress("seed.delivered", 2));

            Assert.AreEqual(
                QuestStatus.ReadyToComplete,
                service.GetQuestStatus("lord.seed_delivery"));
            Assert.IsTrue(service.TryCompleteQuest("lord.seed_delivery"));
            Assert.AreEqual(QuestStatus.Completed, service.GetQuestStatus("lord.seed_delivery"));
        }

        [Test]
        public void TryRecordProgress_IgnoresDifferentProgressKey()
        {
            QuestDefinition quest = CreateCounterQuest(
                "contract.sell_three",
                "sell_three",
                "orc.sold",
                3,
                QuestCompletionMode.AutoComplete,
                false);

            QuestService service = new QuestService();
            service.RegisterQuestDefinition(quest);

            Assert.IsTrue(service.TryStartQuest("contract.sell_three"));
            Assert.IsFalse(service.TryRecordProgress("head.harvested", 1));
            Assert.IsTrue(service.TryGetQuestSnapshot("contract.sell_three", out QuestSnapshot snapshot));

            Assert.AreEqual(QuestStatus.Active, snapshot.Status);
            Assert.AreEqual(0, snapshot.Objectives[0].CurrentCount);
        }

        [Test]
        public void RegisterQuestDefinition_DuplicateQuestIdThrows()
        {
            QuestDefinition first = CreateCounterQuest(
                "contract.duplicate",
                "first",
                "orc.sold",
                1,
                QuestCompletionMode.AutoComplete,
                false);
            QuestDefinition second = CreateCounterQuest(
                "contract.duplicate",
                "second",
                "head.harvested",
                1,
                QuestCompletionMode.AutoComplete,
                false);

            QuestService service = new QuestService();
            service.RegisterQuestDefinition(first);

            Assert.Throws<InvalidOperationException>(() => service.RegisterQuestDefinition(second));
        }

        private static QuestDefinition CreateCounterQuest(
            string questId,
            string objectiveId,
            string progressKey,
            int targetCount,
            QuestCompletionMode completionMode,
            bool canRepeat)
        {
            CounterQuestObjectiveDefinition objective =
                ScriptableObject.CreateInstance<CounterQuestObjectiveDefinition>();
            SetField(objective, "_objectiveId", objectiveId);
            SetField(objective, "_displayName", objectiveId);
            SetField(objective, "_targetCount", targetCount);
            SetField(objective, "_progressKey", progressKey);

            QuestDefinition quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_questId", questId);
            SetField(quest, "_displayName", questId);
            SetField(quest, "_description", string.Empty);
            SetField(quest, "_category", QuestCategory.Contract);
            SetField(quest, "_completionMode", completionMode);
            SetField(quest, "_canRepeat", canRepeat);
            SetField(quest, "_objectives", new QuestObjectiveDefinition[] { objective });

            return quest;
        }

        private static void SetField<TValue>(object target, string fieldName, TValue value)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new InvalidOperationException(
                $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        }
    }
}
