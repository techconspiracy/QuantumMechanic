// MODULE: Combat-02
// FILE: WeaponController.cs
// DEPENDENCIES: NetworkIdentity.cs, DamageSystem.cs
// INTEGRATES WITH: Future NetworkManager, PlayerController, UIManager, ParticleSystemFactory
// PURPOSE: Client-predicted weapon firing with server validation and damage application

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace QuantumMechanic.Combat
{
    /// <summary>
    /// Client-predicted weapon controller with server-authoritative hit validation.
    /// Handles weapon firing, recoil, ammo management, and damage application.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("References")]
        [Tooltip("NetworkIdentity component for shooter identification")]
        public NetworkIdentity NetworkIdentity;
        
        [Tooltip("Camera transform for aim direction calculation")]
        public Transform CameraTransform;
        
        [Tooltip("Muzzle point for projectile spawn and effects")]
        public Transform MuzzlePoint;
        
        [Header("Weapon Data")]
        [Tooltip("Current equipped weapon configuration")]
        public WeaponData CurrentWeapon;
        
        [Header("Recoil Settings")]
        [Tooltip("Speed at which recoil recovers to center")]
        [Range(1f, 20f)]
        public float RecoilRecoverySpeed = 5f;
        
        [Tooltip("Recoil pattern curve (shot count normalized 0-1 on X, multiplier on Y)")]
        public AnimationCurve RecoilPattern = AnimationCurve.Linear(0f, 1f, 1f, 1.5f);
        
        [Header("Audio")]
        [Tooltip("Audio source for weapon sounds")]
        public AudioSource WeaponAudioSource;
        
        [Tooltip("Fire sound effect")]
        public AudioClip FireSound;
        
        [Tooltip("Reload sound effect")]
        public AudioClip ReloadSound;
        
        [Tooltip("Empty magazine click sound")]
        public AudioClip EmptySound;
        
        #endregion
        
        #region Private State
        
        private float lastFireTime;
        private float nextFireTime;
        private bool isReloading;
        private bool isFiring;
        private Vector2 currentRecoil;
        private int shotsFired;
        private Coroutine reloadCoroutine;
        
        // Server-side fire rate tracking (per NetworkIdentity)
        private static Dictionary<NetworkIdentity, float> serverFireTimestamps = new Dictionary<NetworkIdentity, float>();
        
        // Server-side ammo tracking (per NetworkIdentity)
        private static Dictionary<NetworkIdentity, WeaponAmmoState> serverAmmoStates = new Dictionary<NetworkIdentity, WeaponAmmoState>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Invoked when weapon fires (client-side)
        /// </summary>
        public event System.Action OnWeaponFired;
        
        /// <summary>
        /// Invoked when weapon reload completes
        /// </summary>
        public event System.Action OnWeaponReloaded;
        
        /// <summary>
        /// Invoked when ammo count changes
        /// </summary>
        public event System.Action OnAmmoChanged;
        
        /// <summary>
        /// Invoked when server confirms hit result
        /// </summary>
        public event System.Action<FireResult> OnHitConfirmed;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (NetworkIdentity == null)
            {
                NetworkIdentity = GetComponent<NetworkIdentity>();
            }
            
            if (WeaponAudioSource == null)
            {
                WeaponAudioSource = gameObject.AddComponent<AudioSource>();
                WeaponAudioSource.spatialBlend = 1f;
                WeaponAudioSource.maxDistance = 50f;
            }
        }
        
        private void Start()
        {
            // Initialize weapon ammo
            if (CurrentWeapon != null)
            {
                CurrentWeapon.CurrentAmmo = CurrentWeapon.MagazineSize;
            }
            
            // Register server-side state
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                InitializeServerState();
            }
        }
        
        private void Update()
        {
            UpdateRecoilRecovery();
            UpdateAutomaticFire();
        }
        
        private void OnDestroy()
        {
            // Clean up server state
            if (NetworkIdentity != null && serverFireTimestamps.ContainsKey(NetworkIdentity))
            {
                serverFireTimestamps.Remove(NetworkIdentity);
                serverAmmoStates.Remove(NetworkIdentity);
            }
        }
        
        #endregion
        
        #region Public API - Input Handling
        
        /// <summary>
        /// Called when player presses fire button. Handles client prediction and server request.
        /// </summary>
        public void OnFireInput()
        {
            if (!CanFire())
            {
                // Play empty sound if out of ammo
                if (CurrentWeapon != null && CurrentWeapon.CurrentAmmo <= 0 && EmptySound != null)
                {
                    PlaySound(EmptySound);
                }
                return;
            }
            
            // Client-side prediction
            PerformClientRaycast();
            PlayFireEffects();
            ApplyRecoil();
            ConsumeAmmo();
            
            // Update fire timing
            lastFireTime = Time.time;
            nextFireTime = Time.time + CurrentWeapon.FireRate;
            shotsFired++;
            
            // Create server request
            FireRequest request = new FireRequest
            {
                Shooter = NetworkIdentity,
                Origin = CameraTransform.position,
                Direction = CameraTransform.forward,
                Timestamp = Time.time,
                Weapon = CurrentWeapon
            };
            
            // Process on server (in standalone mode, process locally)
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                FireResult result = ProcessFireRequest(request);
                if (result != null)
                {
                    OnHitConfirmed?.Invoke(result);
                }
            }
            
            // TODO: Send request to NetworkManager for actual multiplayer
            // NetworkManager.SendFireRequest(request);
            
            OnWeaponFired?.Invoke();
        }
        
        /// <summary>
        /// Called when player presses reload button. Initiates reload sequence.
        /// </summary>
        public void OnReloadInput()
        {
            Reload();
        }
        
        /// <summary>
        /// Sets weapon aiming state for accuracy modifications.
        /// </summary>
        /// <param name="isAiming">True if player is aiming down sights</param>
        public void SetAiming(bool isAiming)
        {
            // Future: Reduce spread when aiming
            // Future: Slow movement when aiming
            // Future: Increase zoom when aiming
        }
        
        /// <summary>
        /// Begins continuous fire for automatic weapons.
        /// </summary>
        public void StartFiring()
        {
            if (CurrentWeapon != null && CurrentWeapon.IsAutomatic)
            {
                isFiring = true;
            }
        }
        
        /// <summary>
        /// Stops continuous fire for automatic weapons.
        /// </summary>
        public void StopFiring()
        {
            isFiring = false;
        }
        
        #endregion
        
        #region Public API - Weapon Management
        
        /// <summary>
        /// Equips a new weapon and resets state.
        /// </summary>
        /// <param name="weapon">Weapon data to equip</param>
        public void EquipWeapon(WeaponData weapon)
        {
            if (weapon == null) return;
            
            CurrentWeapon = weapon;
            CurrentWeapon.CurrentAmmo = CurrentWeapon.MagazineSize;
            shotsFired = 0;
            currentRecoil = Vector2.zero;
            isReloading = false;
            
            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }
            
            OnAmmoChanged?.Invoke();
        }
        
        /// <summary>
        /// Initiates weapon reload if conditions are met.
        /// </summary>
        public void Reload()
        {
            if (isReloading) return;
            if (CurrentWeapon == null) return;
            if (CurrentWeapon.CurrentAmmo == CurrentWeapon.MagazineSize) return;
            if (CurrentWeapon.ReserveAmmo == 0) return;
            
            reloadCoroutine = StartCoroutine(ReloadCoroutine());
        }
        
        /// <summary>
        /// Checks if weapon can fire based on ammo, fire rate, and reload state.
        /// </summary>
        /// <returns>True if weapon can fire</returns>
        public bool CanFire()
        {
            if (CurrentWeapon == null) return false;
            if (isReloading) return false;
            if (CurrentWeapon.CurrentAmmo <= 0) return false;
            if (Time.time < nextFireTime) return false;
            
            return true;
        }
        
        /// <summary>
        /// Gets current ammo as percentage of magazine size.
        /// </summary>
        /// <returns>Ammo percentage (0-1)</returns>
        public float GetAmmoPercentage()
        {
            if (CurrentWeapon == null || CurrentWeapon.MagazineSize == 0) return 0f;
            return (float)CurrentWeapon.CurrentAmmo / CurrentWeapon.MagazineSize;
        }
        
        /// <summary>
        /// Gets total ammo (magazine + reserve).
        /// </summary>
        /// <returns>Total ammo count</returns>
        public int GetTotalAmmo()
        {
            if (CurrentWeapon == null) return 0;
            return CurrentWeapon.CurrentAmmo + CurrentWeapon.ReserveAmmo;
        }
        
        #endregion
        
        #region Public API - Server Processing
        
        /// <summary>
        /// Processes fire request on server with authoritative hit detection and damage application.
        /// </summary>
        /// <param name="request">Fire request from client</param>
        /// <returns>Fire result with hit information and damage result</returns>
        public static FireResult ProcessFireRequest(FireRequest request)
        {
            // Validate request
            if (!ValidateFireRequest(request))
            {
                return null;
            }
            
            // Update server-side fire timestamp
            serverFireTimestamps[request.Shooter] = request.Timestamp;
            
            // Consume ammo on server
            if (serverAmmoStates.ContainsKey(request.Shooter))
            {
                serverAmmoStates[request.Shooter].CurrentAmmo--;
            }
            
            // Process each projectile (for multi-projectile weapons like shotguns)
            List<FireResult> results = new List<FireResult>();
            
            for (int i = 0; i < request.Weapon.ProjectilesPerShot; i++)
            {
                FireResult result = ProcessSingleProjectile(request);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            
            // Return first valid result (or combined result for shotguns)
            return results.Count > 0 ? results[0] : new FireResult { DidHit = false };
        }
        
        /// <summary>
        /// Validates fire request for anti-cheat checks.
        /// </summary>
        /// <param name="request">Fire request to validate</param>
        /// <returns>True if request is valid</returns>
        public static bool ValidateFireRequest(FireRequest request)
        {
            // Validate shooter exists
            if (request.Shooter == null) return false;
            
            // Validate weapon data
            if (request.Weapon == null) return false;
            
            // Validate direction vector
            if (request.Direction.magnitude < 0.9f || request.Direction.magnitude > 1.1f)
            {
                Debug.LogWarning($"Invalid direction vector magnitude: {request.Direction.magnitude}");
                return false;
            }
            
            // Validate server-side ammo
            if (serverAmmoStates.ContainsKey(request.Shooter))
            {
                if (serverAmmoStates[request.Shooter].CurrentAmmo <= 0)
                {
                    Debug.LogWarning($"Server ammo check failed for {request.Shooter.NetId}");
                    return false;
                }
            }
            
            // Validate fire rate
            if (serverFireTimestamps.ContainsKey(request.Shooter))
            {
                float timeSinceLastFire = request.Timestamp - serverFireTimestamps[request.Shooter];
                if (timeSinceLastFire < request.Weapon.FireRate * 0.9f) // 10% tolerance
                {
                    Debug.LogWarning($"Fire rate exceeded for {request.Shooter.NetId}: {timeSinceLastFire}s < {request.Weapon.FireRate}s");
                    return false;
                }
            }
            
            return true;
        }
        
        #endregion
        
        #region Private Methods - Client Prediction
        
        private void PerformClientRaycast()
        {
            if (CurrentWeapon == null) return;
            
            for (int i = 0; i < CurrentWeapon.ProjectilesPerShot; i++)
            {
                Vector3 direction = CalculateSpread(CameraTransform.forward, CurrentWeapon.SpreadAngle);
                Vector3 origin = CameraTransform.position;
                
                RaycastHit hit;
                if (Physics.Raycast(origin, direction, out hit, CurrentWeapon.Range))
                {
                    // Visual feedback only (no damage on client)
                    SpawnHitEffect(hit.point, hit.normal);
                    
                    // Draw debug line
                    Debug.DrawLine(origin, hit.point, Color.red, 0.5f);
                }
                else
                {
                    // Miss - draw to max range
                    Debug.DrawRay(origin, direction * CurrentWeapon.Range, Color.yellow, 0.5f);
                }
            }
        }
        
        private void ApplyRecoil()
        {
            if (CurrentWeapon == null) return;
            if (CameraTransform == null) return;
            
            // Vertical recoil (always upward)
            float recoilMultiplier = RecoilPattern.Evaluate(Mathf.Clamp01(shotsFired / 10f));
            float verticalRecoil = CurrentWeapon.RecoilAmount * recoilMultiplier;
            
            // Horizontal recoil (random left/right)
            float horizontalRecoil = Random.Range(-0.5f, 0.5f) * CurrentWeapon.RecoilAmount;
            
            // Accumulate recoil
            currentRecoil += new Vector2(verticalRecoil, horizontalRecoil);
            
            // Apply to camera
            CameraTransform.localRotation *= Quaternion.Euler(-verticalRecoil, horizontalRecoil, 0f);
        }
        
        private void UpdateRecoilRecovery()
        {
            if (currentRecoil.magnitude > 0.01f)
            {
                currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, Time.deltaTime * RecoilRecoverySpeed);
                
                // Apply recovery to camera
                if (CameraTransform != null)
                {
                    float recoveryX = currentRecoil.x * Time.deltaTime * RecoilRecoverySpeed;
                    float recoveryY = currentRecoil.y * Time.deltaTime * RecoilRecoverySpeed;
                    CameraTransform.localRotation *= Quaternion.Euler(recoveryX, -recoveryY, 0f);
                }
            }
            else
            {
                currentRecoil = Vector2.zero;
                
                // Reset shot counter when fully recovered
                if (Time.time - lastFireTime > 1f)
                {
                    shotsFired = 0;
                }
            }
        }
        
        private void UpdateAutomaticFire()
        {
            if (isFiring && CurrentWeapon != null && CurrentWeapon.IsAutomatic)
            {
                if (CanFire())
                {
                    OnFireInput();
                }
            }
        }
        
        private void ConsumeAmmo()
        {
            if (CurrentWeapon == null) return;
            
            if (CurrentWeapon.CurrentAmmo > 0)
            {
                CurrentWeapon.CurrentAmmo--;
                OnAmmoChanged?.Invoke();
                
                // Auto-reload when empty (if enabled)
                if (CurrentWeapon.CurrentAmmo == 0 && CurrentWeapon.ReserveAmmo > 0)
                {
                    Reload();
                }
            }
        }
        
        private void PlayFireEffects()
        {
            // Play fire sound
            if (FireSound != null)
            {
                PlaySound(FireSound);
            }
            
            // Spawn muzzle flash at muzzle point
            if (MuzzlePoint != null)
            {
                SpawnMuzzleFlash(MuzzlePoint.position, MuzzlePoint.forward);
            }
            
            // Future: Spawn bullet tracer
            // Future: Play weapon animation
            // Future: Camera shake
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (WeaponAudioSource != null && clip != null)
            {
                WeaponAudioSource.PlayOneShot(clip);
            }
        }
        
        #endregion
        
        #region Private Methods - Reload
        
        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            
            // Play reload sound
            if (ReloadSound != null)
            {
                PlaySound(ReloadSound);
            }
            
            // Wait for reload time
            yield return new WaitForSeconds(CurrentWeapon.ReloadTime);
            
            // Calculate ammo transfer
            int ammoNeeded = CurrentWeapon.MagazineSize - CurrentWeapon.CurrentAmmo;
            int ammoToReload = Mathf.Min(ammoNeeded, CurrentWeapon.ReserveAmmo);
            
            // Transfer ammo
            CurrentWeapon.CurrentAmmo += ammoToReload;
            CurrentWeapon.ReserveAmmo -= ammoToReload;
            
            // Update server state
            if (NetworkIdentity != null && serverAmmoStates.ContainsKey(NetworkIdentity))
            {
                serverAmmoStates[NetworkIdentity].CurrentAmmo = CurrentWeapon.CurrentAmmo;
                serverAmmoStates[NetworkIdentity].ReserveAmmo = CurrentWeapon.ReserveAmmo;
            }
            
            isReloading = false;
            reloadCoroutine = null;
            
            OnWeaponReloaded?.Invoke();
            OnAmmoChanged?.Invoke();
        }
        
        #endregion
        
        #region Private Methods - Server Processing
        
        private static FireResult ProcessSingleProjectile(FireRequest request)
        {
            FireResult result = new FireResult();
            
            // Apply weapon spread
            Vector3 direction = CalculateSpread(request.Direction, request.Weapon.SpreadAngle);
            
            // Perform authoritative raycast
            RaycastHit hit;
            if (Physics.Raycast(request.Origin, direction, out hit, request.Weapon.Range))
            {
                result.DidHit = true;
                result.HitPosition = hit.point;
                result.HitNormal = hit.normal;
                result.Distance = hit.distance;
                
                // Check if target is entity
                NetworkIdentity targetIdentity = hit.collider.GetComponent<NetworkIdentity>();
                if (targetIdentity != null)
                {
                    result.Target = targetIdentity;
                    
                    // Check for headshot
                    result.WasHeadshot = hit.collider.CompareTag("Head");
                    
                    // Calculate damage
                    float damage = request.Weapon.BaseDamage;
                    if (result.WasHeadshot)
                    {
                        damage *= request.Weapon.HeadshotMultiplier;
                    }
                    
                    // Create damage request
                    DamageRequest damageRequest = new DamageRequest
                    {
                        Attacker = request.Shooter,
                        Target = targetIdentity,
                        BaseDamage = damage,
                        Type = request.Weapon.DamageType,
                        CanCrit = true,
                        ArmorPenetration = request.Weapon.ArmorPenetration,
                        HitPosition = hit.point,
                        HitNormal = hit.normal
                    };
                    
                    // Apply damage through DamageSystem
                    DamageSystem.ApplyDamage(damageRequest);
                    result.DamageResult = DamageSystem.CalculateDamage(damageRequest);
                }
            }
            else
            {
                result.DidHit = false;
            }
            
            return result;
        }
        
        private void InitializeServerState()
        {
            if (CurrentWeapon == null) return;
            
            serverFireTimestamps[NetworkIdentity] = -999f; // Allow immediate first shot
            serverAmmoStates[NetworkIdentity] = new WeaponAmmoState
            {
                CurrentAmmo = CurrentWeapon.CurrentAmmo,
                ReserveAmmo = CurrentWeapon.ReserveAmmo
            };
        }
        
        #endregion
        
        #region Private Methods - Helpers
        
        private static Vector3 CalculateSpread(Vector3 direction, float spreadAngle)
        {
            if (spreadAngle <= 0f) return direction;
            
            // Random point in cone
            float randomX = Random.Range(-spreadAngle, spreadAngle);
            float randomY = Random.Range(-spreadAngle, spreadAngle);
            
            // Create rotation quaternion
            Quaternion spread = Quaternion.Euler(randomX, randomY, 0f);
            
            // Apply spread to direction
            Vector3 spreadDirection = spread * direction;
            return spreadDirection.normalized;
        }
        
        private void SpawnMuzzleFlash(Vector3 position, Vector3 direction)
        {
            // Future: Use ParticleSystemFactory to spawn muzzle flash
            // ParticleSystemFactory.CreateMuzzleFlash(position, direction);
            
            // Temporary debug visualization
            Debug.DrawRay(position, direction * 0.5f, Color.yellow, 0.1f);
        }
        
        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            // Future: Use ParticleSystemFactory to spawn hit particles
            // ParticleSystemFactory.CreateImpactEffect(position, normal);
            
            // Temporary debug visualization
            Debug.DrawRay(position, normal * 0.3f, Color.red, 0.5f);
        }
        
        #endregion
    }
    
    #region Data Structures
    
    /// <summary>
    /// Weapon configuration data with all parameters for firing, damage, and behavior.
    /// </summary>
    [System.Serializable]
    public class WeaponData
    {
        [Header("Identification")]
        public string WeaponName = "Assault Rifle";
        
        [Header("Damage")]
        public DamageType DamageType = DamageType.Physical;
        public float BaseDamage = 30f;
        public float HeadshotMultiplier = 2f;
        public float ArmorPenetration = 0.1f;
        
        [Header("Fire Rate")]
        public float FireRate = 0.1f; // Seconds between shots
        public bool IsAutomatic = true;
        
        [Header("Range & Accuracy")]
        public float Range = 100f;
        public float SpreadAngle = 1f; // Cone of inaccuracy in degrees
        public int ProjectilesPerShot = 1; // For shotguns
        
        [Header("Recoil")]
        public float RecoilAmount = 0.5f;
        
        [Header("Ammo")]
        public int MagazineSize = 30;
        public int CurrentAmmo = 30;
        public int ReserveAmmo = 120;
        public float ReloadTime = 2f;
    }
    
    /// <summary>
    /// Fire request sent from client to server for validation.
    /// </summary>
    public class FireRequest
    {
        public NetworkIdentity Shooter;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Timestamp;
        public WeaponData Weapon;
    }
    
    /// <summary>
    /// Fire result returned from server with hit information and damage result.
    /// </summary>
    public class FireResult
    {
        public bool DidHit;
        public NetworkIdentity Target;
        public Vector3 HitPosition;
        public Vector3 HitNormal;
        public float Distance;
        public bool WasHeadshot;
        public DamageResult DamageResult;
    }
    
    /// <summary>
    /// Server-side ammo state for anti-cheat validation.
    /// </summary>
    internal class WeaponAmmoState
    {
        public int CurrentAmmo;
        public int ReserveAmmo;
    }
    
    #endregion
}

