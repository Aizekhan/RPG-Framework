using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

namespace RPGFramework.UI.Notifications
{
    /// <summary>
    /// Generic notification panel for displaying temporary messages.
    /// Handles level up, achievements, quest completion, item pickup notifications.
    /// </summary>
    public class NotificationPanel : MonoBehaviour
    {
        [Header("Notification Settings")]
        [SerializeField] protected Transform notificationContainer;
        [SerializeField] protected GameObject notificationPrefab;
        [SerializeField] protected float defaultDisplayDuration = 3f;
        [SerializeField] protected float fadeInDuration = 0.3f;
        [SerializeField] protected float fadeOutDuration = 0.3f;

        [Header("Queue Settings")]
        [SerializeField] protected int maxSimultaneousNotifications = 3;
        [SerializeField] protected float notificationSpacing = 10f;

        protected Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
        protected List<NotificationInstance> activeNotifications = new List<NotificationInstance>();

        protected virtual void Update()
        {
            ProcessQueue();
        }

        /// <summary>
        /// Shows level up notification.
        /// </summary>
        public virtual void ShowLevelUp(int newLevel)
        {
            var data = new NotificationData
            {
                title = "LEVEL UP!",
                message = $"You reached level {newLevel}",
                type = NotificationType.LevelUp,
                duration = defaultDisplayDuration,
                color = new Color(1f, 0.84f, 0f) // Gold
            };

            QueueNotification(data);
        }

        /// <summary>
        /// Shows achievement unlocked notification.
        /// </summary>
        public virtual void ShowAchievement(string achievementName, string description, Sprite icon = null)
        {
            var data = new NotificationData
            {
                title = "Achievement Unlocked!",
                message = $"{achievementName}\n{description}",
                type = NotificationType.Achievement,
                icon = icon,
                duration = defaultDisplayDuration + 1f,
                color = new Color(0.5f, 0f, 0.8f) // Purple
            };

            QueueNotification(data);
        }

        /// <summary>
        /// Shows quest completed notification.
        /// </summary>
        public virtual void ShowQuestCompleted(string questName)
        {
            var data = new NotificationData
            {
                title = "Quest Completed!",
                message = questName,
                type = NotificationType.QuestComplete,
                duration = defaultDisplayDuration,
                color = new Color(0f, 0.8f, 0f) // Green
            };

            QueueNotification(data);
        }

        /// <summary>
        /// Shows item picked up notification.
        /// </summary>
        public virtual void ShowItemPickup(string itemName, int quantity, Sprite icon = null)
        {
            string message = quantity > 1 ? $"{itemName} x{quantity}" : itemName;

            var data = new NotificationData
            {
                title = "Item Acquired",
                message = message,
                type = NotificationType.ItemPickup,
                icon = icon,
                duration = 2f,
                color = Color.white
            };

            QueueNotification(data);
        }

        /// <summary>
        /// Shows custom notification.
        /// </summary>
        public virtual void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, float duration = 0f, Color? color = null)
        {
            var data = new NotificationData
            {
                title = title,
                message = message,
                type = type,
                duration = duration > 0 ? duration : defaultDisplayDuration,
                color = color ?? Color.white
            };

            QueueNotification(data);
        }

        protected virtual void QueueNotification(NotificationData data)
        {
            notificationQueue.Enqueue(data);
        }

        protected virtual void ProcessQueue()
        {
            if (notificationQueue.Count == 0) return;
            if (activeNotifications.Count >= maxSimultaneousNotifications) return;

            var data = notificationQueue.Dequeue();
            ShowNotificationInternal(data);
        }

        protected virtual void ShowNotificationInternal(NotificationData data)
        {
            if (notificationContainer == null || notificationPrefab == null) return;

            var notificationObj = Instantiate(notificationPrefab, notificationContainer);
            var instance = new NotificationInstance
            {
                gameObject = notificationObj,
                data = data
            };

            SetupNotification(notificationObj, data);
            activeNotifications.Add(instance);

            StartCoroutine(NotificationLifecycle(instance));
        }

        protected virtual void SetupNotification(GameObject notificationObj, NotificationData data)
        {
            var titleText = notificationObj.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            var messageText = notificationObj.transform.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
            var iconImage = notificationObj.transform.Find("Icon")?.GetComponent<Image>();
            var backgroundImage = notificationObj.GetComponent<Image>();

            if (titleText != null)
                titleText.text = data.title;

            if (messageText != null)
                messageText.text = data.message;

            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(data.color.r, data.color.g, data.color.b, 0.8f);
            }

            // Set initial alpha to 0 for fade in
            var canvasGroup = notificationObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = notificationObj.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
        }

        protected virtual IEnumerator NotificationLifecycle(NotificationInstance instance)
        {
            var canvasGroup = instance.gameObject.GetComponent<CanvasGroup>();

            // Fade in
            yield return FadeCanvasGroup(canvasGroup, 0f, 1f, fadeInDuration);

            // Display
            yield return new WaitForSeconds(instance.data.duration);

            // Fade out
            yield return FadeCanvasGroup(canvasGroup, 1f, 0f, fadeOutDuration);

            // Cleanup
            activeNotifications.Remove(instance);
            if (instance.gameObject != null)
                Destroy(instance.gameObject);
        }

        protected virtual IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        protected class NotificationInstance
        {
            public GameObject gameObject;
            public NotificationData data;
        }
    }

    [Serializable]
    public class NotificationData
    {
        public string title;
        public string message;
        public NotificationType type;
        public Sprite icon;
        public float duration;
        public Color color;
    }

    public enum NotificationType
    {
        Info,
        LevelUp,
        Achievement,
        QuestComplete,
        ItemPickup,
        Warning,
        Error
    }
}
