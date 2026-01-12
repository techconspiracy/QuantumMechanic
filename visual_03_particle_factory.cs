// MODULE: Visual-03
// FILE: ParticleSystemFactory.cs
// DEPENDENCIES: UnityEngine
// INTEGRATES WITH: Future WeaponController, AbilitySystem, CombatManager
// PURPOSE: Runtime procedural particle system generation for visual effects

using UnityEngine;
using System.Collections.Generic;

namespace QuantumMechanic.Visual
{
    /// <summary>
    /// Types of visual effects that can be generated.
    /// </summary>
    public enum VFXType
    {
        MuzzleFlash,
        ProjectileTrail,
        Impact,
        Explosion,
        Aura,
        Burst,
        Smoke,
        Fire,
        Electric,
        Splash
    }

    /// <summary>
    /// Types of abilities for ability-specific effects.
    /// </summary>
    public enum AbilityType
    {
        Heal,
        Shield,
        Dash,
        Teleport,
        Buff,
        Debuff,
        Stun,
        Poison
    }

    /// <summary>
    /// Types of environmental effects.
    /// </summary>
    public enum EnvironmentType
    {
        Smoke,
        Fire,
        Steam,
        Sparks,
        Dust,
        Water,
        Electricity
    }

    /// <summary>
    /// Request structure for creating particle effects with customizable parameters.
    /// </summary>
    public class VFXRequest
    {
        public VFXType Type;
        public ModelStyle Style;
        public Color PrimaryColor = Color.white;
        public Color SecondaryColor = Color.gray;
        public float Scale = 1f;
        public float Duration = 2f;
        public float Intensity = 1f;
        public bool LoopEffect = false;
        public Dictionary<string, float> Parameters = new Dictionary<string, float>();

        public VFXRequest()
        {
            Parameters = new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Factory for creating procedural particle systems at runtime without prefabs.
    /// Generates visual effects for weapons, abilities, impacts, and environments with
    /// style-specific variations matching the procedural art direction.
    /// </summary>
    public static class ParticleSystemFactory
    {
        #region Public API

        /// <summary>
        /// Creates a particle effect from a VFX request with full customization.
        /// </summary>
        /// <param name="request">The VFX configuration request</param>
        /// <param name="parent">Optional parent transform for the effect</param>
        /// <returns>Configured ParticleSystem component</returns>
        public static ParticleSystem CreateEffect(VFXRequest request, Transform parent = null)
        {
            GameObject go = new GameObject($"VFX_{request.Type}_{request.Style}");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(request.Style);

            ConfigureMainModule(ps, request);
            ConfigureEmissionModule(ps, request);
            ConfigureShapeModule(ps, request);
            ConfigureColorModule(ps, request);
            ConfigureSizeModule(ps, request);
            ConfigureVelocityModule(ps, request);

            if (ShouldHaveNoise(request.Type))
            {
                ConfigureNoiseModule(ps, request);
            }

            if (!request.LoopEffect)
            {
                Object.Destroy(go, request.Duration + 2f);
            }

            return ps;
        }

        /// <summary>
        /// Creates a muzzle flash effect for weapon firing with style-specific visuals.
        /// </summary>
        /// <param name="style">Visual style (Cyberpunk, Fantasy, Organic, Mechanical)</param>
        /// <param name="parent">Parent transform (typically weapon barrel)</param>
        /// <returns>Configured muzzle flash ParticleSystem</returns>
        public static ParticleSystem CreateMuzzleFlash(ModelStyle style, Transform parent)
        {
            var request = new VFXRequest
            {
                Type = VFXType.MuzzleFlash,
                Style = style,
                Duration = 0.2f,
                Scale = 0.3f,
                Intensity = 3f,
                LoopEffect = false
            };

            switch (style)
            {
                case ModelStyle.Cyberpunk:
                    request.PrimaryColor = new Color(0f, 1f, 1f); // Cyan
                    request.SecondaryColor = new Color(0.5f, 0f, 1f); // Purple
                    break;
                case ModelStyle.Fantasy:
                    request.PrimaryColor = new Color(1f, 0.8f, 0f); // Gold
                    request.SecondaryColor = new Color(1f, 0.3f, 0f); // Orange
                    break;
                case ModelStyle.Organic:
                    request.PrimaryColor = new Color(0f, 1f, 0.3f); // Green
                    request.SecondaryColor = new Color(0.8f, 1f, 0f); // Yellow-green
                    break;
                case ModelStyle.Mechanical:
                    request.PrimaryColor = Color.white;
                    request.SecondaryColor = new Color(1f, 0.5f, 0f); // Orange
                    break;
            }

            ParticleSystem ps = CreateEffect(request, parent);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 15, 25)
            });

