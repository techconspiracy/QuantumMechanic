#region Public API - Spawning

        /// <summary>
        /// Spawns a projectile based on the provided request.
        /// </summary>
        public ProjectileInstance SpawnProjectile(ProjectileSpawnRequest request)
        {
            // Check max projectile limits
            if (activeProjectiles.Count >= maxActiveProjectiles)
            {
                if (logProjectileEvents)
                    Debug.LogWarning($"[ProjectileSystem] Max active projectiles ({maxActiveProjectiles}) reached. Cannot spawn new projectile.");
                return null;
            }

            // Check per-caster limit
            if (request.Caster != null)
            {
                int casterProjectileCount = activeProjectiles.Count(p => p.Caster == request.Caster);
                if (casterProjectileCount >= maxProjectilesPerCaster)
                {
                    if (logProjectileEvents)
                        Debug.LogWarning($"[ProjectileSystem] Max projectiles per caster ({maxProjectilesPerCaster}) reached for {request.Caster.name}.");
                    return null;
                }
            }

            // Get pooled or new GameObject
            GameObject projectileObj = GetPooledProjectile(request.Data);
            if (projectileObj == null)
            {
                Debug.LogError("[ProjectileSystem] Failed to get pooled projectile.");
                return null;
            }

            // Set initial position and rotation
            projectileObj.transform.position = request.Origin;
            projectileObj.transform.rotation = Quaternion.LookRotation(request.Direction);

            // Create ProjectileInstance
            ProjectileInstance instance = new ProjectileInstance
            {
                GameObject = projectileObj,
                Transform = projectileObj.transform,
                Data = request.Data,
                Caster = request.Caster,
                InitialTarget = request.Target,
                CurrentTarget = request.Target,
                Direction = request.Direction.normalized,
                CurrentVelocity = request.Direction.normalized * request.Data.Speed,
                LifetimeRemaining = request.Data.Lifetime,
                DistanceTraveled = 0f,
                IsReturning = false,
                ChainCount = 0,
                HitTargets = new List<NetworkIdentity>()
            };

            // Set HitsRemaining based on collision behavior
            switch (request.Data.CollisionMode)
            {
                case CollisionBehavior.Pierce:
                    instance.HitsRemaining = request.Data.MaxPierceTargets;
                    break;
                case CollisionBehavior.Chain:
                    instance.HitsRemaining = request.Data.MaxChainTargets;
                    break;
                default:
                    instance.HitsRemaining = 1;
                    break;
            }

            // Setup visual components
            SetupTrail(instance);

            // Apply scale and color to renderer if present
            Renderer renderer = projectileObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = request.Data.ProjectileColor;
            }
            projectileObj.transform.localScale = Vector3.one * request.Data.ProjectileScale;

            // Add to active projectiles
            activeProjectiles.Add(instance);

            // Invoke event
            OnProjectileSpawned?.Invoke(instance);

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Spawned projectile {request.Data.ProjectileId} from {request.Caster?.name ?? "Unknown"}");

            return instance;
        }

        /// <summary>
        /// Spawns a projectile from an ability definition.
        /// </summary>
        public ProjectileInstance SpawnProjectileFromAbility(AbilityData ability, NetworkIdentity caster, Vector3 origin, Vector3 direction, NetworkIdentity target = null)
        {
            if (ability == null)
            {
                Debug.LogError("[ProjectileSystem] Cannot spawn projectile from null ability.");
                return null;
            }

            if (ability.ProjectileData == null)
            {
                Debug.LogError($"[ProjectileSystem] Ability {ability.abilityName} has no projectile data.");
                return null;
            }

            ProjectileSpawnRequest request = new ProjectileSpawnRequest
            {
                Data = ability.ProjectileData,
                Caster = caster,
                Origin = origin,
                Direction = direction,
                Target = target
            };

            return SpawnProjectile(request);
        }

        /// <summary>
        /// Gets a pooled projectile GameObject or creates a new one.
        /// </summary>
        private GameObject GetPooledProjectile(ProjectileData data)
        {
            if (data == null)
            {
                Debug.LogError("[ProjectileSystem] Cannot get pooled projectile for null data.");
                return null;
            }

            // Check if pool exists, if not initialize it
            if (!projectilePools.ContainsKey(data.ProjectileId))
            {
                InitializePool(data, poolInitialSize);
            }

            Queue<GameObject> pool = projectilePools[data.ProjectileId];

            // Try to get from pool
            if (pool.Count > 0)
            {
                GameObject pooled = pool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            // Pool empty, create new
            if (data.ProjectilePrefab != null)
            {
                GameObject newProjectile = Instantiate(data.ProjectilePrefab);
                if (logProjectileEvents)
                    Debug.Log($"[ProjectileSystem] Pool empty, created new projectile for {data.ProjectileId}");
                return newProjectile;
            }

            // Fallback: create primitive sphere
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * data.ProjectileScale;
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = data.ProjectileColor;
            }

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Created fallback sphere projectile for {data.ProjectileId}");

            return sphere;
        }

        #endregion

        #region Projectile Update (Private)

        /// <summary>
        /// Updates projectile position based on its type.
        /// Called from Update() loop.
        /// </summary>
        private void UpdateProjectile(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.GameObject == null)
                return;

            switch (projectile.Data.Type)
            {
                case ProjectileType.Straight:
                    UpdateStraight(projectile);
                    break;
                case ProjectileType.Homing:
                    UpdateHoming(projectile);
                    break;
                case ProjectileType.Arcing:
                    UpdateArcing(projectile);
                    break;
                case ProjectileType.Boomerang:
                    UpdateBoomerang(projectile);
                    break;
                case ProjectileType.Chaining:
                    UpdateChaining(projectile);
                    break;
                default:
                    UpdateStraight(projectile);
                    break;
            }
        }

        /// <summary>
        /// Updates straight projectile (linear velocity).
        /// </summary>
        private void UpdateStraight(ProjectileInstance projectile)
        {
            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            ApplyRotation(projectile);
        }

        /// <summary>
        /// Updates homing projectile (RotateTowards target).
        /// </summary>
        private void UpdateHoming(ProjectileInstance projectile)
        {
            // Only home after activation delay
            if (projectile.InitialTarget != null &&
                projectile.LifetimeRemaining < projectile.Data.Lifetime - projectile.Data.HomingActivationDelay)
            {
                Vector3 targetPos = projectile.InitialTarget.transform.position;
                Vector3 targetDir = (targetPos - projectile.Transform.position).normalized;
                Vector3 currentDir = projectile.CurrentVelocity.normalized;

                Vector3 newDir = Vector3.RotateTowards(currentDir, targetDir,
                    projectile.Data.HomingStrength * Time.deltaTime, 0f);

                projectile.CurrentVelocity = newDir * projectile.Data.Speed;
            }

            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
        }

        /// <summary>
        /// Updates arcing projectile (applies gravity).
        /// </summary>
        private void UpdateArcing(ProjectileInstance projectile)
        {
            // Apply gravity
            projectile.CurrentVelocity += Physics.gravity * projectile.Data.GravityMultiplier * Time.deltaTime;

            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
        }

        /// <summary>
        /// Updates boomerang projectile (returns to caster).
        /// </summary>
        private void UpdateBoomerang(ProjectileInstance projectile)
        {
            // Check if should start returning
            if (!projectile.IsReturning && projectile.DistanceTraveled >= projectile.Data.MaxDistance)
            {
                projectile.IsReturning = true;
                if (logProjectileEvents)
                    Debug.Log($"[ProjectileSystem] Boomerang projectile {projectile.Data.ProjectileId} starting return.");
            }

            if (projectile.IsReturning && projectile.Caster != null)
            {
                Vector3 casterPos = projectile.Caster.transform.position;
                Vector3 returnDir = (casterPos - projectile.Transform.position).normalized;
                projectile.CurrentVelocity = returnDir * projectile.Data.ReturnSpeed;

                // Check if returned to caster (within 1 unit)
                if (Vector3.Distance(projectile.Transform.position, casterPos) < 1f)
                {
                    if (logProjectileEvents)
                        Debug.Log($"[ProjectileSystem] Boomerang projectile {projectile.Data.ProjectileId} returned to caster.");
                    DestroyProjectile(projectile);
                    return;
                }
            }

            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            ApplyRotation(projectile);
        }

        /// <summary>
        /// Updates chaining projectile (finds next target).
        /// </summary>
        private void UpdateChaining(ProjectileInstance projectile)
        {
            // For now, just move straight - full chain logic in Chunk 3 collision detection
            UpdateStraight(projectile);
        }

        #endregion

        #region Visual Effects (Private)

        /// <summary>
        /// Applies rotation based on RotationMode.
        /// </summary>
        private void ApplyRotation(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.Transform == null)
                return;

            switch (projectile.Data.Rotation)
            {
                case RotationMode.FaceDirection:
                    if (projectile.CurrentVelocity != Vector3.zero)
                        projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
                    break;

                case RotationMode.Spin:
                    projectile.Transform.Rotate(Vector3.forward, projectile.Data.SpinSpeed * Time.deltaTime);
                    break;

                case RotationMode.Fixed:
                    // Do nothing - maintain initial rotation
                    break;

                case RotationMode.Random:
                    projectile.Transform.rotation = Random.rotation;
                    break;
            }
        }

        /// <summary>
        /// Configures the TrailRenderer component.
        /// </summary>
        private void SetupTrail(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.GameObject == null)
                return;

            if (!projectile.Data.UseTrail)
                return;

            TrailRenderer trail = projectile.GameObject.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = projectile.GameObject.AddComponent<TrailRenderer>();
            }

            trail.time = 0.5f;
            trail.startWidth = projectile.Data.ProjectileScale * 0.3f;
            trail.endWidth = 0f;
            trail.startColor = projectile.Data.ProjectileColor;
            trail.endColor = new Color(
                projectile.Data.ProjectileColor.r,
                projectile.Data.ProjectileColor.g,
                projectile.Data.ProjectileColor.b,
                0f
            );

            projectile.Trail = trail;

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Setup trail for projectile {projectile.Data.ProjectileId}");
        }

        /// <summary>
        /// Initializes the object pool for a projectile type.
        /// </summary>
        private void InitializePool(ProjectileData data, int initialSize)
        {
            if (data == null)
            {
                Debug.LogError("[ProjectileSystem] Cannot initialize pool for null data.");
                return;
            }

            if (projectilePools.ContainsKey(data.ProjectileId))
            {
                if (logProjectileEvents)
                    Debug.LogWarning($"[ProjectileSystem] Pool for {data.ProjectileId} already exists.");
                return;
            }

            // Create pool parent object
            GameObject poolParent = new GameObject($"Pool_{data.ProjectileId}");
            poolParent.transform.SetParent(transform);
            poolParents[data.ProjectileId] = poolParent;

            Queue<GameObject> pool = new Queue<GameObject>();

            for (int i = 0; i < initialSize; i++)
            {
                GameObject projectile;
                if (data.ProjectilePrefab != null)
                {
                    projectile = Instantiate(data.ProjectilePrefab, poolParent.transform);
                }
                else
                {
                    projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    projectile.transform.SetParent(poolParent.transform);
                    projectile.transform.localScale = Vector3.one * data.ProjectileScale;
                    
                    Renderer renderer = projectile.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = data.ProjectileColor;
                    }
                }

                projectile.name = $"{data.ProjectileId}_Pooled_{i}";
                projectile.SetActive(false);
                pool.Enqueue(projectile);
            }

            projectilePools[data.ProjectileId] = pool;

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Initialized pool for {data.ProjectileId} with {initialSize} projectiles.");
        }

        #endregion