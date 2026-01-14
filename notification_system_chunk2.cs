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
