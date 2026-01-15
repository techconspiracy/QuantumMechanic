using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.VFX
{
    /// <summary>
    /// Manages ability and skill visual effects
    /// </summary>
    public class AbilityVFX : MonoBehaviour
    {
        public static AbilityVFX Instance { get; private set; }

        [SerializeField] private GameObject projectileTrailPrefab;
        [SerializeField] private float trailSpeed = 10f;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Spawn projectile with trail from start to target
        /// </summary>
        public void SpawnProjectile(string effectName, Vector3 start, Vector3 target, Action onHit = null)
        {
            StartCoroutine(ProjectileCoroutine(effectName, start, target, onHit));
        }

        private IEnumerator ProjectileCoroutine(string effectName, Vector3 start, Vector3 target, Action onHit)
        {
            ParticleEffect projectile = ParticleManager.Instance.SpawnEffect(effectName, start);
            Vector3 direction = (target - start).normalized;
            float distance = Vector3.Distance(start, target);
            float traveled = 0f;

            while (traveled < distance)
            {
                float step = trailSpeed * Time.deltaTime;
                projectile.transform.position += direction * step;
                traveled += step;
                yield return null;
            }

            onHit?.Invoke();
            ParticleManager.Instance.ReturnEffect(projectile);
        }
    }

    /// <summary>
    /// Combat-specific visual effects (hits, blood, sparks)
    /// </summary>
    public class CombatVFX : MonoBehaviour
    {
        public static CombatVFX Instance { get; private set; }

        [Header("Hit Effects")]
        [SerializeField] private string sparkEffectName = "HitSpark";
        [SerializeField] private string bloodEffectName = "BloodSplatter";
        [SerializeField] private string explosionEffectName = "Explosion";

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Spawn hit effect based on surface type
        /// </summary>
        public void SpawnHitEffect(Vector3 position, Vector3 normal, SurfaceType surface)
        {
            string effectName = surface switch
            {
                SurfaceType.Flesh => bloodEffectName,
                SurfaceType.Metal => sparkEffectName,
                _ => sparkEffectName
            };

            ParticleManager.Instance.SpawnEffect(effectName, position, normal);
        }

        /// <summary>
        /// Spawn explosion effect with radius
        /// </summary>
        public void SpawnExplosion(Vector3 position, float radius = 1f)
        {
            ParticleEffect explosion = ParticleManager.Instance.SpawnEffect(explosionEffectName, position);
            if (explosion != null)
            {
                explosion.SetSize(radius);
                CameraShakeManager.Instance?.Shake(radius * 0.5f, 0.3f);
            }
        }
    }

    /// <summary>
    /// Environmental particle effects (dust, leaves, splashes)
    /// </summary>
    public class EnvironmentalVFX : MonoBehaviour
    {
        public static EnvironmentalVFX Instance { get; private set; }

        [Header("Environment Effects")]
        [SerializeField] private string dustEffectName = "Dust";
        [SerializeField] private string leavesEffectName = "Leaves";
        [SerializeField] private string waterSplashName = "WaterSplash";

        private void Awake()
        {
            Instance = this;
        }

        public void SpawnDust(Vector3 position, float intensity = 1f)
        {
            ParticleEffect dust = ParticleManager.Instance.SpawnEffect(dustEffectName, position);
            dust?.SetSize(intensity);
        }

        public void SpawnWaterSplash(Vector3 position, float intensity = 1f)
        {
            ParticleManager.Instance.SpawnEffect(waterSplashName, position);
        }
    }

    /// <summary>
    /// Weather particle systems (rain, snow, fog)
    /// </summary>
    public class WeatherVFX : MonoBehaviour
    {
        public static WeatherVFX Instance { get; private set; }

        [Header("Weather Particles")]
        [SerializeField] private ParticleSystem rainSystem;
        [SerializeField] private ParticleSystem snowSystem;
        [SerializeField] private ParticleSystem fogSystem;

        private WeatherType currentWeather = WeatherType.Clear;
        private float currentIntensity = 0f;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Set weather type and intensity
        /// </summary>
        public void SetWeather(WeatherType weather, float intensity)
        {
            currentWeather = weather;
            currentIntensity = Mathf.Clamp01(intensity);

            StopAllWeather();

            switch (weather)
            {
                case WeatherType.Rain:
                    SetParticleIntensity(rainSystem, intensity);
                    break;
                case WeatherType.Snow:
                    SetParticleIntensity(snowSystem, intensity);
                    break;
                case WeatherType.Fog:
                    SetParticleIntensity(fogSystem, intensity);
                    break;
            }
        }

        private void SetParticleIntensity(ParticleSystem ps, float intensity)
        {
            if (ps == null) return;

            var emission = ps.emission;
            emission.rateOverTimeMultiplier = intensity * 100f;
            ps.Play();
        }

        private void StopAllWeather()
        {
            rainSystem?.Stop();
            snowSystem?.Stop();
            fogSystem?.Stop();
        }
    }

    /// <summary>
    /// Magic spell and enchantment effects
    /// </summary>
    public class MagicVFX : MonoBehaviour
    {
        public static MagicVFX Instance { get; private set; }

        [Header("Magic Effects")]
        [SerializeField] private string buffEffectName = "BuffAura";
        [SerializeField] private string debuffEffectName = "DebuffAura";
        [SerializeField] private string healEffectName = "HealSparkle";

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Cast spell from caster to target with callback
        /// </summary>
        public void CastSpell(string spellEffect, Vector3 startPos, Vector3 targetPos, Action onImpact = null)
        {
            AbilityVFX.Instance.SpawnProjectile(spellEffect, startPos, targetPos, onImpact);
        }

        /// <summary>
        /// Attach buff/debuff aura to transform
        /// </summary>
        public ParticleEffect AttachAura(Transform target, bool isBuff)
        {
            string effectName = isBuff ? buffEffectName : debuffEffectName;
            ParticleEffect aura = ParticleManager.Instance.SpawnEffect(effectName, target.position);
            if (aura != null)
            {
                aura.transform.SetParent(target);
                aura.transform.localPosition = Vector3.zero;
            }
            return aura;
        }
    }

    /// <summary>
    /// Trail renderer for weapons and movement
    /// </summary>
    public class TrailVFXSystem : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trailPrefab;
        private Dictionary<string, TrailRenderer> activeTrails = new Dictionary<string, TrailRenderer>();

        /// <summary>
        /// Start trail on object
        /// </summary>
        public void StartTrail(string trailId, Transform followTarget, Color color, float width = 0.1f)
        {
            if (activeTrails.ContainsKey(trailId))
                return;

            TrailRenderer trail = Instantiate(trailPrefab, followTarget);
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.startWidth = width;
            trail.endWidth = 0f;
            activeTrails[trailId] = trail;
        }

        /// <summary>
        /// Stop and remove trail
        /// </summary>
        public void StopTrail(string trailId)
        {
            if (activeTrails.TryGetValue(trailId, out TrailRenderer trail))
            {
                trail.emitting = false;
                Destroy(trail.gameObject, trail.time);
                activeTrails.Remove(trailId);
            }
        }
    }

    /// <summary>
    /// Beam and laser effects
    /// </summary>
    public class BeamVFX : MonoBehaviour
    {
        [SerializeField] private LineRenderer beamPrefab;

        /// <summary>
        /// Create beam from start to end point
        /// </summary>
        public LineRenderer CreateBeam(Vector3 start, Vector3 end, Color color, float width = 0.1f, float duration = 1f)
        {
            LineRenderer beam = Instantiate(beamPrefab);
            beam.startColor = color;
            beam.endColor = color;
            beam.startWidth = width;
            beam.endWidth = width;
            beam.SetPosition(0, start);
            beam.SetPosition(1, end);

            Destroy(beam.gameObject, duration);
            return beam;
        }
    }

    /// <summary>
    /// Shield and barrier effects
    /// </summary>
    public class ShieldVFX : MonoBehaviour
    {
        [SerializeField] private GameObject shieldPrefab;

        public GameObject SpawnShield(Transform target, float radius = 2f, float duration = 5f)
        {
            GameObject shield = Instantiate(shieldPrefab, target);
            shield.transform.localScale = Vector3.one * radius;
            Destroy(shield, duration);
            return shield;
        }
    }

    public enum SurfaceType { Flesh, Metal, Wood, Stone, Dirt }
    public enum WeatherType { Clear, Rain, Snow, Fog }
}