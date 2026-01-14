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
