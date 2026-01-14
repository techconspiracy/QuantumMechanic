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