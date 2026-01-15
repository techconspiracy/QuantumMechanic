using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace QuantumMechanic.Analytics
{
    /// <summary>
    /// Analytics reporting and query system.
    /// </summary>
    public class AnalyticsReporting
    {
        private static AnalyticsReporting _instance;
        public static AnalyticsReporting Instance => _instance ?? (_instance = new AnalyticsReporting());

        private List<AnalyticsEvent> cachedEvents = new List<AnalyticsEvent>();

        /// <summary>
        /// Query events by name and date range.
        /// </summary>
        public List<AnalyticsEvent> QueryEvents(string eventName, DateTime startDate, DateTime endDate)
        {
            return cachedEvents
                .Where(e => e.EventName == eventName && e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();
        }

        /// <summary>
        /// Get event count for a specific event type.
        /// </summary>
        public int GetEventCount(string eventName, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = cachedEvents.Where(e => e.EventName == eventName);
            
            if (startDate.HasValue)
                query = query.Where(e => e.Timestamp >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(e => e.Timestamp <= endDate.Value);
            
            return query.Count();
        }

        /// <summary>
        /// Calculate Daily Active Users (DAU).
        /// </summary>
        public int GetDAU(DateTime date)
        {
            return cachedEvents
                .Where(e => e.Timestamp.Date == date.Date)
                .Select(e => e.UserId)
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Calculate Monthly Active Users (MAU).
        /// </summary>
        public int GetMAU(int year, int month)
        {
            return cachedEvents
                .Where(e => e.Timestamp.Year == year && e.Timestamp.Month == month)
                .Select(e => e.UserId)
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Calculate retention rate for a cohort.
        /// </summary>
        public float CalculateRetention(DateTime cohortDate, int daysAfter)
        {
            var cohortUsers = cachedEvents
                .Where(e => e.Timestamp.Date == cohortDate.Date && e.EventName == "session_start")
                .Select(e => e.UserId)
                .Distinct()
                .ToList();

            if (cohortUsers.Count == 0) return 0f;

            var targetDate = cohortDate.AddDays(daysAfter);
            var returnedUsers = cachedEvents
                .Where(e => e.Timestamp.Date == targetDate.Date && cohortUsers.Contains(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .Count();

            return (float)returnedUsers / cohortUsers.Count * 100f;
        }

        /// <summary>
        /// Generate funnel conversion report.
        /// </summary>
        public FunnelReport GenerateFunnelReport(string funnelName, DateTime startDate, DateTime endDate)
        {
            var funnelEvents = cachedEvents
                .Where(e => e.EventName == "funnel_step" && 
                           e.Properties.ContainsKey("funnel") && 
                           e.Properties["funnel"].ToString() == funnelName &&
                           e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            var report = new FunnelReport { FunnelName = funnelName };
            var stepCounts = new Dictionary<string, int>();

            foreach (var evt in funnelEvents)
            {
                string step = evt.Properties["step"].ToString();
                stepCounts[step] = stepCounts.ContainsKey(step) ? stepCounts[step] + 1 : 1;
            }

            report.Steps = stepCounts.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return report;
        }

        /// <summary>
        /// Export events to CSV format.
        /// </summary>
        public string ExportToCSV(DateTime startDate, DateTime endDate)
        {
            var events = cachedEvents.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate).ToList();
            var csv = new StringBuilder();
            
            csv.AppendLine("EventName,Timestamp,UserId,SessionId,Properties");
            
            foreach (var evt in events)
            {
                string props = string.Join(";", evt.Properties.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                csv.AppendLine($"{evt.EventName},{evt.Timestamp:yyyy-MM-dd HH:mm:ss},{evt.UserId},{evt.SessionId},\"{props}\"");
            }
            
            return csv.ToString();
        }

        /// <summary>
        /// Export events to JSON format.
        /// </summary>
        public string ExportToJSON(DateTime startDate, DateTime endDate)
        {
            var events = cachedEvents.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate).ToList();
            return JsonUtility.ToJson(new { events });
        }

        /// <summary>
        /// Cache event for local querying.
        /// </summary>
        public void CacheEvent(AnalyticsEvent evt)
        {
            cachedEvents.Add(evt);
            
            // Keep only last 10000 events in memory
            if (cachedEvents.Count > 10000)
                cachedEvents.RemoveAt(0);
        }
    }

    /// <summary>
    /// Real-time analytics dashboard data provider.
    /// </summary>
    public class AnalyticsDashboard : MonoBehaviour
    {
        public DashboardData CurrentData { get; private set; }

        private void Start()
        {
            InvokeRepeating(nameof(RefreshDashboard), 0f, 5f);
        }

        /// <summary>
        /// Refresh dashboard with latest metrics.
        /// </summary>
        private void RefreshDashboard()
        {
            CurrentData = new DashboardData
            {
                ActiveSessions = GetActiveSessions(),
                TodayDAU = AnalyticsReporting.Instance.GetDAU(DateTime.Today),
                AverageFPS = PerformanceTracker.Instance != null ? GetAverageFPS() : 0f,
                TotalRevenue = CalculateTotalRevenue(),
                TopEvents = GetTopEvents(10)
            };

            GameEventSystem.Publish("analytics_dashboard_updated", CurrentData);
        }

        private int GetActiveSessions()
        {
            // Count active sessions in last 5 minutes
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            return AnalyticsReporting.Instance.QueryEvents("session_start", cutoff, DateTime.UtcNow).Count;
        }

        private float GetAverageFPS()
        {
            // This would be calculated from performance metrics
            return 60f; // Placeholder
        }

        private float CalculateTotalRevenue()
        {
            var revenueEvents = AnalyticsReporting.Instance.QueryEvents("iap_purchase", 
                DateTime.Today, DateTime.UtcNow);
            
            return revenueEvents.Sum(e => e.Properties.ContainsKey("revenue") 
                ? Convert.ToSingle(e.Properties["revenue"]) : 0f);
        }

        private Dictionary<string, int> GetTopEvents(int count)
        {
            var today = DateTime.Today;
            var events = AnalyticsReporting.Instance.QueryEvents("*", today, DateTime.UtcNow);
            
            return events
                .GroupBy(e => e.EventName)
                .OrderByDescending(g => g.Count())
                .Take(count)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    /// <summary>
    /// Alert system for anomaly detection.
    /// </summary>
    public class AnalyticsAlerts
    {
        private static AnalyticsAlerts _instance;
        public static AnalyticsAlerts Instance => _instance ?? (_instance = new AnalyticsAlerts());

        public event Action<Alert> OnAlert;

        /// <summary>
        /// Check for metric anomalies and trigger alerts.
        /// </summary>
        public void CheckForAnomalies()
        {
            CheckCrashRate();
            CheckConversionRate();
            CheckRetention();
        }

        private void CheckCrashRate()
        {
            int crashes = AnalyticsReporting.Instance.GetEventCount("app_crash", DateTime.Today);
            int sessions = AnalyticsReporting.Instance.GetEventCount("session_start", DateTime.Today);
            
            if (sessions > 0)
            {
                float crashRate = (float)crashes / sessions * 100f;
                if (crashRate > 5f) // Alert if crash rate > 5%
                {
                    TriggerAlert(new Alert
                    {
                        Severity = AlertSeverity.High,
                        Message = $"High crash rate detected: {crashRate:F2}%",
                        Metric = "crash_rate",
                        Value = crashRate
                    });
                }
            }
        }

        private void CheckConversionRate()
        {
            int purchases = AnalyticsReporting.Instance.GetEventCount("iap_purchase", DateTime.Today);
            int dau = AnalyticsReporting.Instance.GetDAU(DateTime.Today);
            
            if (dau > 0)
            {
                float conversionRate = (float)purchases / dau * 100f;
                if (conversionRate < 1f) // Alert if conversion < 1%
                {
                    TriggerAlert(new Alert
                    {
                        Severity = AlertSeverity.Medium,
                        Message = $"Low conversion rate: {conversionRate:F2}%",
                        Metric = "conversion_rate",
                        Value = conversionRate
                    });
                }
            }
        }

        private void CheckRetention()
        {
            float retention = AnalyticsReporting.Instance.CalculateRetention(DateTime.Today.AddDays(-1), 1);
            
            if (retention < 40f) // Alert if D1 retention < 40%
            {
                TriggerAlert(new Alert
                {
                    Severity = AlertSeverity.Medium,
                    Message = $"Low D1 retention: {retention:F2}%",
                    Metric = "retention_d1",
                    Value = retention
                });
            }
        }

        private void TriggerAlert(Alert alert)
        {
            Debug.LogWarning($"[Analytics Alert] {alert.Severity}: {alert.Message}");
            OnAlert?.Invoke(alert);
        }
    }

    /// <summary>
    /// Integration helper for external analytics services.
    /// </summary>
    public static class ExternalIntegrations
    {
        /// <summary>
        /// Send event to Google Analytics.
        /// </summary>
        public static void SendToGoogleAnalytics(string eventName, Dictionary<string, object> parameters)
        {
            // Integration with Google Analytics
            Debug.Log($"[GA] Event: {eventName}");
        }

        /// <summary>
        /// Send event to Mixpanel.
        /// </summary>
        public static void SendToMixpanel(string eventName, Dictionary<string, object> properties)
        {
            // Integration with Mixpanel
            Debug.Log($"[Mixpanel] Event: {eventName}");
        }

        /// <summary>
        /// Send event to Unity Analytics.
        /// </summary>
        public static void SendToUnityAnalytics(string eventName, Dictionary<string, object> parameters)
        {
            // Integration with Unity Analytics
            Debug.Log($"[Unity Analytics] Event: {eventName}");
        }
    }

    #region Data Models

    [Serializable]
    public class DashboardData
    {
        public int ActiveSessions;
        public int TodayDAU;
        public float AverageFPS;
        public float TotalRevenue;
        public Dictionary<string, int> TopEvents;
    }

    [Serializable]
    public class FunnelReport
    {
        public string FunnelName;
        public Dictionary<string, int> Steps;
    }

    [Serializable]
    public class Alert
    {
        public AlertSeverity Severity;
        public string Message;
        public string Metric;
        public float Value;
        public DateTime Timestamp = DateTime.UtcNow;
    }

    public enum AlertSeverity { Low, Medium, High, Critical }

    #endregion
}