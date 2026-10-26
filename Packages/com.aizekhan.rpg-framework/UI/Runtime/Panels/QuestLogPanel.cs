using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using RPGFramework.Quests;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic quest log panel for displaying active and completed quests.
    /// Keyboard shortcut: Q
    /// </summary>
    public class QuestLogPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Quest List")]
        [SerializeField] protected Transform questListContainer;
        [SerializeField] protected GameObject questEntryPrefab;
        [SerializeField] protected Toggle showActiveToggle;
        [SerializeField] protected Toggle showCompletedToggle;

        [Header("Quest Details")]
        [SerializeField] protected TextMeshProUGUI questTitleText;
        [SerializeField] protected TextMeshProUGUI questDescriptionText;
        [SerializeField] protected Transform objectivesContainer;
        [SerializeField] protected GameObject objectiveEntryPrefab;
        [SerializeField] protected Transform rewardsContainer;
        [SerializeField] protected GameObject rewardEntryPrefab;

        [Header("Quest Actions")]
        [SerializeField] protected Button trackButton;
        [SerializeField] protected Button abandonButton;

        // Events
        public event Action<IQuest> OnQuestSelected;
        public event Action<IQuest> OnQuestTracked;
        public event Action<IQuest> OnQuestAbandoned;

        protected IQuestManager questManager;
        protected IQuest selectedQuest;
        protected List<GameObject> questEntries = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (trackButton != null)
                trackButton.onClick.AddListener(() => OnQuestTracked?.Invoke(selectedQuest));

            if (abandonButton != null)
                abandonButton.onClick.AddListener(() => OnQuestAbandoned?.Invoke(selectedQuest));

            if (showActiveToggle != null)
                showActiveToggle.onValueChanged.AddListener(_ => RefreshQuestList());

            if (showCompletedToggle != null)
                showCompletedToggle.onValueChanged.AddListener(_ => RefreshQuestList());
        }

        /// <summary>
        /// Initializes panel with quest manager.
        /// </summary>
        public virtual void Initialize(IQuestManager questManager)
        {
            this.questManager = questManager;
            RefreshQuestList();
        }

        /// <summary>
        /// Refreshes quest list display.
        /// </summary>
        public virtual void RefreshQuestList()
        {
            if (questManager == null) return;

            ClearQuestList();

            // Show active quests
            if (showActiveToggle == null || showActiveToggle.isOn)
            {
                // Display active quests (override in subclass)
            }

            // Show completed quests
            if (showCompletedToggle != null && showCompletedToggle.isOn)
            {
                // Display completed quests (override in subclass)
            }
        }

        /// <summary>
        /// Displays details for selected quest.
        /// </summary>
        public virtual void DisplayQuestDetails(IQuest quest)
        {
            if (quest == null) return;

            selectedQuest = quest;

            if (questTitleText != null)
                questTitleText.text = quest.Title;

            if (questDescriptionText != null)
                questDescriptionText.text = quest.Description;

            DisplayObjectives(quest);
            DisplayRewards(quest);

            OnQuestSelected?.Invoke(quest);
        }

        protected virtual void DisplayObjectives(IQuest quest)
        {
            if (objectivesContainer == null || objectiveEntryPrefab == null) return;

            // Clear existing
            foreach (Transform child in objectivesContainer)
            {
                Destroy(child.gameObject);
            }

            // Create objective entries
            if (quest.Objectives != null)
            {
                foreach (var objective in quest.Objectives)
                {
                    var entry = Instantiate(objectiveEntryPrefab, objectivesContainer);
                    var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        // Get progress (override to get actual progress)
                        int current = 0; // GetObjectiveProgress(objective);
                        int required = objective.RequiredCount;
                        bool completed = current >= required;

                        text.text = $"{(completed ? "✓" : "○")} {objective.Description} ({current}/{required})";
                        text.color = completed ? Color.green : Color.white;
                    }
                }
            }
        }

        protected virtual void DisplayRewards(IQuest quest)
        {
            if (rewardsContainer == null || rewardEntryPrefab == null) return;

            // Clear existing
            foreach (Transform child in rewardsContainer)
            {
                Destroy(child.gameObject);
            }

            // Create reward entries
            if (quest.Rewards != null)
            {
                foreach (var reward in quest.Rewards)
                {
                    var entry = Instantiate(rewardEntryPrefab, rewardsContainer);
                    var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        string rewardText = reward.Type switch
                        {
                            RewardType.Experience => $"+{reward.Amount} XP",
                            RewardType.Currency => $"+{reward.Amount} Gold",
                            RewardType.Item => $"{reward.RewardId} x{reward.Amount}",
                            _ => reward.RewardId
                        };
                        text.text = rewardText;
                    }
                }
            }
        }

        protected virtual void ClearQuestList()
        {
            foreach (var entry in questEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            questEntries.Clear();
        }
    }
}
