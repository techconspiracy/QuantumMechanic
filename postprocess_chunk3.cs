using UnityEngine;
using System.Collections;

namespace QuantumMechanic.Rendering
{
    /// <summary>
    /// Dynamic post-processing effects triggered by gameplay events
    /// </summary>
    public partial class PostProcessManager
    {
        [Header("Dynamic Effects")]
        [SerializeField] private float damageFlashDuration = 0.2f;
        [SerializeField] private float lowHealthThreshold = 0.3f;
        [SerializeField] private AnimationCurve healthVignetteCurve;

        private float currentHealthPercent = 1f;
        private bool isInCinematicMode = false;
        private Coroutine fadeCoroutine;

        public enum EnvironmentType { None, Underwater, Fog, Darkness, Rain, Snow, Fire, Toxic }
        private EnvironmentType currentEnvironment = EnvironmentType.None;

        private void Update()
        {
            UpdateDynamicEffects();
            UpdateHealthBasedEffects();
            UpdateEnvironmentEffects();
        }

        /// <summary>
        /// Update dynamic effects each frame
        /// </summary>
        private void UpdateDynamicEffects()
        {
            // Update effect stack priorities
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                activeEffects[i].elapsed += Time.deltaTime;
                if (activeEffects[i].duration > 0 && activeEffects[i].elapsed >= activeEffects[i].duration)
                {
                    activeEffects.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Trigger damage flash effect
        /// </summary>
        public void TriggerDamageFlash(float intensity = 0.8f, Color? flashColor = null)
        {
            StartCoroutine(DamageFlashCoroutine(intensity, flashColor ?? new Color(1f, 0f, 0f, 0.5f)));
        }

        private IEnumerator DamageFlashCoroutine(float intensity, Color flashColor)
        {
            float elapsed = 0f;
            colorAdjustments.active = true;

            while (elapsed < damageFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / damageFlashDuration);
                colorAdjustments.colorFilter.value = Color.Lerp(Color.white, flashColor, t * intensity);
                yield return null;
            }

            colorAdjustments.colorFilter.value = Color.white;
        }

        /// <summary>
        /// Set health-based vignette effect
        /// </summary>
        public void SetHealthVignette(float healthPercent)
        {
            currentHealthPercent = healthPercent;
        }

        /// <summary>
        /// Update health-based visual effects
        /// </summary>
        private void UpdateHealthBasedEffects()
        {
            if (currentHealthPercent < lowHealthThreshold)
            {
                float vignetteIntensity = healthVignetteCurve?.Evaluate(currentHealthPercent) ?? (1f - currentHealthPercent);
                SetVignette(vignetteIntensity * 0.6f, 0.3f, new Color(0.8f, 0f, 0f));
                
                // Pulse effect when critically low
                if (currentHealthPercent < 0.15f)
                {
                    float pulse = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
                    SetChromaticAberration(pulse * 0.2f);
                }
            }
            else
            {
                SetVignette(0f);
                SetChromaticAberration(0f);
            }
        }

        /// <summary>
        /// Set environment-based post-processing
        /// </summary>
        public void SetEnvironmentEffect(EnvironmentType environmentType, float intensity = 1f)
        {
            currentEnvironment = environmentType;

            switch (environmentType)
            {
                case EnvironmentType.Underwater:
                    ApplyUnderwaterEffect(intensity);
                    break;
                case EnvironmentType.Fog:
                    ApplyColorGrade(saturation: -20f * intensity, contrast: -10f * intensity);
                    SetVignette(0.3f * intensity, 0.6f, new Color(0.7f, 0.7f, 0.8f));
                    break;
                case EnvironmentType.Darkness:
                    SetVignette(0.7f * intensity, 0.2f);
                    ApplyColorGrade(saturation: -30f * intensity);
                    break;
                case EnvironmentType.Fire:
                    ApplyColorGrade(temperature: 30f * intensity, saturation: 20f * intensity);
                    ApplyHeatDistortion(Vector3.zero, intensity * 0.5f);
                    break;
                case EnvironmentType.Toxic:
                    ApplyColorGrade(tint: 40f * intensity, saturation: -10f * intensity);
                    SetVignette(0.4f * intensity, 0.4f, new Color(0f, 0.8f, 0.2f));
                    break;
            }
        }

        /// <summary>
        /// Update environment-specific effects
        /// </summary>
        private void UpdateEnvironmentEffects()
        {
            if (currentEnvironment == EnvironmentType.Rain)
            {
                // Rain on lens effect
                float rainIntensity = Mathf.PerlinNoise(Time.time * 5f, 0f);
                SetBloom(rainIntensity * 0.1f);
            }
        }

        /// <summary>
        /// Apply dynamic exposure adjustment
        /// </summary>
        public void SetDynamicExposure(float targetExposure, float adaptationSpeed = 2f)
        {
            StartCoroutine(ExposureTransition(targetExposure, adaptationSpeed));
        }

        private IEnumerator ExposureTransition(float target, float speed)
        {
            float current = colorAdjustments.postExposure.value;
            while (Mathf.Abs(current - target) > 0.01f)
            {
                current = Mathf.Lerp(current, target, Time.deltaTime * speed);
                colorAdjustments.postExposure.value = current;
                yield return null;
            }
        }

        /// <summary>
        /// Fade to black transition
        /// </summary>
        public void FadeToBlack(float duration, System.Action onComplete = null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f, duration, onComplete));
        }

