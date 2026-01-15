using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuantumMechanic.UI.Notifications
{
    /// <summary>
    /// Types of notifications that can be displayed in the game
    /// </summary>
    public enum NotificationType
    {
        Info,           // General information
        Success,        // Positive feedback (quest complete, achievement)
        Warning,        // Caution messages
        Error,          // Error or failure messages
        Achievement,    // Achievement unlocked
        Quest,          // Quest updates
        Combat,         // Combat notifications
        Loot,           // Item/resource obtained
        Level,          // Level up or progression
        System          // System messages
    }

    /// <summary>
    /// Priority levels for notification display
    /// </summary>
    public enum NotificationPriority
    {
        Low = 0,        // Can be grouped/delayed
        Normal = 1,     // Standard priority
        High = 2,       // Shows immediately
        Critical = 3    // Cannot be dismissed automatically, requires user action
    }

    /// <summary>
    /// Visual style of the notification
    /// </summary>
    public enum NotificationStyle
    {
        Toast,          // Small, bottom corner notification
        Banner,         // Top banner notification
        Popup,          // Center popup with more details
        Modal           // Full screen blocking modal
    }

    /// <summary>
    /// Animation types for notification entrance/exit
    /// </summary>
    public enum NotificationAnimation
    {
        Slide,
        Fade,
        Bounce,
        Shake,
        Scale
    }

    /// <summary>
    /// Represents a single notification
    /// </summary>
    [Serializable]
    public class Notification
    {
        public string id;
        public string title;
        public string message;
        public NotificationType type;
        public NotificationPriority priority;
        public NotificationStyle style;
        public NotificationAnimation animation;
        public float duration;
        public Sprite icon;
        public Color? customColor;
        public bool dismissible;
        public bool playSound;
        public bool useHaptic;
        public List<NotificationButton> buttons;
        public Action onShown;
        public Action onDismissed;
        public DateTime timestamp;
        public string groupKey; // For grouping similar notifications

        public Notification()
        {
            id = Guid.NewGuid().ToString();
            duration = 3f;
            dismissible = true;
            playSound = true;
            useHaptic = false;
            buttons = new List<NotificationButton>();
            timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Interactive button for notifications
    /// </summary>
    [Serializable]
    public class NotificationButton
    {
        public string text;
        public Action onClick;
        public Color buttonColor;
        public bool dismissOnClick;

        public NotificationButton(string text, Action onClick, bool dismissOnClick = true)
        {
            this.text = text;
            this.onClick = onClick;
            this.dismissOnClick = dismissOnClick;
            this.buttonColor = Color.white;
        }
    }

    /// <summary>
    /// User preferences for notifications
    /// </summary>
    [Serializable]
    public class NotificationSettings
    {
        public bool notificationsEnabled = true;
        public bool soundEnabled = true;
        public bool hapticEnabled = true;
        public float globalDuration = 3f;
        public Dictionary<NotificationType, bool> typeFilters = new Dictionary<NotificationType, bool>();
        public bool groupSimilar = true;
        public int maxSimultaneous = 3;
    }

    /// <summary>
    /// Central notification system for managing in-game notifications
    /// </summary>
    public class NotificationSystem : MonoBehaviour
    {
        private static NotificationSystem _instance;
        public static NotificationSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("NotificationSystem");
                    _instance = go.AddComponent<NotificationSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private NotificationSettings settings = new NotificationSettings();
        [SerializeField] private float notificationCooldown = 0.1f;
        
        // Queue management
        private Queue<Notification> notificationQueue = new Queue<Notification>();
        private List<Notification> activeNotifications = new List<Notification>();
        private List<Notification> notificationHistory = new List<Notification>();
        private Dictionary<string, List<Notification>> groupedNotifications = new Dictionary<string, List<Notification>>();
        
        // Timing
        private float lastNotificationTime;
        private Dictionary<string, float> notificationTimers = new Dictionary<string, float>();
        
        // Events
        public UnityEvent<Notification> OnNotificationShown = new UnityEvent<Notification>();
        public UnityEvent<Notification> OnNotificationDismissed = new UnityEvent<Notification>();
        public UnityEvent<int> OnQueueSizeChanged = new UnityEvent<int>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSettings();
        }

        /// <summary>
        /// Initialize notification settings with defaults
        /// </summary>
        private void InitializeSettings()
        {
            foreach (NotificationType type in Enum.GetValues(typeof(NotificationType)))
            {
                if (!settings.typeFilters.ContainsKey(type))
                {
                    settings.typeFilters[type] = true;
                }
            }
        }
/// <summary>
        /// Show a notification with the specified parameters
        /// </summary>
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info,
            NotificationPriority priority = NotificationPriority.Normal, float duration = 0f)
        {
            Notification notification = new Notification
            {
                title = title,
                message = message,
                type = type,
                priority = priority,
                duration = duration > 0 ? duration : settings.globalDuration,
                style = GetDefaultStyleForType(type),
                animation = NotificationAnimation.Slide
            };
            
            EnqueueNotification(notification);
        }

        /// <summary>
        /// Show a custom notification
        /// </summary>
        public void ShowNotification(Notification notification)
        {
            if (!settings.notificationsEnabled || !settings.typeFilters[notification.type])
                return;

            EnqueueNotification(notification);
        }

        /// <summary>
        /// Add notification to queue with priority and grouping logic
        /// </summary>
        private void EnqueueNotification(Notification notification)
        {
            // Check if we should group this notification
            if (settings.groupSimilar && !string.IsNullOrEmpty(notification.groupKey))
            {
                if (TryGroupNotification(notification))
                    return;
            }

            // Priority-based insertion
            if (notification.priority == NotificationPriority.Critical)
            {
                // Critical notifications go to front
                var tempQueue = new Queue<Notification>();
                tempQueue.Enqueue(notification);
                while (notificationQueue.Count > 0)
                {
                    tempQueue.Enqueue(notificationQueue.Dequeue());
                }
                notificationQueue = tempQueue;
            }
            else
            {
                notificationQueue.Enqueue(notification);
            }

            OnQueueSizeChanged?.Invoke(notificationQueue.Count);
        }

        /// <summary>
        /// Process notification queue and display notifications
        /// </summary>
        private void Update()
        {
            // Process queue with cooldown
            if (notificationQueue.Count > 0 && 
                Time.time - lastNotificationTime >= notificationCooldown &&
                activeNotifications.Count < settings.maxSimultaneous)
            {
                Notification notification = notificationQueue.Dequeue();
                DisplayNotification(notification);
                lastNotificationTime = Time.time;
                OnQueueSizeChanged?.Invoke(notificationQueue.Count);
            }

            // Update active notification timers
            UpdateNotificationTimers();
        }

        /// <summary>
        /// Display a notification on screen
        /// </summary>
        private void DisplayNotification(Notification notification)
        {
            activeNotifications.Add(notification);
            notificationHistory.Add(notification);

            // Play sound if enabled
            if (notification.playSound && settings.soundEnabled)
            {
                PlayNotificationSound(notification.type);
            }

            // Trigger haptic feedback if enabled
            if (notification.useHaptic && settings.hapticEnabled)
            {
                TriggerHaptic(notification.priority);
            }

            // Animate notification entrance
            AnimateNotification(notification, true);

            // Set up auto-dismiss timer
            if (notification.dismissible && notification.priority != NotificationPriority.Critical)
            {
                notificationTimers[notification.id] = notification.duration;
            }

            // Invoke callbacks
            notification.onShown?.Invoke();
            OnNotificationShown?.Invoke(notification);

            Debug.Log($"[Notification] {notification.type}: {notification.title} - {notification.message}");
        }

        /// <summary>
        /// Update timers for active notifications and auto-dismiss
        /// </summary>
        private void UpdateNotificationTimers()
        {
            List<string> toRemove = new List<string>();

            foreach (var kvp in notificationTimers)
            {
                notificationTimers[kvp.Key] -= Time.deltaTime;
                
                if (notificationTimers[kvp.Key] <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string id in toRemove)
            {
                Notification notification = activeNotifications.Find(n => n.id == id);
                if (notification != null)
                {
                    DismissNotification(notification);
                }
                notificationTimers.Remove(id);
            }
        }

        /// <summary>
        /// Dismiss a notification
        /// </summary>
        public void DismissNotification(Notification notification)
        {
            if (!activeNotifications.Contains(notification))
                return;

            // Animate exit
            AnimateNotification(notification, false);

            activeNotifications.Remove(notification);
            notificationTimers.Remove(notification.id);

            // Invoke callbacks
            notification.onDismissed?.Invoke();
            OnNotificationDismissed?.Invoke(notification);
        }

        /// <summary>
        /// Animate notification entrance or exit
        /// </summary>
        private void AnimateNotification(Notification notification, bool isEntering)
        {
            // This would integrate with your UI system
            // For now, we'll log the animation type
            string direction = isEntering ? "IN" : "OUT";
            Debug.Log($"[Animation] {notification.animation} {direction} for notification: {notification.id}");
            
            // Example: Different animations based on type
            switch (notification.animation)
            {
                case NotificationAnimation.Slide:
                    // Slide from side
                    break;
                case NotificationAnimation.Fade:
                    // Fade in/out
                    break;
                case NotificationAnimation.Bounce:
                    // Bounce effect
                    break;
                case NotificationAnimation.Shake:
                    // Shake for warnings/errors
                    break;
                case NotificationAnimation.Scale:
                    // Scale up/down
                    break;
            }
        }

        /// <summary>
        /// Play sound effect for notification type
        /// </summary>
        private void PlayNotificationSound(NotificationType type)
        {
            // Integrate with AudioManager
            string soundName = $"Notification_{type}";
            Debug.Log($"[Audio] Playing sound: {soundName}");
            // AudioManager.Instance?.PlaySound(soundName);
        }

        /// <summary>
        /// Trigger haptic feedback based on priority
        /// </summary>
        private void TriggerHaptic(NotificationPriority priority)
        {
#if UNITY_IOS || UNITY_ANDROID
            switch (priority)
            {
                case NotificationPriority.Low:
                    Handheld.Vibrate(); // Light vibration
                    break;
                case NotificationPriority.Critical:
                    Handheld.Vibrate(); // Strong vibration
                    break;
                default:
                    Handheld.Vibrate(); // Medium vibration
                    break;
            }
#endif
        }
/// <summary>
        /// Try to group a notification with existing similar notifications
        /// </summary>
        private bool TryGroupNotification(Notification notification)
        {
            if (!groupedNotifications.ContainsKey(notification.groupKey))
            {
                groupedNotifications[notification.groupKey] = new List<Notification>();
            }

            var group = groupedNotifications[notification.groupKey];
            
            // If there's already a notification of this type in queue/active, group it
            if (group.Count > 0)
            {
                group.Add(notification);
                
                // Update the visible notification to show count
                var activeGroupNotification = activeNotifications.Find(n => n.groupKey == notification.groupKey);
                if (activeGroupNotification != null)
                {
                    activeGroupNotification.message = $"{notification.message} (x{group.Count + 1})";
                }
                
                return true;
            }

            group.Add(notification);
            return false;
        }

        /// <summary>
        /// Get default style based on notification type
        /// </summary>
        private NotificationStyle GetDefaultStyleForType(NotificationType type)
        {
            return type switch
            {
                NotificationType.Achievement => NotificationStyle.Banner,
                NotificationType.Level => NotificationStyle.Banner,
                NotificationType.Error => NotificationStyle.Popup,
                NotificationType.System => NotificationStyle.Modal,
                _ => NotificationStyle.Toast
            };
        }

        /// <summary>
        /// Show achievement unlock notification
        /// </summary>
        public void ShowAchievement(string achievementName, string description, Sprite icon = null)
        {
            Notification notification = new Notification
            {
                title = "Achievement Unlocked!",
                message = $"{achievementName}\n{description}",
                type = NotificationType.Achievement,
                priority = NotificationPriority.High,
                style = NotificationStyle.Banner,
                animation = NotificationAnimation.Bounce,
                icon = icon,
                duration = 5f,
                customColor = new Color(1f, 0.84f, 0f) // Gold
            };

            ShowNotification(notification);
        }

        /// <summary>
        /// Show quest update notification
        /// </summary>
        public void ShowQuestUpdate(string questName, string update, bool completed = false)
        {
            Notification notification = new Notification
            {
                title = completed ? "Quest Completed!" : "Quest Updated",
                message = $"{questName}\n{update}",
                type = NotificationType.Quest,
                priority = completed ? NotificationPriority.High : NotificationPriority.Normal,
                style = NotificationStyle.Toast,
                animation = NotificationAnimation.Slide,
                groupKey = $"quest_{questName}",
                customColor = completed ? Color.green : new Color(0.5f, 0.8f, 1f)
            };

            ShowNotification(notification);
        }

        /// <summary>
        /// Show combat notification
        /// </summary>
        public void ShowCombatNotification(string message, bool isDamage = true)
        {
            Notification notification = new Notification
            {
                title = isDamage ? "Damage" : "Heal",
                message = message,
                type = NotificationType.Combat,
                priority = NotificationPriority.Low,
                style = NotificationStyle.Toast,
                animation = NotificationAnimation.Shake,
                duration = 1.5f,
                playSound = false, // Combat has its own sounds
                customColor = isDamage ? Color.red : Color.green,
                groupKey = "combat"
            };

            ShowNotification(notification);
        }

        /// <summary>
        /// Show loot notification
        /// </summary>
        public void ShowLoot(string itemName, int quantity = 1, Sprite icon = null)
        {
            Notification notification = new Notification
            {
                title = "Item Obtained",
                message = quantity > 1 ? $"{itemName} x{quantity}" : itemName,
                type = NotificationType.Loot,
                priority = NotificationPriority.Normal,
                style = NotificationStyle.Toast,
                animation = NotificationAnimation.Scale,
                icon = icon,
                duration = 2.5f,
                groupKey = $"loot_{itemName}"
            };

            ShowNotification(notification);
        }

        /// <summary>
        /// Clear all active notifications
        /// </summary>
        public void ClearAllNotifications()
        {
            foreach (var notification in activeNotifications.ToArray())
            {
                DismissNotification(notification);
            }
            notificationQueue.Clear();
            OnQueueSizeChanged?.Invoke(0);
        }

        /// <summary>
        /// Clear notifications of a specific type
        /// </summary>
        public void ClearNotificationsByType(NotificationType type)
        {
            foreach (var notification in activeNotifications.FindAll(n => n.type == type).ToArray())
            {
                DismissNotification(notification);
            }
            
            // Remove from queue as well
            var tempQueue = new Queue<Notification>();
            while (notificationQueue.Count > 0)
            {
                var n = notificationQueue.Dequeue();
                if (n.type != type)
                    tempQueue.Enqueue(n);
            }
            notificationQueue = tempQueue;
        }

        /// <summary>
        /// Get notification history
        /// </summary>
        public List<Notification> GetHistory(int count = 10)
        {
            int startIndex = Mathf.Max(0, notificationHistory.Count - count);
            return notificationHistory.GetRange(startIndex, notificationHistory.Count - startIndex);
        }

        /// <summary>
        /// Update notification settings
        /// </summary>
        public void UpdateSettings(NotificationSettings newSettings)
        {
            settings = newSettings;
        }

        /// <summary>
        /// Get current settings
        /// </summary>
        public NotificationSettings GetSettings()
        {
            return settings;
        }

        /// <summary>
        /// Toggle notifications for a specific type
        /// </summary>
        public void ToggleNotificationType(NotificationType type, bool enabled)
        {
            settings.typeFilters[type] = enabled;
        }

        /// <summary>
        /// Get queue size
        /// </summary>
        public int GetQueueSize()
        {
            return notificationQueue.Count;
        }

        /// <summary>
        /// Get active notification count
        /// </summary>
        public int GetActiveCount()
        {
            return activeNotifications.Count;
        }
    }
}