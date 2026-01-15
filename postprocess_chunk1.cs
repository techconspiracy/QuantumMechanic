using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace QuantumMechanic.Rendering
{
    /// <summary>
    /// Manages all post-processing effects and screen space visual enhancements
    /// </summary>
    public class PostProcessManager : MonoBehaviour
    {
        private static PostProcessManager instance;
        public static PostProcessManager Instance => instance;

        [Header("Volume Settings")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private VolumeProfile defaultProfile;
        [SerializeField] private float transitionSpeed = 2f;

        [Header("Effect Components")]
        private Bloom bloom;
        private ColorAdjustments colorAdjustments;
        private Vignette vignette;
        private ChromaticAberration chromaticAberration;
        private LensDistortion lensDistortion;
        private FilmGrain filmGrain;

        private Dictionary<string, VolumeProfile> effectPresets = new Dictionary<string, VolumeProfile>();
        private List<EffectStack> activeEffects = new List<EffectStack>();
        private float currentBlendWeight = 0f;

        private class EffectStack
        {
            public string id;
            public float intensity;
            public int priority;
            public float duration;
            public float elapsed;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            InitializeVolume();
            LoadEffectComponents();
            CreateDefaultPresets();
        }

        /// <summary>
        /// Initialize the global volume and profile
        /// </summary>
        private void InitializeVolume()
        {
            if (globalVolume == null)
            {
                GameObject volumeObj = new GameObject("GlobalVolume");
                volumeObj.transform.SetParent(transform);
                globalVolume = volumeObj.AddComponent<Volume>();
                globalVolume.isGlobal = true;
                globalVolume.priority = 0;
            }

            if (defaultProfile == null)
            {
                defaultProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            globalVolume.profile = defaultProfile;
        }

        /// <summary>
        /// Load all post-processing effect components
        /// </summary>
        private void LoadEffectComponents()
        {
            if (globalVolume.profile.TryGet(out bloom)) { }
            else { bloom = globalVolume.profile.Add<Bloom>(); }

            if (globalVolume.profile.TryGet(out colorAdjustments)) { }
            else { colorAdjustments = globalVolume.profile.Add<ColorAdjustments>(); }

            if (globalVolume.profile.TryGet(out vignette)) { }
            else { vignette = globalVolume.profile.Add<Vignette>(); }

            if (globalVolume.profile.TryGet(out chromaticAberration)) { }
            else { chromaticAberration = globalVolume.profile.Add<ChromaticAberration>(); }

            if (globalVolume.profile.TryGet(out lensDistortion)) { }
            else { lensDistortion = globalVolume.profile.Add<LensDistortion>(); }

            if (globalVolume.profile.TryGet(out filmGrain)) { }
            else { filmGrain = globalVolume.profile.Add<FilmGrain>(); }

            ResetAllEffects();
        }

        /// <summary>
        /// Reset all effects to default values
        /// </summary>
        public void ResetAllEffects()
        {
            bloom.active = false;
            colorAdjustments.active = false;
            vignette.active = false;
            chromaticAberration.active = false;
            lensDistortion.active = false;
            filmGrain.active = false;
        }

        /// <summary>
        /// Apply bloom/glow effect
        /// </summary>
        public void SetBloom(float intensity, float threshold = 0.9f, float scatter = 0.7f)
        {
            bloom.active = intensity > 0f;
            bloom.intensity.value = intensity;
            bloom.threshold.value = threshold;
            bloom.scatter.value = scatter;
        }

        /// <summary>
        /// Apply color grading adjustments
        /// </summary>
        public void ApplyColorGrade(float temperature = 0f, float tint = 0f, float saturation = 0f, float contrast = 0f)
        {
            colorAdjustments.active = true;
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.contrast.value = contrast;
            colorAdjustments.colorFilter.value = Color.white;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.saturation.value = saturation;
        }

        /// <summary>
        /// Apply vignette darkening at screen edges
        /// </summary>
        public void SetVignette(float intensity, float smoothness = 0.4f, Color? color = null)
        {
            vignette.active = intensity > 0f;
            vignette.intensity.value = intensity;
            vignette.smoothness.value = smoothness;
            vignette.color.value = color ?? Color.black;
        }

        /// <summary>
        /// Apply chromatic aberration (color fringing)
        /// </summary>
        public void SetChromaticAberration(float intensity)
        {
            chromaticAberration.active = intensity > 0f;
            chromaticAberration.intensity.value = intensity;
        }

        /// <summary>
        /// Apply lens distortion effect
        /// </summary>
        public void SetLensDistortion(float intensity, float scale = 1f)
        {
            lensDistortion.active = Mathf.Abs(intensity) > 0.01f;
            lensDistortion.intensity.value = intensity;
            lensDistortion.scale.value = scale;
        }

        /// <summary>
        /// Apply film grain effect
        /// </summary>
        public void SetFilmGrain(float intensity, float response = 0.8f)
        {
            filmGrain.active = intensity > 0f;
            filmGrain.intensity.value = intensity;
            filmGrain.response.value = response;
        }

        /// <summary>
        /// Create preset effect configurations
        /// </summary>
        private void CreateDefaultPresets()
        {
            // Cinematic preset
            CreatePreset("Cinematic", () =>
            {
                SetBloom(0.3f, 0.8f, 0.7f);
                ApplyColorGrade(saturation: 10f, contrast: 15f);
                SetVignette(0.35f, 0.3f);
                SetFilmGrain(0.15f);
            });

            // Horror preset
            CreatePreset("Horror", () =>
            {
                ApplyColorGrade(saturation: -20f, contrast: 20f);
                SetVignette(0.5f, 0.2f, new Color(0.1f, 0f, 0f));
                SetChromaticAberration(0.3f);
                SetFilmGrain(0.3f);
            });

            // Dreamlike preset
            CreatePreset("Dreamlike", () =>
            {
                SetBloom(0.6f, 0.7f, 0.9f);
                ApplyColorGrade(saturation: 30f, contrast: -10f);
                SetVignette(0.2f, 0.5f);
            });

            // Retro preset
            CreatePreset("Retro", () =>
            {
                ApplyColorGrade(saturation: -10f, contrast: 25f);
                SetVignette(0.4f, 0.3f);
                SetFilmGrain(0.4f, 0.6f);
                SetChromaticAberration(0.2f);
            });
        }

        /// <summary>
        /// Create a named effect preset
        /// </summary>
        private void CreatePreset(string name, System.Action setupAction)
        {
            ResetAllEffects();
            setupAction?.Invoke();
        }

        /// <summary>
        /// Apply a named preset
        /// </summary>
        public void ApplyPreset(string presetName)
        {
            CreateDefaultPresets();
        }
    }
}