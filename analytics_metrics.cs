using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace QuantumMechanic.Analytics
{
    /// <summary>
    /// Performance tracking for FPS, memory, and load times.
    /// </summary>
    public class PerformanceTracker : MonoBehaviour
    {
        private static PerformanceTracker _instance;
        public static PerformanceTracker Instance => _instance;

        private float[] fpsBuffer = new float[60];
        private int fpsBufferIndex = 0;
        private float lastFpsTrack = 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            TrackFPS();
        }

        /// <summary>
        /// Track current FPS and send metrics periodically.
        /// </summary>
        public void TrackFPS()
        {
            float fps = 1f / Time.unscaledDeltaTime;
            fpsBuffer[fpsBufferIndex] = fps;
            fpsBufferIndex = (fpsBufferIndex + 1) % fpsBuffer.Length;

            if (Time.time - lastFpsTrack > 60f)
            {
                float avgFps = CalculateAverageFPS();
                AnalyticsManager.Instance?.TrackEvent("performance_fps", new Dictionary<string, object>
                {
                    {"avg_fps", avgFps},
                    {"min_fps", GetMinFPS()},
                    {"max_fps", GetMaxFPS()}
                });
                lastFpsTrack = Time.time;
            }
        }

        /// <summary>
        /// Track current memory usage.
        /// </summary>
        public void TrackMemoryUsage()
        {
            long totalMemory = Profiler.GetTotalAllocatedMemoryLong() / 1048576; // MB
            long reservedMemory = Profiler.GetTotalReservedMemoryLong() / 1048576;

            AnalyticsManager.Instance?.TrackEvent("performance_memory", new Dictionary<string, object>
            {
                {"total_mb", totalMemory},
                {"reserved_mb", reservedMemory}
            });
        }

        /// <summary>
        /// Track scene load time.
        /// </summary>
        public void TrackLoadTime(string sceneName, float loadDuration)
        {
            AnalyticsManager.Instance?.TrackEvent("performance_load", new Dictionary<string, object>
            {
                {"scene", sceneName},
                {"duration", loadDuration}
            });
        }

        private float CalculateAverageFPS() => fpsBuffer.Length > 0 ? fpsBuffer.Sum() / fpsBuffer.Length : 0f;
        private float GetMinFPS() => fpsBuffer.Min();
        private float GetMaxFPS() => fpsBuffer.Max();
    }

    /// <summary>
    /// Gameplay metrics tracking for sessions, playtime, and progression.
    /// </summary>
    public class GameplayMetrics
    {
        /// <summary>
        /// Track level completion with detailed metrics.
        /// </summary>
        public static void TrackLevelComplete(int level, float timeSpent, int score, bool perfect = false)
        {
            AnalyticsManager.Instance?.TrackEvent("level_complete", new Dictionary<string, object>
            {
                {"level", level},
                {"time_spent", timeSpent},
                {"score", score},
                {"perfect", perfect}
            });
        }

        /// <summary>
        /// Track player death/failure.
        /// </summary>
        public static void TrackPlayerDeath(string location, string causeOfDeath, int attempts)
        {
            AnalyticsManager.Instance?.TrackEvent("player_death", new Dictionary<string, object>
            {
                {"location", location},
                {"cause", causeOfDeath},
                {"attempts", attempts}
            });
        }

        /// <summary>
        /// Track achievement unlock.
        /// </summary>
        public static void TrackAchievement(string achievementId, int rarity)
        {
            AnalyticsManager.Instance?.TrackEvent("achievement_unlock", new Dictionary<string, object>
            {
                {"achievement_id", achievementId},
                {"rarity", rarity}
            });
        }

        /// <summary>
        /// Track tutorial completion step.
        /// </summary>
        public static void TrackTutorialStep(string stepId, int stepNumber, bool completed)
        {
            AnalyticsManager.Instance?.TrackEvent("tutorial_step", new Dictionary<string, object>
            {
                {"step_id", stepId},
                {"step_number", stepNumber},
                {"completed", completed}
            });
        }
    }

    /// <summary>
    /// Economy metrics for currency and purchases.
    /// </summary>
    public class EconomyMetrics
    {
        /// <summary>
        /// Track virtual currency earned.
        /// </summary>
        public static void TrackCurrencyEarned(string currencyType, int amount, string source)
        {
            AnalyticsManager.Instance?.TrackEvent("currency_earned", new Dictionary<string, object>
            {
                {"currency_type", currencyType},
                {"amount", amount},
                {"source", source}
            });
        }

        /// <summary>
        /// Track virtual currency spent.
        /// </summary>
        public static void TrackCurrencySpent(string currencyType, int amount, string itemId)
        {
            AnalyticsManager.Instance?.TrackEvent("currency_spent", new Dictionary<string, object>
            {
                {"currency_type", currencyType},
                {"amount", amount},
                {"item_id", itemId}
            });
        }

        /// <summary>
        /// Track real money purchase.
        /// </summary>
        public static void TrackRevenue(string productId, decimal price, string currency)
        {
            AnalyticsManager.Instance?.TrackEvent("iap_purchase", new Dictionary<string, object>
            {
                {"product_id", productId},
                {"price", (float)price},
                {"currency", currency},
                {"revenue", (float)price}
            });
        }

        /// <summary>
        /// Track item acquisition.
        /// </summary>
        public static void TrackItemAcquired(string itemId, int quantity, string method)
        {
            AnalyticsManager.Instance?.TrackEvent("item_acquired", new Dictionary<string, object>
            {
                {"item_id", itemId},
                {"quantity", quantity},
                {"method", method}
            });
        }
    }

    /// <summary>
    /// Funnel tracking for user progression through flows.
    /// </summary>
    public class FunnelTracker
    {
        private static FunnelTracker _instance;
        public static FunnelTracker Instance => _instance ?? (_instance = new FunnelTracker());

        private Dictionary<string, List<string>> activeFunnels = new Dictionary<string, List<string>>();

        /// <summary>
        /// Track a step in a funnel (e.g., onboarding, tutorial).
        /// </summary>
        public void TrackFunnelStep(string funnelName, string stepName, int stepIndex)
        {
            if (!activeFunnels.ContainsKey(funnelName))
                activeFunnels[funnelName] = new List<string>();

            activeFunnels[funnelName].Add(stepName);

            AnalyticsManager.Instance?.TrackEvent("funnel_step", new Dictionary<string, object>
            {
                {"funnel", funnelName},
                {"step", stepName},
                {"step_index", stepIndex},
                {"completed_steps", activeFunnels[funnelName].Count}
            });
        }

        /// <summary>
        /// Mark funnel as completed.
        /// </summary>
        public void CompleteFunnel(string funnelName)
        {
            if (!activeFunnels.ContainsKey(funnelName)) return;

            AnalyticsManager.Instance?.TrackEvent("funnel_complete", new Dictionary<string, object>
            {
                {"funnel", funnelName},
                {"total_steps", activeFunnels[funnelName].Count}
            });

            activeFunnels.Remove(funnelName);
        }
    }

    /// <summary>
    /// A/B testing framework for variant assignment and tracking.
    /// </summary>
    public class ABTestManager
    {
        private static ABTestManager _instance;
        public static ABTestManager Instance => _instance ?? (_instance = new ABTestManager());

        private Dictionary<string, string> assignedVariants = new Dictionary<string, string>();

        /// <summary>
        /// Assign user to A/B test variant.
        /// </summary>
        public string AssignVariant(string testName, params string[] variants)
        {
            if (assignedVariants.ContainsKey(testName))
                return assignedVariants[testName];

            // Simple random assignment - use more sophisticated methods in production
            string variant = variants[UnityEngine.Random.Range(0, variants.Length)];
            assignedVariants[testName] = variant;

            AnalyticsManager.Instance?.TrackEvent("ab_test_assigned", new Dictionary<string, object>
            {
                {"test_name", testName},
                {"variant", variant}
            });

            return variant;
        }

        /// <summary>
        /// Track conversion for A/B test.
        /// </summary>
        public void TrackConversion(string testName, string goalName)
        {
            if (!assignedVariants.ContainsKey(testName)) return;

            AnalyticsManager.Instance?.TrackEvent("ab_test_conversion", new Dictionary<string, object>
            {
                {"test_name", testName},
                {"variant", assignedVariants[testName]},
                {"goal", goalName}
            });
        }
    }

    /// <summary>
    /// Heatmap tracking for player positions and interactions.
    /// </summary>
    public class HeatmapTracker
    {
        private static HeatmapTracker _instance;
        public static HeatmapTracker Instance => _instance ?? (_instance = new HeatmapTracker());

        /// <summary>
        /// Track player position for heatmap generation.
        /// </summary>
        public void TrackPosition(Vector3 position, string context = "")
        {
            AnalyticsManager.Instance?.TrackEvent("heatmap_position", new Dictionary<string, object>
            {
                {"x", position.x},
                {"y", position.y},
                {"z", position.z},
                {"context", context}
            });
        }

        /// <summary>
        /// Track interaction point (clicks, hits, etc.).
        /// </summary>
        public void TrackInteraction(Vector3 position, string interactionType)
        {
            AnalyticsManager.Instance?.TrackEvent("heatmap_interaction", new Dictionary<string, object>
            {
                {"x", position.x},
                {"y", position.y},
                {"z", position.z},
                {"type", interactionType}
            });
        }
    }
}