            return ps;
        }

        /// <summary>
        /// Creates a projectile trail effect that follows a moving object.
        /// </summary>
        /// <param name="style">Visual style for the trail</param>
        /// <param name="parent">Parent transform (typically the projectile)</param>
        /// <returns>Configured trail ParticleSystem</returns>
        public static ParticleSystem CreateProjectileTrail(ModelStyle style, Transform parent)
        {
            var request = new VFXRequest
            {
                Type = VFXType.ProjectileTrail,
                Style = style,
                Duration = 1f,
                Scale = 0.15f,
                Intensity = 1f,
                LoopEffect = true
            };

            request.PrimaryColor = GetStylePrimaryColor(style);
            request.SecondaryColor = GetStyleSecondaryColor(style);

            ParticleSystem ps = CreateEffect(request, parent);
            ps.main.simulationSpace = ParticleSystemSimulationSpace.World;

            return ps;
        }

        /// <summary>
        /// Creates an impact effect at a specific world position (hit sparks, debris, etc).
        /// </summary>
        /// <param name="style">Visual style for the impact</param>
        /// <param name="position">World position where impact occurred</param>
        /// <returns>Configured impact ParticleSystem</returns>
        public static ParticleSystem CreateImpactEffect(ModelStyle style, Vector3 position)
        {
            GameObject go = new GameObject("ImpactEffect");
            go.transform.position = position;

            var request = new VFXRequest
            {
                Type = VFXType.Impact,
                Style = style,
                Duration = 0.5f,
                Scale = 0.5f,
                Intensity = 2f,
                PrimaryColor = GetStyleImpactColor(style),
                SecondaryColor = Color.gray
            };

            ParticleSystem ps = CreateEffect(request, go.transform);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 20, 30)
            });

            return ps;
        }

        /// <summary>
        /// Creates an ability-specific effect (heal, shield, teleport, etc).
        /// </summary>
        /// <param name="ability">Type of ability being used</param>
        /// <param name="style">Visual style matching the game's aesthetic</param>
        /// <param name="parent">Parent transform (typically the character)</param>
        /// <returns>Configured ability ParticleSystem</returns>
        public static ParticleSystem CreateAbilityEffect(AbilityType ability, ModelStyle style, Transform parent)
        {
            var request = new VFXRequest
            {
                Style = style,
                Duration = GetAbilityDuration(ability),
                Scale = GetAbilityScale(ability),
                Intensity = 1.5f,
                LoopEffect = IsAbilityLooping(ability)
            };

            switch (ability)
            {
                case AbilityType.Heal:
                    request.Type = VFXType.Aura;
                    request.PrimaryColor = new Color(0f, 1f, 0.3f); // Green
                    request.SecondaryColor = new Color(0.5f, 1f, 0.8f); // Light green
                    break;
                case AbilityType.Shield:
                    request.Type = VFXType.Burst;
                    request.PrimaryColor = new Color(0.3f, 0.6f, 1f); // Blue
                    request.SecondaryColor = new Color(0.8f, 0.9f, 1f); // Light blue
                    break;
                case AbilityType.Dash:
                    request.Type = VFXType.Burst;
                    request.PrimaryColor = GetStylePrimaryColor(style);
                    request.SecondaryColor = Color.white;
                    break;
                case AbilityType.Teleport:
                    request.Type = VFXType.Burst;
                    request.PrimaryColor = new Color(0.5f, 0f, 1f); // Purple
                    request.SecondaryColor = new Color(1f, 0.3f, 1f); // Magenta
                    break;
                case AbilityType.Buff:
                    request.Type = VFXType.Aura;
                    request.PrimaryColor = new Color(1f, 0.8f, 0f); // Gold
                    request.SecondaryColor = new Color(1f, 1f, 0.5f); // Yellow
                    break;
                case AbilityType.Debuff:
                    request.Type = VFXType.Aura;
                    request.PrimaryColor = new Color(0.5f, 0f, 0.5f); // Dark purple
                    request.SecondaryColor = new Color(0.3f, 0f, 0.3f); // Darker purple
                    break;
                case AbilityType.Stun:
                    request.Type = VFXType.Electric;
                    request.PrimaryColor = new Color(1f, 1f, 0.3f); // Yellow
                    request.SecondaryColor = Color.white;
                    break;
                case AbilityType.Poison:
                    request.Type = VFXType.Aura;
                    request.PrimaryColor = new Color(0.3f, 0.8f, 0f); // Toxic green
                    request.SecondaryColor = new Color(0.5f, 0.5f, 0f); // Yellow-green
                    break;
            }

            return CreateEffect(request, parent);
        }

        /// <summary>
        /// Creates an environmental effect (smoke, fire, steam, etc).
        /// </summary>
        /// <param name="type">Type of environmental effect</param>
        /// <param name="parent">Parent transform for the effect</param>
        /// <returns>Configured environmental ParticleSystem</returns>
        public static ParticleSystem CreateEnvironmentalEffect(EnvironmentType type, Transform parent)
        {
            var request = new VFXRequest
            {
                Style = ModelStyle.Mechanical, // Default style for environment
                Duration = 5f,
                Scale = 1f,
                Intensity = 1f,
                LoopEffect = true
            };

            switch (type)
            {
                case EnvironmentType.Smoke:
                    request.Type = VFXType.Smoke;
                    request.PrimaryColor = new Color(0.3f, 0.3f, 0.3f);
                    request.SecondaryColor = new Color(0.5f, 0.5f, 0.5f);
                    break;
                case EnvironmentType.Fire:
                    request.Type = VFXType.Fire;
                    request.PrimaryColor = new Color(1f, 0.5f, 0f);
                    request.SecondaryColor = new Color(1f, 0.2f, 0f);
                    break;
                case EnvironmentType.Steam:
                    request.Type = VFXType.Smoke;
                    request.PrimaryColor = new Color(0.9f, 0.9f, 1f);
                    request.SecondaryColor = Color.white;
                    break;
                case EnvironmentType.Sparks:
                    request.Type = VFXType.Electric;
                    request.PrimaryColor = new Color(1f, 0.8f, 0.3f);
                    request.SecondaryColor = Color.white;
                    break;
                case EnvironmentType.Dust:
                    request.Type = VFXType.Smoke;
                    request.PrimaryColor = new Color(0.6f, 0.5f, 0.4f);
                    request.SecondaryColor = new Color(0.7f, 0.6f, 0.5f);
                    break;
                case EnvironmentType.Water:
                    request.Type = VFXType.Splash;
                    request.PrimaryColor = new Color(0.3f, 0.5f, 0.8f);
                    request.SecondaryColor = new Color(0.5f, 0.7f, 1f);
                    break;
                case EnvironmentType.Electricity:
                    request.Type = VFXType.Electric;
                    request.PrimaryColor = new Color(0.5f, 0.8f, 1f);
                    request.SecondaryColor = Color.white;
                    break;
            }

            return CreateEffect(request, parent);
        }

        /// <summary>
        /// Plays a one-shot particle effect at a specific position (not parented).
        /// </summary>
        /// <param name="ps">ParticleSystem to play</param>
        /// <param name="position">World position to play the effect</param>
        public static void PlayOneShot(ParticleSystem ps, Vector3 position)
        {
            if (ps == null) return;

            ps.transform.position = position;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }

        /// <summary>
        /// Resets a particle system to its initial state for reuse in pooling.
        /// </summary>
        /// <param name="ps">ParticleSystem to reset</param>
        public static void ResetParticleSystem(ParticleSystem ps)
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear();
            ps.time = 0f;
        }

        #endregion

        #region Module Configuration

        private static void ConfigureMainModule(ParticleSystem ps, VFXRequest request)
        {
            var main = ps.main;
            main.duration = request.Duration;
            main.loop = request.LoopEffect;
            main.startLifetime = GetLifetimeForType(request.Type);
            main.startSpeed = GetSpeedForType(request.Type) * request.Intensity;
            main.startSize = GetSizeForType(request.Type) * request.Scale;
            main.startColor = request.PrimaryColor;
            main.maxParticles = GetMaxParticlesForType(request.Type);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        private static void ConfigureEmissionModule(ParticleSystem ps, VFXRequest request)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = GetEmissionRateForType(request.Type) * request.Intensity;
        }

        private static void ConfigureShapeModule(ParticleSystem ps, VFXRequest request)
        {
            var shape = ps.shape;
            shape.enabled = true;

            switch (request.Type)
            {
                case VFXType.MuzzleFlash:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 20f;
                    shape.radius = 0.1f;
                    shape.radiusThickness = 0f;
                    break;

                case VFXType.ProjectileTrail:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.05f;
                    break;

                case VFXType.Impact:
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.radius = 0.2f;
                    shape.radiusThickness = 0.5f;
                    break;

                case VFXType.Explosion:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.5f;
                    shape.radiusThickness = 0.8f;
                    break;

                case VFXType.Aura:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 1.5f;
                    shape.radiusThickness = 0.3f;
                    break;

                case VFXType.Burst:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.5f;
                    break;

                case VFXType.Smoke:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 10f;
                    shape.radius = 0.3f;
                    break;

                case VFXType.Fire:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 5f;
                    shape.radius = 0.2f;
                    break;

                case VFXType.Electric:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.3f;
                    break;

                case VFXType.Splash:
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.radius = 0.3f;
                    break;
            }
        }

        private static void ConfigureColorModule(ParticleSystem ps, VFXRequest request)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = GetGradientForType(request.Type, request.PrimaryColor, request.SecondaryColor);
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ConfigureSizeModule(ParticleSystem ps, VFXRequest request)
        {
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            AnimationCurve curve = GetSizeCurveForType(request.Type);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void ConfigureVelocityModule(ParticleSystem ps, VFXRequest request)
        {
            var velocityOverLifetime = ps.velocityOverLifetime;

            switch (request.Type)
            {
                case VFXType.Fire:
                case VFXType.Smoke:
                    velocityOverLifetime.enabled = true;
                    velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
                    velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(1f, 3f);
                    break;

                case VFXType.Aura:
                    velocityOverLifetime.enabled = true;
                    velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(0.5f);
                    break;
            }
        }

        private static void ConfigureNoiseModule(ParticleSystem ps, VFXRequest request)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 1f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        #endregion

        #region Gradient Creation

        private static Gradient GetGradientForType(VFXType type, Color primary, Color secondary)
        {
            switch (type)
            {
                case VFXType.Fire:
                    return CreateFireGradient();

                case VFXType.Electric:
                    return CreateElectricGradient(primary);

                case VFXType.Smoke:
                    return CreateSmokeGradient(primary);

                case VFXType.Aura:
                    return CreateAuraGradient(primary, secondary);

                case VFXType.Explosion:
                    return CreateExplosionGradient(primary);

                default:
                    return CreateEnergyGradient(primary, secondary);
            }
        }

        private static Gradient CreateFireGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 0.8f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0f), 0.4f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0f), 0.7f),
                    new GradientColorKey(new Color(0.3f, 0f, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0.4f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateEnergyGradient(Color primaryColor, Color secondaryColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(primaryColor, 0.3f),
                    new GradientColorKey(secondaryColor, 0.7f),
                    new GradientColorKey(secondaryColor * 0.5f, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0.3f, 0.9f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateElectricGradient(Color baseColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(baseColor, 0.2f),
                    new GradientColorKey(baseColor * 0.7f, 0.6f),
                    new GradientColorKey(baseColor * 0.3f, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.3f),
                    new GradientAlphaKey(0.5f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateSmokeGradient(Color baseColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(baseColor * 0.8f, 0f),
                    new GradientColorKey(baseColor, 0.5f),
                    new GradientColorKey(baseColor * 1.2f, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.6f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateAuraGradient(Color primary, Color secondary)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(primary, 0f),
                    new GradientColorKey(secondary, 0.5f),
                    new GradientColorKey(primary, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.6f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0.3f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateExplosionGradient(Color baseColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(baseColor, 0.1f),
                    new GradientColorKey(baseColor * 0.5f, 0.5f),
                    new GradientColorKey(Color.black, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0.4f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        #endregion

        #region Curve Creation

        private static AnimationCurve GetSizeCurveForType(VFXType type)
        {
            switch (type)
            {
                case VFXType.MuzzleFlash:
                case VFXType.Impact:
                    return CreateDecayCurve();

                case VFXType.Explosion:
                    return CreateGrowShrinkCurve();

                case VFXType.Aura:
                    return CreatePulseCurve();

                case VFXType.Fire:
                case VFXType.Smoke:
                    return CreateGrowCurve();

                default:
                    return CreateLinearDecayCurve();
            }
        }

        private static AnimationCurve CreateDecayCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.3f, 0.7f),
                new Keyframe(1f, 0f)
            );
        }

        private static AnimationCurve CreateLinearDecayCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
        }

        private static AnimationCurve CreatePulseCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.5f, 0.5f),
                new Keyframe(0.75f, 1f),
                new Keyframe(1f, 0.5f)
            );
        }

        private static AnimationCurve CreateGrowShrinkCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.3f, 1.2f),
                new Keyframe(1f, 0f)
            );
        }

        private static AnimationCurve CreateGrowCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 1.2f)
            );
        }

        #endregion

        #region Helper Methods

        private static float GetLifetimeForType(VFXType type)
        {
            switch (type)
            {
                case VFXType.MuzzleFlash: return 0.1f;
                case VFXType.ProjectileTrail: return 0.3f;
                case VFXType.Impact: return 0.5f;
                case VFXType.Explosion: return 1.5f;
                case VFXType.Aura: return 2f;
                case VFXType.Burst: return 0.8f;
                case VFXType.Smoke: return 3f;
                case VFXType.Fire: return 1.5f;
                case VFXType.Electric: return 0.4f;
                case VFXType.Splash: return 0.6f;
                default: return 1f;
            }
        }

        private static float GetSpeedForType(VFXType type)
        {
            switch (type)
            {
                case VFXType.MuzzleFlash: return 3f;
                case VFXType.ProjectileTrail: return 0.5f;
                case VFXType.Impact: return 5f;
                case VFXType.Explosion: return 4f;
                case VFXType.Aura: return 1f;
                case VFXType.Burst: return 6f;
                case VFXType.Smoke: return 0.8f;
                case VFXType.Fire: return 2f;
                case VFXType.Electric: return 4f;
                case VFXType.Splash: return 3f;
                default: return 2f;
            }
        }

        private static float GetSizeForType(VFXType type)
        {
            switch (type)
            {
                case VFXType.MuzzleFlash: return 0.3f;
                case VFXType.ProjectileTrail: return 0.1f;
                case VFXType.Impact: return 0.2f;
                case VFXType.Explosion: return 0.8f;
                case VFXType.Aura: return 0.15f;
                case VFXType.Burst: return 0.3f;
                case VFXType.Smoke: return 0.5f;
                case VFXType.Fire: return 0.4f;
                case VFXType.Electric: return 0.15f;
                case VFXType.Splash: return 0.25f;
                default: return 0.3f;
            }
        }

        private static int GetMaxParticlesForType(VFXType type)
        {
            switch (type)
            {
                case VFXType.MuzzleFlash: return 30;
                case VFXType.ProjectileTrail: return 50;
                case VFXType.Impact: return 40;
                case VFXType.Explosion: return 100;
                case VFXType.Aura: return 80;
                case VFXType.Burst: return 60;
                case VFXType.Smoke: return 100;
                case VFXType.Fire: return 80;
                case VFXType.Electric: return 50;
                case VFXType.Splash: return 40;
                default: return 50;
            }
        }

        private static float GetEmissionRateForType(VFXType type)
        {
            switch (type)
            {
                case VFXType.MuzzleFlash: return 0f; // Uses burst
                case VFXType.ProjectileTrail: return 30f;
                case VFXType.Impact: return 0f; // Uses burst
                case VFXType.Explosion: return 0f; // Uses burst
                case VFXType.Aura: return 20f;
                case VFXType.Burst: return 0f; // Uses burst
                case VFXType.Smoke: return 15f;
                case VFXType.Fire: return 25f;
                case VFXType.Electric: return 40f;
                case VFXType.Splash: return 0f; // Uses burst
                default: return 10f;
            }
        }

        private static bool ShouldHaveNoise(VFXType type)
        {
            return type == VFXType.Fire || type == VFXType.Smoke || 
                   type == VFXType.Electric || type == VFXType.Aura;
        }

        private static Color GetStylePrimaryColor(ModelStyle style)
        {
            switch (style)
            {
                case ModelStyle.Cyberpunk: return new Color(0f, 1f, 1f); // Cyan
                case ModelStyle.Fantasy: return new Color(1f, 0.8f, 0f); // Gold
                case ModelStyle.Organic: return new Color(0f, 1f, 0.3f); // Green
                case ModelStyle.Mechanical: return new Color(0.8f, 0.8f, 0.8f); // Gray
                default: return Color.white;
            }
        }

        private static Color GetStyleSecondaryColor(ModelStyle style)
        {
            switch (style)
            {
                case ModelStyle.Cyberpunk: return new Color(0.5f, 0f, 1f); // Purple
                case ModelStyle.Fantasy: return new Color(1f, 0.3f, 0f); // Orange
                case ModelStyle.Organic: return new Color(0.8f, 1f, 0f); // Yellow-green
                case ModelStyle.Mechanical: return new Color(1f, 0.5f, 0f); // Orange
                default: return Color.gray;
            }
        }

        private static Color GetStyleImpactColor(ModelStyle style)
        {
            switch (style)
            {
                case ModelStyle.Cyberpunk: return new Color(0f, 0.8f, 1f); // Bright cyan
                case ModelStyle.Fantasy: return new Color(1f, 0.7f, 0.2f); // Bright gold
                case ModelStyle.Organic: return new Color(0.3f, 1f, 0.3f); // Bright green
                case ModelStyle.Mechanical: return Color.white; // Bright white
                default: return Color.yellow;
            }
        }

        private static float GetAbilityDuration(AbilityType ability)
        {
            switch (ability)
            {
                case AbilityType.Heal: return 2f;
                case AbilityType.Shield: return 0.5f;
                case AbilityType.Dash: return 0.3f;
                case AbilityType.Teleport: return 1f;
                case AbilityType.Buff: return 3f;
                case AbilityType.Debuff: return 3f;
                case AbilityType.Stun: return 1f;
                case AbilityType.Poison: return 4f;
                default: return 2f;
            }
        }

        private static float GetAbilityScale(AbilityType ability)
        {
            switch (ability)
            {
                case AbilityType.Heal: return 1.2f;
                case AbilityType.Shield: return 1.5f;
                case AbilityType.Dash: return 0.8f;
                case AbilityType.Teleport: return 1f;
                case AbilityType.Buff: return 1f;
                case AbilityType.Debuff: return 1f;
                case AbilityType.Stun: return 0.6f;
                case AbilityType.Poison: return 1.3f;
                default: return 1f;
            }
        }

        private static bool IsAbilityLooping(AbilityType ability)
        {
            return ability == AbilityType.Heal || ability == AbilityType.Buff || 
                   ability == AbilityType.Debuff || ability == AbilityType.Poison;
        }

        private static Material CreateParticleMaterial(ModelStyle style)
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", Color.white);
            
            switch (style)
            {
                case ModelStyle.Cyberpunk:
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    break;
                case ModelStyle.Fantasy:
                    mat.SetFloat("_Mode", 3);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    break;
                case ModelStyle.Organic:
                    mat.SetFloat("_Mode", 3);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    break;
                case ModelStyle.Mechanical:
                    mat.SetFloat("_Mode", 2); // Fade
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    break;
            }
            
            return mat;
        }

        #endregion
    }
}

// EXAMPLE USAGE:
//
// In WeaponController.cs:
//   ParticleSystem flash = ParticleSystemFactory.CreateMuzzleFlash(ModelStyle.Cyberpunk, weaponBarrel);
//   ParticleSystem trail = ParticleSystemFactory.CreateProjectileTrail(ModelStyle.Cyberpunk, projectileTransform);
//
// In CombatManager.cs:
//   ParticleSystem impact = ParticleSystemFactory.CreateImpactEffect(ModelStyle.Fantasy, hitPosition);
//
// In AbilitySystem.cs:
//   ParticleSystem heal = ParticleSystemFactory.CreateAbilityEffect(AbilityType.Heal, ModelStyle.Organic, playerTransform);
//
// In EnvironmentManager.cs:
//   ParticleSystem fire = ParticleSystemFactory.CreateEnvironmentalEffect(EnvironmentType.Fire, fireSourceTransform);
//
// For pooled effects:
//   ParticleSystemFactory.ResetParticleSystem(pooledEffect);
//   ParticleSystemFactory.PlayOneShot(pooledEffect, newPosition);