/*
EXAMPLE USAGE:

1. Setup on Player GameObject:
   - Add NetworkIdentity component
   - Add WeaponController component
   - Assign CameraTransform (first-person camera)
   - Create empty child GameObject for MuzzlePoint
   - Configure WeaponData in inspector

2. Player Input Integration:
   void Update()
   {
       if (Input.GetMouseButtonDown(0))
       {
           weaponController.StartFiring();
       }
       
       if (Input.GetMouseButtonUp(0))
       {
           weaponController.StopFiring();
       }
       
       if (Input.GetKeyDown(KeyCode.R))
       {
           weaponController.OnReloadInput();
       }
   }

3. UI Integration:
   weaponController.OnAmmoChanged += UpdateAmmoUI;
   
   void UpdateAmmoUI()
   {
       ammoText.text = $"{weaponController.CurrentWeapon.CurrentAmmo} / {weaponController.CurrentWeapon.ReserveAmmo}";
   }

4. Server Processing (in NetworkManager):
   void OnFireRequestReceived(FireRequest request)
   {
       FireResult result = WeaponController.ProcessFireRequest(request);
       BroadcastFireResult(result);
   }

5. Creating Weapon Presets:
   WeaponData rifle = new WeaponData
   {
       WeaponName = "AR-15",
       BaseDamage = 30f,
       FireRate = 0.08f,
       Range = 150f,
       MagazineSize = 30,
       RecoilAmount = 0.4f,
       IsAutomatic = true
   };
   
   WeaponData shotgun = new WeaponData
   {
       WeaponName = "Shotgun",
       BaseDamage = 15f,
       FireRate = 0.8f,
       Range = 30f,
       MagazineSize = 8,
       ProjectilesPerShot = 8,
       SpreadAngle = 5f,
       IsAutomatic = false
   };
*/