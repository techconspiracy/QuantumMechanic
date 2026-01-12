#endregion

        #region Collision Detection (Private)

        /// <summary>
        /// Checks for collisions with targets in the scene.
        /// Called from Update() loop.
        /// </summary>
        /// <param name="projectile">The projectile instance to check collisions for.</param>
        private void CheckCollisions(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.GameObject == null)
                return;

            // Get all valid targets
            List<NetworkIdentity> validTargets = GetValidTargets(projectile);

            // Check collision with each target
            foreach (NetworkIdentity target in validTargets)
            {
                if (target == null || target.gameObject == null)
                    continue;

                // Calculate distance to target
                float distance = Vector3.Distance(projectile.Transform.position, target.transform.position);

                // Check if within collision radius
                if (distance <= projectile.Data.CollisionRadius)
                {
                    HandleCollision(projectile, target);
                    return; // Exit after handling collision
                }
            }
        }

        /// <summary>
        /// Handles collision with a single target.
        /// Processes collision behavior (Pierce, Bounce, Explode, Chain, Stop) and invokes events.
        /// </summary>
        /// <param name="projectile">The projectile that collided.</param>
        /// <param name="target">The target that was hit.</param>
        private void HandleCollision(ProjectileInstance projectile, NetworkIdentity target)
        {
            if (projectile == null || target == null)
                return;

            // Add target to hit list
            projectile.HitTargets.Add(target);

            // Create hit result
            ProjectileHitResult result = new ProjectileHitResult
            {
                Projectile = projectile,
                HitTarget = target,
                HitPosition = target.transform.position,
                HitNormal = (target.transform.position - projectile.Transform.position).normalized,
                Damage = projectile.Data.Damage,
                IsExplosion = false
            };

            // Invoke hit event
            OnProjectileHit?.Invoke(result);

            // Decrement hits remaining
            projectile.HitsRemaining--;

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Projectile hit {target.name} at {target.transform.position}. Hits remaining: {projectile.HitsRemaining}");

            // Handle collision behavior
            switch (projectile.Data.CollisionBehavior)
            {
                case CollisionBehavior.Pierce:
                    if (projectile.HitsRemaining <= 0)
                    {
                        DestroyProjectile(projectile);
                    }
                    // else continue moving through target
                    break;

                case CollisionBehavior.Bounce:
                    if (projectile.HitsRemaining <= 0)
                    {
                        DestroyProjectile(projectile);
                    }
                    else
                    {
                        // Simple reflection off target
                        Vector3 hitDir = (projectile.Transform.position - target.transform.position).normalized;
                        projectile.CurrentVelocity = hitDir * projectile.Data.Speed;

                        if (logProjectileEvents)
                            Debug.Log($"[ProjectileSystem] Projectile bounced with new velocity: {projectile.CurrentVelocity}");
                    }
                    break;

                case CollisionBehavior.Explode:
                    HandleExplosion(projectile);
                    DestroyProjectile(projectile);
                    break;

                case CollisionBehavior.Chain:
                    projectile.ChainCount++;

                    if (projectile.HitsRemaining > 0)
                    {
                        // Find next target
                        NetworkIdentity nextTarget = FindNextChainTarget(projectile);

                        if (nextTarget != null)
                        {
                            projectile.CurrentTarget = nextTarget;
                            Vector3 chainDir = (nextTarget.transform.position - projectile.Transform.position).normalized;
                            projectile.CurrentVelocity = chainDir * projectile.Data.Speed;

                            if (logProjectileEvents)
                                Debug.Log($"[ProjectileSystem] Projectile chained to {nextTarget.name} (Chain #{projectile.ChainCount})");
                        }
                        else
                        {
                            // No more targets, destroy
                            if (logProjectileEvents)
                                Debug.Log($"[ProjectileSystem] No more chain targets found. Destroying projectile.");
                            DestroyProjectile(projectile);
                        }
                    }
                    else
                    {
                        DestroyProjectile(projectile);
                    }
                    break;

                case CollisionBehavior.Stop:
                    DestroyProjectile(projectile);
                    break;
            }
        }

        /// <summary>
        /// Finds the next chain target within range.
        /// Returns the closest valid target that hasn't been hit yet.
        /// </summary>
        /// <param name="projectile">The projectile looking for a chain target.</param>
        /// <returns>The next valid chain target, or null if none found.</returns>
        private NetworkIdentity FindNextChainTarget(ProjectileInstance projectile)
        {
            List<NetworkIdentity> validTargets = GetValidTargets(projectile);

            if (validTargets.Count == 0)
                return null;

            NetworkIdentity closestTarget = null;
            float closestDistance = projectile.Data.ChainRange;

            foreach (NetworkIdentity target in validTargets)
            {
                if (target == null || target.gameObject == null)
                    continue;

                float distance = Vector3.Distance(projectile.Transform.position, target.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Handles explosion damage and effects.
        /// Applies damage to all targets within explosion radius and invokes explosion events.
        /// </summary>
        /// <param name="projectile">The projectile that is exploding.</param>
        private void HandleExplosion(ProjectileInstance projectile)
        {
            if (projectile.Data.ExplosionRadius <= 0f)
                return;

            // Get all NetworkIdentities in scene
            NetworkIdentity[] allTargets = FindObjectsOfType<NetworkIdentity>();

            List<NetworkIdentity> hitByExplosion = new List<NetworkIdentity>();

            foreach (NetworkIdentity target in allTargets)
            {
                if (target == null || target.gameObject == null)
                    continue;

                // Skip caster
                if (target == projectile.Caster)
                    continue;

                float distance = Vector3.Distance(projectile.Transform.position, target.transform.position);

                if (distance <= projectile.Data.ExplosionRadius)
                {
                    hitByExplosion.Add(target);

                    // Create hit result for each explosion victim
                    ProjectileHitResult result = new ProjectileHitResult
                    {
                        Projectile = projectile,
                        HitTarget = target,
                        HitPosition = target.transform.position,
                        HitNormal = (target.transform.position - projectile.Transform.position).normalized,
                        Damage = projectile.Data.Damage * projectile.Data.ExplosionDamageMultiplier,
                        IsExplosion = true
                    };

                    OnProjectileHit?.Invoke(result);
                }
            }

            // Invoke explosion event
            OnProjectileExploded?.Invoke(projectile, hitByExplosion);

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Explosion hit {hitByExplosion.Count} targets at {projectile.Transform.position} with radius {projectile.Data.ExplosionRadius}");
        }

        #endregion

        #region Helper Methods (Private)

        /// <summary>
        /// Gets all valid targets in the scene (excluding caster and already hit targets).
        /// </summary>
        /// <param name="projectile">The projectile to get valid targets for.</param>
        /// <returns>List of valid NetworkIdentity targets.</returns>
        private List<NetworkIdentity> GetValidTargets(ProjectileInstance projectile)
        {
            NetworkIdentity[] allTargets = FindObjectsOfType<NetworkIdentity>();
            List<NetworkIdentity> validTargets = new List<NetworkIdentity>();

            foreach (NetworkIdentity target in allTargets)
            {
                if (IsValidTarget(projectile, target))
                {
                    validTargets.Add(target);
                }
            }

            return validTargets;
        }

        /// <summary>
        /// Checks if a target is valid for this projectile.
        /// Validates target existence, caster check, and hit history.
        /// </summary>
        /// <param name="projectile">The projectile checking for valid targets.</param>
        /// <param name="target">The potential target to validate.</param>
        /// <returns>True if target is valid, false otherwise.</returns>
        private bool IsValidTarget(ProjectileInstance projectile, NetworkIdentity target)
        {
            if (target == null || target.gameObject == null)
                return false;

            // Can't hit self (caster)
            if (target == projectile.Caster)
                return false;

            // Can't hit already-hit targets (unless bounce/pierce allows multiple hits)
            if (projectile.HitTargets.Contains(target))
                return false;

            // Additional validation could go here (team checks, layer masks, etc.)

            return true;
        }

        #endregion
    }
}