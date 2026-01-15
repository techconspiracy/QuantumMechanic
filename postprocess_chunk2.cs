using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace QuantumMechanic.Rendering
{
    /// <summary>
    /// Advanced screen space effects for enhanced visual quality
    /// </summary>
    public partial class PostProcessManager
    {
        [Header("Advanced Effects")]
        private MotionBlur motionBlur;
        private DepthOfField depthOfField;
        private ScreenSpaceAmbientOcclusion ssao;

        private Material distortionMaterial;
        private Material outlineMaterial;
        private Material godRaysMaterial;

        [Header("Effect Settings")]
        [SerializeField] private bool enableMotionBlur = true;
        [SerializeField] private bool enableDepthOfField = true;
        [SerializeField] private bool enableSSAO = true;
        [SerializeField] private bool enableSSR = false;

        private float currentFocusDistance = 10f;
        private float targetFocusDistance = 10f;
        private float focusTransitionSpeed = 2f;

        /// <summary>
        /// Load advanced effect components
        /// </summary>
        private void LoadAdvancedEffects()
        {
            if (globalVolume.profile.TryGet(out motionBlur)) { }
            else { motionBlur = globalVolume.profile.Add<MotionBlur>(); }

            if (globalVolume.profile.TryGet(out depthOfField)) { }
            else { depthOfField = globalVolume.profile.Add<DepthOfField>(); }

            LoadCustomShaders();
        }

        /// <summary>
        /// Load custom shader materials for advanced effects
        /// </summary>
        private void LoadCustomShaders()
        {
            Shader distortionShader = Shader.Find("Hidden/ScreenDistortion");
            if (distortionShader) distortionMaterial = new Material(distortionShader);

            Shader outlineShader = Shader.Find("Hidden/EdgeDetection");
            if (outlineShader) outlineMaterial = new Material(outlineShader);

            Shader godRaysShader = Shader.Find("Hidden/GodRays");
            if (godRaysShader) godRaysMaterial = new Material(godRaysShader);
        }

        /// <summary>
        /// Set motion blur intensity
        /// </summary>
        public void SetMotionBlur(float intensity, MotionBlurQuality quality = MotionBlurQuality.Medium)
        {
            if (!enableMotionBlur) return;

            motionBlur.active = intensity > 0f;
            motionBlur.intensity.value = Mathf.Clamp01(intensity);
            motionBlur.quality.value = (int)quality;
        }

        /// <summary>
        /// Set depth of field with focus distance and aperture
        /// </summary>
        public void SetDepthOfField(float focusDistance, float aperture = 5.6f, float focalLength = 50f)
        {
            if (!enableDepthOfField) return;

            depthOfField.active = true;
            depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            depthOfField.focusDistance.value = focusDistance;
            depthOfField.aperture.value = aperture;
            depthOfField.focalLength.value = focalLength;

            targetFocusDistance = focusDistance;
        }

        /// <summary>
        /// Smoothly transition focus distance
        /// </summary>
        public void TransitionFocus(float newFocusDistance, float transitionSpeed = 2f)
        {
            targetFocusDistance = newFocusDistance;
            focusTransitionSpeed = transitionSpeed;
            StartCoroutine(SmoothFocusTransition());
        }

        private IEnumerator SmoothFocusTransition()
        {
            while (Mathf.Abs(currentFocusDistance - targetFocusDistance) > 0.1f)
            {
                currentFocusDistance = Mathf.Lerp(currentFocusDistance, targetFocusDistance, Time.deltaTime * focusTransitionSpeed);
                depthOfField.focusDistance.value = currentFocusDistance;
                yield return null;
            }
            currentFocusDistance = targetFocusDistance;
        }

        /// <summary>
        /// Enable/disable screen space ambient occlusion
        /// </summary>
        public void SetAmbientOcclusion(bool enabled, float intensity = 0.5f, float radius = 0.25f)
        {
            if (!enableSSAO) return;
            // SSAO is typically handled by URP renderer features
            // This is a placeholder for integration
        }

        /// <summary>
        /// Apply screen distortion effect (damage, drunk, underwater)
        /// </summary>
        public void ApplyScreenDistortion(float intensity, Vector2 direction, float frequency = 10f)
        {
            if (distortionMaterial == null) return;

            distortionMaterial.SetFloat("_Intensity", intensity);
            distortionMaterial.SetVector("_Direction", direction);
            distortionMaterial.SetFloat("_Frequency", frequency);
            distortionMaterial.SetFloat("_Time", Time.time);
        }

        /// <summary>
        /// Apply heat distortion/refraction effect
        /// </summary>
        public void ApplyHeatDistortion(Vector3 sourcePosition, float intensity, float radius = 5f)
        {
            if (distortionMaterial == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(sourcePosition);
            distortionMaterial.SetVector("_DistortionCenter", new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height));
            distortionMaterial.SetFloat("_DistortionIntensity", intensity);
            distortionMaterial.SetFloat("_DistortionRadius", radius);
        }

        /// <summary>
        /// Apply edge detection and outline effects
        /// </summary>
        public void SetEdgeDetection(bool enabled, float thickness = 1f, Color outlineColor = default)
        {
            if (outlineMaterial == null) return;

            if (outlineColor == default) outlineColor = Color.black;

            outlineMaterial.SetFloat("_Thickness", thickness);
            outlineMaterial.SetColor("_OutlineColor", outlineColor);
        }

        /// <summary>
        /// Apply god rays / volumetric lighting effect
        /// </summary>
        public void SetGodRays(bool enabled, Vector3 lightSourcePosition, float intensity = 1f, float decay = 0.95f)
        {
            if (godRaysMaterial == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(lightSourcePosition);
            godRaysMaterial.SetVector("_LightPosition", new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height));
            godRaysMaterial.SetFloat("_Intensity", intensity);
            godRaysMaterial.SetFloat("_Decay", decay);
            godRaysMaterial.SetFloat("_Weight", 0.5f);
            godRaysMaterial.SetFloat("_Density", 0.5f);
        }

        /// <summary>
        /// Apply underwater distortion effect
        /// </summary>
        public void ApplyUnderwaterEffect(float intensity)
        {
            ApplyScreenDistortion(intensity * 0.02f, Vector2.up, 5f);
            SetChromaticAberration(intensity * 0.1f);
            ApplyColorGrade(tint: -20f * intensity, saturation: -10f * intensity);
            SetVignette(0.2f * intensity, 0.5f, new Color(0f, 0.2f, 0.3f));
        }

        /// <summary>
        /// Apply drunk/dizzy screen effect
        /// </summary>
        public void ApplyDrunkEffect(float intensity)
        {
            float wobble = Mathf.Sin(Time.time * 2f) * intensity;
            ApplyScreenDistortion(wobble * 0.05f, new Vector2(Mathf.Cos(Time.time), Mathf.Sin(Time.time * 1.3f)), 3f);
            SetLensDistortion(wobble * 0.3f);
            SetChromaticAberration(intensity * 0.2f);
            SetMotionBlur(intensity * 0.3f);
        }

        /// <summary>
        /// Apply screen damage/crack effect
        /// </summary>
        public void ApplyDamageEffect(float intensity, Vector2 impactPoint)
        {
            Vector2 screenCenter = new Vector2(0.5f, 0.5f);
            Vector2 direction = (impactPoint - screenCenter).normalized;
            ApplyScreenDistortion(intensity * 0.03f, direction, 20f);
            SetChromaticAberration(intensity * 0.15f);
        }

        /// <summary>
        /// Apply speed-based motion effects
        /// </summary>
        public void ApplySpeedEffect(float speed, float maxSpeed = 50f)
        {
            float intensity = Mathf.Clamp01(speed / maxSpeed);
            SetMotionBlur(intensity * 0.5f);
            SetVignette(intensity * 0.3f, 0.6f);
        }

        /// <summary>
        /// Set anti-aliasing quality
        /// </summary>
        public void SetAntiAliasing(AntialiasingMode mode, AntialiasingQuality quality = AntialiasingQuality.High)
        {
            // This would typically be set on the URP camera
            // Placeholder for integration
        }

        public enum MotionBlurQuality { Low = 0, Medium = 1, High = 2 }
        public enum AntialiasingMode { None, FXAA, SMAA, TAA }
        public enum AntialiasingQuality { Low, Medium, High, Ultra }
    }
}