using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic dialogue panel for NPC conversations.
    /// Displays speaker name, dialogue text, and response choices.
    /// </summary>
    public class DialoguePanel : UIViewBase
    {
        [Header("Dialogue Display")]
        [SerializeField] protected TextMeshProUGUI speakerNameText;
        [SerializeField] protected TextMeshProUGUI dialogueText;
        [SerializeField] protected Image speakerPortrait;

        [Header("Response Choices")]
        [SerializeField] protected Transform choicesContainer;
        [SerializeField] protected GameObject choiceButtonPrefab;

        [Header("Continue Button")]
        [SerializeField] protected Button continueButton;
        [SerializeField] protected GameObject continueIndicator;

        // Events
        public event Action<int> OnChoiceSelected; // choice index
        public event Action OnDialogueContinued;
        public event Action OnDialogueEnded;

        protected List<GameObject> choiceButtons = new List<GameObject>();
        protected bool isWaitingForChoice = false;

        protected override void Awake()
        {
            base.Awake();

            if (continueButton != null)
                continueButton.onClick.AddListener(ContinueDialogue);
        }

        /// <summary>
        /// Displays dialogue with speaker name and text.
        /// </summary>
        public virtual void DisplayDialogue(string speakerName, string dialogueText, Sprite portrait = null)
        {
            if (speakerNameText != null)
                speakerNameText.text = speakerName;

            if (this.dialogueText != null)
                this.dialogueText.text = dialogueText;

            if (speakerPortrait != null && portrait != null)
            {
                speakerPortrait.sprite = portrait;
                speakerPortrait.enabled = true;
            }
            else if (speakerPortrait != null)
            {
                speakerPortrait.enabled = false;
            }

            ClearChoices();
            ShowContinueButton();
            Show();
        }

        /// <summary>
        /// Displays dialogue with response choices.
        /// </summary>
        public virtual void DisplayDialogueWithChoices(string speakerName, string dialogueText, string[] choices, Sprite portrait = null)
        {
            DisplayDialogue(speakerName, dialogueText, portrait);

            HideContinueButton();
            CreateChoiceButtons(choices);
            isWaitingForChoice = true;
        }

        protected virtual void CreateChoiceButtons(string[] choices)
        {
            ClearChoices();

            if (choicesContainer == null || choiceButtonPrefab == null) return;

            for (int i = 0; i < choices.Length; i++)
            {
                var choiceButton = Instantiate(choiceButtonPrefab, choicesContainer);
                var buttonText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
                var button = choiceButton.GetComponent<Button>();

                if (buttonText != null)
                    buttonText.text = choices[i];

                int choiceIndex = i; // Capture for closure
                if (button != null)
                    button.onClick.AddListener(() => SelectChoice(choiceIndex));

                choiceButtons.Add(choiceButton);
            }
        }

        protected virtual void SelectChoice(int choiceIndex)
        {
            isWaitingForChoice = false;
            ClearChoices();
            OnChoiceSelected?.Invoke(choiceIndex);
        }

        protected virtual void ContinueDialogue()
        {
            if (isWaitingForChoice) return;

            OnDialogueContinued?.Invoke();
        }

        protected virtual void ClearChoices()
        {
            foreach (var button in choiceButtons)
            {
                if (button != null)
                    Destroy(button);
            }
            choiceButtons.Clear();
        }

        protected virtual void ShowContinueButton()
        {
            if (continueButton != null)
                continueButton.gameObject.SetActive(true);

            if (continueIndicator != null)
                continueIndicator.SetActive(true);
        }

        protected virtual void HideContinueButton()
        {
            if (continueButton != null)
                continueButton.gameObject.SetActive(false);

            if (continueIndicator != null)
                continueIndicator.SetActive(false);
        }

        /// <summary>
        /// Ends dialogue and closes panel.
        /// </summary>
        public virtual void EndDialogue()
        {
            OnDialogueEnded?.Invoke();
            ClearChoices();
            Close();
        }

        public override void Hide()
        {
            base.Hide();
            ClearChoices();
            isWaitingForChoice = false;
        }
    }
}