        /// <summary>
        /// Fade from black transition
        /// </summary>
        public void FadeFromBlack(float duration, System.Action onComplete = null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, duration, onComplete));
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration, System.Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float vignetteValue = Mathf.Lerp(from, to, t);
                SetVignette(vignetteValue, 0.1f);
                yield return null;
            }
            onComplete?.Invoke();
        }

        /// <summary>
        /// Enter cinematic mode with letterbox and enhanced effects
        /// </summary>
        public void EnableCinematicMode(bool enabled, float focusDistance = 10f)
        {
            isInCinematicMode = enabled;

            if (enabled)
            {
                SetDepthOfField(focusDistance, 2.8f);
                ApplyColorGrade(saturation: 15f, contrast: 10f);
                SetBloom(0.25f);
                // Letterbox would be handled by UI overlay
            }
            else
            {
                depthOfField.active = false;
                ResetAllEffects();
            }
        }

        /// <summary>
        /// Apply status effect visual (poison, fire, etc.)
        /// </summary>
        public void ApplyStatusEffect(StatusEffectType effectType, float intensity)
        {
            switch (effectType)
            {
                case StatusEffectType.Poison:
                    ApplyColorGrade(tint: 30f * intensity, saturation: -10f * intensity);
                    SetVignette(0.3f * intensity, 0.5f, new Color(0f, 0.6f, 0.1f));
                    break;
                case StatusEffectType.Burning:
                    ApplyColorGrade(temperature: 25f * intensity, saturation: 15f * intensity);
                    SetChromaticAberration(intensity * 0.2f);
                    break;
                case StatusEffectType.Frozen:
                    ApplyColorGrade(tint: -40f * intensity, saturation: -20f * intensity);
                    SetVignette(0.4f * intensity, 0.4f, new Color(0.6f, 0.8f, 1f));
                    break;
                case StatusEffectType.Stunned:
                    ApplyScreenDistortion(intensity * 0.1f, Vector2.zero, 2f);
                    SetMotionBlur(intensity * 0.4f);
                    break;
            }
        }

        /// <summary>
        /// Performance-based quality adjustment
        /// </summary>
        public void AdjustQualityForPerformance(float targetFrameRate = 60f)
        {
            float currentFPS = 1f / Time.deltaTime;
            if (currentFPS < targetFrameRate * 0.8f)
            {
                enableMotionBlur = false;
                enableSSAO = false;
                SetBloom(0f);
            }
        }

        public enum StatusEffectType { Poison, Burning, Frozen, Stunned, Bleeding }
    }
}