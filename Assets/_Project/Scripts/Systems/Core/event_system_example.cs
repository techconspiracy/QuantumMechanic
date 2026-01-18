using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic
{
    /// <summary>
    /// Example Quantum Mechanic system that provides event dispatch functionality.
    /// This demonstrates best practices for creating systems:
    /// 
    /// 1. Uses [QuantumSystem] to auto-register with the bootstrapper
    /// 2. Declares capabilities with [ProvidesCapability]
    /// 3. Declares hooks with [ProvidesHook]
    /// 4. Inherits from BaseGameSystem for lifecycle management
    /// 5. Uses HookManager for hook functionality (composition over inheritance)
    /// 6. Uses async OnInitialize() for setup
    /// 7. Implements IsHealthy() for health monitoring
    /// 
    /// This system provides a simple publish/subscribe event system that other
    /// systems can use to communicate without tight coupling.
    /// 
    /// Usage Example:
    /// 
    /// // Subscribe to events
    /// EventSystem events = QuantumBootstrapper.Instance.GetSystem<EventSystem>();
    /// events.Subscribe("PlayerDied", (data) => {
    ///     Debug.Log($"Player died: {data}");
    /// });
    /// 
    /// // Publish events
    /// events.Publish("PlayerDied", new { playerName = "Alice", score = 1000 });
    /// </summary>
    [QuantumSystem(InitializationPhase.Core, priority: 200, DisplayName = "Event System")]
    [ProvidesCapability("EventDispatch", Version = 1, Description = "Basic publish/subscribe event system")]
    [ProvidesHook("OnEventPublished", typeof(Action<string, object>), Description = "Fired whenever an event is published")]
    public class EventSystem : BaseGameSystem
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // PRIVATE STATE
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Manages hook registration and invocation.
        /// C# doesn't support multiple inheritance, so we use composition instead.
        /// </summary>
        private HookManager hookManager = new HookManager();

        /// <summary>
        /// Dictionary mapping event names to lists of subscribers.
        /// Each event can have multiple subscribers (callbacks).
        /// </summary>
        private Dictionary<string, List<Action<object>>> events;

        /// <summary>
        /// Tracks total number of events published (for diagnostics).
        /// </summary>
        private int totalEventsPublished = 0;

        /// <summary>
        /// Tracks total number of subscribers across all events.
        /// </summary>
        private int totalSubscribers = 0;

        // ═══════════════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes the event system.
        /// Called automatically by the bootstrapper during the Core phase.
        /// 
        /// Sets up the event dictionary and registers the "OnEventPublished" hook
        /// so other systems can monitor all events if needed.
        /// </summary>
        protected override async Awaitable OnInitialize()
        {
            // Create the event storage dictionary
            events = new Dictionary<string, List<Action<object>>>();

            // Register the hook that fires whenever any event is published
            hookManager.RegisterHook<Action<string, object>>("OnEventPublished");

            Log("Event dispatch system initialized");
            Log("Ready to receive subscriptions and publish events");

            // Wait one frame to ensure other systems can start subscribing
            await Awaitable.NextFrameAsync();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // PUBLIC API - Event Subscription
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Subscribe to an event by name. Your callback will be invoked whenever
        /// the event is published.
        /// 
        /// This method is marked with [CapabilityImplementation] to document that
        /// it's part of the "EventDispatch" capability contract.
        /// </summary>
        /// <param name="eventName">Name of the event (e.g., "PlayerDied", "QuestCompleted")</param>
        /// <param name="callback">Function to call when the event fires (receives event data)</param>
        [CapabilityImplementation("EventDispatch")]
        public void Subscribe(string eventName, Action<object> callback)
        {
            // Validation
            if (string.IsNullOrEmpty(eventName))
            {
                LogError("Cannot subscribe to empty event name");
                return;
            }

            if (callback == null)
            {
                LogError($"Cannot subscribe to '{eventName}' with null callback");
                return;
            }

            // Create event list if this is the first subscriber
            if (!events.ContainsKey(eventName))
            {
                events[eventName] = new List<Action<object>>();
                Log($"Created new event: '{eventName}'");
            }

            // Add callback to subscriber list
            events[eventName].Add(callback);
            totalSubscribers++;

            Log($"Subscribed to '{eventName}' (now {events[eventName].Count} subscribers)");
        }

        /// <summary>
        /// Unsubscribe from an event. Your callback will no longer be invoked.
        /// Important: You must unsubscribe when your system shuts down to prevent
        /// memory leaks and null reference errors.
        /// </summary>
        /// <param name="eventName">Name of the event to unsubscribe from</param>
        /// <param name="callback">The exact callback instance you originally subscribed</param>
        [CapabilityImplementation("EventDispatch")]
        public void Unsubscribe(string eventName, Action<object> callback)
        {
            // Validation
            if (string.IsNullOrEmpty(eventName))
            {
                LogWarning("Cannot unsubscribe from empty event name");
                return;
            }

            if (callback == null)
            {
                LogWarning($"Cannot unsubscribe from '{eventName}' with null callback");
                return;
            }

            // Check if event exists
            if (!events.ContainsKey(eventName))
            {
                LogWarning($"Attempted to unsubscribe from non-existent event: '{eventName}'");
                return;
            }

            // Remove callback
            bool removed = events[eventName].Remove(callback);
            
            if (removed)
            {
                totalSubscribers--;
                Log($"Unsubscribed from '{eventName}' ({events[eventName].Count} subscribers remaining)");

                // Clean up empty event lists to save memory
                if (events[eventName].Count == 0)
                {
                    events.Remove(eventName);
                    Log($"Removed empty event: '{eventName}'");
                }
            }
            else
            {
                LogWarning($"Callback was not subscribed to '{eventName}'");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // PUBLIC API - Event Publishing
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Publishes an event to all subscribers.
        /// This invokes all registered callbacks with the provided data.
        /// Also triggers the "OnEventPublished" hook for monitoring systems.
        /// 
        /// This method is marked with [CapabilityImplementation] to document that
        /// it's part of the "EventDispatch" capability contract.
        /// </summary>
        /// <param name="eventName">Name of the event to publish</param>
        /// <param name="data">Data to pass to subscribers (can be null, or any object/struct)</param>
        [CapabilityImplementation("EventDispatch")]
        public void Publish(string eventName, object data)
        {
            // Validation
            if (string.IsNullOrEmpty(eventName))
            {
                LogError("Cannot publish event with empty name");
                return;
            }

            // Track statistics
            totalEventsPublished++;

            // Check if anyone is listening
            if (!events.ContainsKey(eventName))
            {
                // This is not an error - it's fine to publish events no one is listening to
                // (Systems might subscribe later, or it might be an optional event)
                return;
            }

            // Get subscriber list (create a copy to prevent modification during iteration)
            List<Action<object>> subscribers = new List<Action<object>>(events[eventName]);

            Log($"Publishing '{eventName}' to {subscribers.Count} subscribers");

            // Invoke all subscribers
            int successCount = 0;
            int errorCount = 0;

            foreach (var callback in subscribers)
            {
                try
                {
                    callback?.Invoke(data);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    LogError($"Error in subscriber callback for '{eventName}': {ex.Message}\n{ex.StackTrace}");
                }
            }

            // Log summary if there were errors
            if (errorCount > 0)
            {
                LogWarning($"Published '{eventName}': {successCount} succeeded, {errorCount} failed");
            }

            // Invoke the hook so other systems can monitor event traffic
            // (e.g., for analytics, logging, debugging)
            hookManager.InvokeHook("OnEventPublished", eventName, data);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // PUBLIC API - Hook Access
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Allows external systems to subscribe to the OnEventPublished hook.
        /// This hook fires whenever ANY event is published through this system.
        /// 
        /// Example:
        /// eventSystem.SubscribeToHook<Action<string, object>>("OnEventPublished", 
        ///     (name, data) => Debug.Log($"Event '{name}' published with data: {data}"));
        /// </summary>
        public void SubscribeToHook<T>(string hookName, T handler) where T : Delegate
        {
            hookManager.SubscribeToHook(hookName, handler);
        }

        /// <summary>
        /// Allows external systems to unsubscribe from the OnEventPublished hook.
        /// </summary>
        public void UnsubscribeFromHook<T>(string hookName, T handler) where T : Delegate
        {
            hookManager.UnsubscribeFromHook(hookName, handler);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // PUBLIC API - Diagnostics
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the number of subscribers for a specific event.
        /// Useful for debugging and diagnostics.
        /// </summary>
        /// <param name="eventName">Name of the event to check</param>
        /// <returns>Number of subscribers, or 0 if event doesn't exist</returns>
        public int GetSubscriberCount(string eventName)
        {
            if (string.IsNullOrEmpty(eventName) || !events.ContainsKey(eventName))
            {
                return 0;
            }

            return events[eventName].Count;
        }

        /// <summary>
        /// Gets a list of all registered event names.
        /// Useful for debugging and editor tools.
        /// </summary>
        /// <returns>List of event names that currently have subscribers</returns>
        public List<string> GetAllEventNames()
        {
            return new List<string>(events.Keys);
        }

        /// <summary>
        /// Gets statistics about the event system.
        /// Useful for performance monitoring and debugging.
        /// </summary>
        /// <returns>Formatted string with system stats</returns>
        public string GetStatistics()
        {
            return $"EventSystem Stats:\n" +
                   $"  Active Events: {events.Count}\n" +
                   $"  Total Subscribers: {totalSubscribers}\n" +
                   $"  Events Published: {totalEventsPublished}";
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // HEALTH MONITORING
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks if the event system is healthy.
        /// Overrides BaseGameSystem.IsHealthy() to add custom health checks.
        /// 
        /// Returns false if:
        /// - Base health check fails (not initialized)
        /// - Event dictionary is null (catastrophic failure)
        /// </summary>
        public override bool IsHealthy()
        {
            // First check base health (initialized and healthy flags)
            if (!base.IsHealthy())
            {
                return false;
            }

            // Check that our critical data structure exists
            if (events == null)
            {
                LogError("Event dictionary is null - critical failure!");
                return false;
            }

            // All checks passed
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // CLEANUP (Optional but recommended)
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called when the system is being destroyed.
        /// Cleans up all event subscriptions to prevent memory leaks.
        /// 
        /// Note: This is a Unity MonoBehaviour callback, not part of IGameSystem.
        /// BaseGameSystem doesn't derive from MonoBehaviour, so this won't be called
        /// automatically unless you add MonoBehaviour to your inheritance chain.
        /// 
        /// For proper cleanup, consider adding an IDisposable interface or
        /// a Shutdown() method to the IGameSystem contract.
        /// </summary>
        private void OnDestroy()
        {
            if (events != null)
            {
                Log($"Cleaning up {events.Count} events and {totalSubscribers} subscribers");
                events.Clear();
            }
        }
    }
}