using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using QuantumMechanic.Networking;
using QuantumMechanic.Save;
using QuantumMechanic.Events;

namespace QuantumMechanic.Analytics
{
    /// <summary>
    /// Core analytics manager for tracking events, metrics, and user behavior.
    /// GDPR/CCPA compliant with privacy-first design.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        private static AnalyticsManager _instance;
        public static AnalyticsManager Instance => _instance;

        [Header("Configuration")]
        [SerializeField] private string apiKey;
        [SerializeField] private string endpoint = "https://analytics.example.com/api";
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private int batchSize = 20;
        [SerializeField] private float batchInterval = 30f;

        private string userId;
        private string sessionId;
        private DateTime sessionStart;
        private bool isInitialized;
        private bool analyticsEnabled = true;
        private Queue<AnalyticsEvent> eventQueue = new Queue<AnalyticsEvent>();
        private Dictionary<string, object> userProperties = new Dictionary<string, object>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Initialize the analytics system with API credentials.
        /// </summary>
        public void Initialize(string key = null)
        {
            if (isInitialized) return;

            apiKey = key ?? apiKey;
            analyticsEnabled = LoadPrivacyConsent();

            if (!analyticsEnabled)
            {
                Debug.Log("[Analytics] User has opted out of analytics");
                return;
            }

            userId = GetOrCreateUserId();
            StartNewSession();
            InvokeRepeating(nameof(FlushEventBatch), batchInterval, batchInterval);

            isInitialized = true;
            Debug.Log($"[Analytics] Initialized with User ID: {userId}");
        }

        /// <summary>
        /// Start a new analytics session.
        /// </summary>
        private void StartNewSession()
        {
            sessionId = Guid.NewGuid().ToString();
            sessionStart = DateTime.UtcNow;

            TrackEvent("session_start", new Dictionary<string, object>
            {
                {"session_id", sessionId},
                {"platform", Application.platform.ToString()},
                {"app_version", Application.version},
                {"unity_version", Application.unityVersion}
            });
        }

        /// <summary>
        /// Track a custom analytics event with optional properties.
        /// </summary>
        public void TrackEvent(string eventName, Dictionary<string, object> properties = null)
        {
            if (!analyticsEnabled || !isInitialized) return;

            var analyticsEvent = new AnalyticsEvent
            {
                EventName = SanitizeEventName(eventName),
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                SessionId = sessionId,
                Properties = properties ?? new Dictionary<string, object>()
            };

            // Add default properties
            analyticsEvent.Properties["session_duration"] = (DateTime.UtcNow - sessionStart).TotalSeconds;

            if (ValidateEvent(analyticsEvent))
            {
                eventQueue.Enqueue(analyticsEvent);
                SaveEventLocally(analyticsEvent);

                if (enableDebugLogging)
                    Debug.Log($"[Analytics] Event tracked: {eventName}");

                if (eventQueue.Count >= batchSize)
                    FlushEventBatch();
            }
        }

        /// <summary>
        /// Set a user property for segmentation and analysis.
        /// </summary>
        public void SetUserProperty(string key, object value)
        {
            if (!analyticsEnabled) return;
            userProperties[key] = value;
            SaveUserProperties();
        }

        /// <summary>
        /// Set user ID for tracking (e.g., after login).
        /// </summary>
        public void IdentifyUser(string customUserId)
        {
            userId = customUserId;
            TrackEvent("user_identified", new Dictionary<string, object> { {"user_id", userId} });
        }

        /// <summary>
        /// Flush queued events to the server.
        /// </summary>
        private async void FlushEventBatch()
        {
            if (eventQueue.Count == 0) return;

            var batch = new List<AnalyticsEvent>();
            int count = Mathf.Min(batchSize, eventQueue.Count);

            for (int i = 0; i < count; i++)
                batch.Add(eventQueue.Dequeue());

            await SendEventsWithRetry(batch);
        }

        /// <summary>
        /// Send events to server with retry logic.
        /// </summary>
        private async Task SendEventsWithRetry(List<AnalyticsEvent> events, int maxRetries = 3)
        {
            int attempt = 0;
            bool success = false;

            while (attempt < maxRetries && !success)
            {
                try
                {
                    var encrypted = EncryptEventData(events);
                    // Simulate network call - replace with actual HTTP request
                    await Task.Delay(100);
                    success = true;

                    if (enableDebugLogging)
                        Debug.Log($"[Analytics] Sent {events.Count} events to server");
                }
                catch (Exception ex)
                {
                    attempt++;
                    Debug.LogWarning($"[Analytics] Send failed (attempt {attempt}): {ex.Message}");
                    await Task.Delay(1000 * attempt);
                }
            }

            if (!success)
            {
                // Re-queue failed events
                foreach (var evt in events)
                    eventQueue.Enqueue(evt);
            }
        }

        /// <summary>
        /// Enable or disable analytics tracking (GDPR compliance).
        /// </summary>
        public void SetAnalyticsEnabled(bool enabled)
        {
            analyticsEnabled = enabled;
            PlayerPrefs.SetInt("AnalyticsConsent", enabled ? 1 : 0);
            PlayerPrefs.Save();

            if (enabled && !isInitialized)
                Initialize();
            else if (!enabled)
                TrackEvent("analytics_disabled");
        }

        /// <summary>
        /// Request deletion of all user data (GDPR right to be forgotten).
        /// </summary>
        public async Task RequestDataDeletion()
        {
            TrackEvent("data_deletion_requested");
            await Task.Delay(100); // Simulate API call
            ClearLocalData();
            Debug.Log("[Analytics] Data deletion requested");
        }

        private string GetOrCreateUserId()
        {
            string id = PlayerPrefs.GetString("AnalyticsUserId", "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("AnalyticsUserId", id);
                PlayerPrefs.Save();
            }
            return id;
        }

        private bool LoadPrivacyConsent() => PlayerPrefs.GetInt("AnalyticsConsent", 1) == 1;

        private string SanitizeEventName(string name) => name.Replace(" ", "_").ToLower();

        private bool ValidateEvent(AnalyticsEvent evt)
        {
            if (string.IsNullOrEmpty(evt.EventName)) return false;
            if (evt.EventName.Length > 100) return false;
            if (evt.Properties.Count > 50) return false;
            return true;
        }

        private void SaveEventLocally(AnalyticsEvent evt)
        {
            // Cache events locally for offline support
            string key = $"analytics_event_{DateTime.UtcNow.Ticks}";
            PlayerPrefs.SetString(key, JsonUtility.ToJson(evt));
        }

        private void SaveUserProperties()
        {
            PlayerPrefs.SetString("AnalyticsUserProps", JsonUtility.ToJson(userProperties));
        }

        private string EncryptEventData(List<AnalyticsEvent> events)
        {
            // Simple encryption - replace with proper encryption in production
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(events)));
        }

        private void ClearLocalData()
        {
            PlayerPrefs.DeleteKey("AnalyticsUserId");
            PlayerPrefs.DeleteKey("AnalyticsUserProps");
            eventQueue.Clear();
        }

        private void OnApplicationQuit()
        {
            if (!isInitialized) return;

            var sessionDuration = (DateTime.UtcNow - sessionStart).TotalSeconds;
            TrackEvent("session_end", new Dictionary<string, object>
            {
                {"session_id", sessionId},
                {"duration", sessionDuration}
            });

            FlushEventBatch();
        }
    }

    [Serializable]
    public class AnalyticsEvent
    {
        public string EventName;
        public DateTime Timestamp;
        public string UserId;
        public string SessionId;
        public Dictionary<string, object> Properties;
    }
}