using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.VFX
{
    /// <summary>
    /// Camera shake effects for impacts and explosions
    /// </summary>
    public class CameraShakeManager : MonoBehaviour
    {
        public static CameraShakeManager Instance { get; private set; }

        [SerializeField] private float maxShakeIntensity = 1f;
        [SerializeField] private AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

        private Camera mainCamera;
        private Vector3 originalPosition;
        private Coroutine shakeCoroutine;

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera != null)
                originalPosition = mainCamera.transform.localPosition;
        }

        /// <summary>
        /// Shake camera with intensity and duration
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        private IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            float elapsed = 0f;
            intensity = Mathf.Min(intensity, maxShakeIntensity);

            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float currentIntensity = intensity * shakeFalloff.Evaluate(progress);

                Vector3 offset = UnityEngine.Random.insideUnitSphere * currentIntensity;
                mainCamera.transform.localPosition = originalPosition + offset;

                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCamera.transform.localPosition = originalPosition;
        }
    }

    /// <summary>
    /// Flash effects for damage, invincibility, etc.
    /// </summary>
    public class FlashEffect : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;

        private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>();

            CacheOriginalColors();
        }

        private void CacheOriginalColors()
        {
            foreach (var renderer in renderers)
            {
                Material[] materials = renderer.materials;
                Color[] colors = new Color[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    colors[i] = materials[i].color;
                }
                originalColors[renderer] = colors;
            }
        }

        /// <summary>
        /// Flash renderers with color
        /// </summary>
        public void Flash()
        {
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            SetFlashColor();
            yield return new WaitForSeconds(flashDuration);
            RestoreOriginalColors();
        }

        private void SetFlashColor()
        {
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    material.color = flashColor;
                }
            }
        }

        private void RestoreOriginalColors()
        {
            foreach (var kvp in originalColors)
            {
                Material[] materials = kvp.Key.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i].color = kvp.Value[i];
                }
            }
        }
    }

    /// <summary>
    /// Decal system for bullet holes, footprints, blood
    /// </summary>
    public class DecalSystem : MonoBehaviour
    {
        public static DecalSystem Instance { get; private set; }

        [SerializeField] private GameObject bulletHolePrefab;
        [SerializeField] private GameObject bloodDecalPrefab;
        [SerializeField] private float decalLifetime = 30f;
        [SerializeField] private int maxDecals = 50;

        private Queue<GameObject> activeDecals = new Queue<GameObject>();

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Spawn decal on surface
        /// </summary>
        public void SpawnDecal(DecalType type, Vector3 position, Vector3 normal)
        {
            GameObject prefab = type == DecalType.BulletHole ? bulletHolePrefab : bloodDecalPrefab;
            if (prefab == null) return;

            if (activeDecals.Count >= maxDecals)
            {
                GameObject oldest = activeDecals.Dequeue();
                Destroy(oldest);
            }

            Quaternion rotation = Quaternion.LookRotation(normal);
            GameObject decal = Instantiate(prefab, position, rotation);
            activeDecals.Enqueue(decal);
            Destroy(decal, decalLifetime);
        }
    }

    /// <summary>
    /// Procedural lightning generator
    /// </summary>
    public class LightningGenerator : MonoBehaviour
    {
        [SerializeField] private LineRenderer lightningPrefab;
        [SerializeField] private int segments = 10;
        [SerializeField] private float displacement = 0.5f;

        /// <summary>
        /// Generate lightning bolt between two points
        /// </summary>
        public LineRenderer GenerateLightning(Vector3 start, Vector3 end, float duration = 0.2f)
        {
            LineRenderer lightning = Instantiate(lightningPrefab);
            lightning.positionCount = segments + 1;

            Vector3[] positions = new Vector3[segments + 1];
            positions[0] = start;
            positions[segments] = end;

            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector3 basePos = Vector3.Lerp(start, end, t);
                Vector3 offset = UnityEngine.Random.insideUnitSphere * displacement;
                positions[i] = basePos + offset;
            }

            lightning.SetPositions(positions);
            Destroy(lightning.gameObject, duration);
            return lightning;
        }
    }

    /// <summary>
    /// Material property animation (glow, dissolve, fade)
    /// </summary>
    public class MaterialAnimator : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string propertyName = "_Emission";

        /// <summary>
        /// Animate material property over time
        /// </summary>
        public void AnimateProperty(float from, float to, float duration, Action onComplete = null)
        {
            StartCoroutine(AnimateCoroutine(from, to, duration, onComplete));
        }

        private IEnumerator AnimateCoroutine(float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            Material mat = targetRenderer.material;

            while (elapsed < duration)
            {
                float value = Mathf.Lerp(from, to, elapsed / duration);
                mat.SetFloat(propertyName, value);
                elapsed += Time.deltaTime;
                yield return null;
            }

            mat.SetFloat(propertyName, to);
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// VFX sequencer for timed multi-effect combos
    /// </summary>
    public class VFXSequencer : MonoBehaviour
    {
        /// <summary>
        /// Play sequence of effects with delays
        /// </summary>
        public void PlaySequence(VFXSequenceData sequence, Vector3 position)
        {
            StartCoroutine(SequenceCoroutine(sequence, position));
        }

        private IEnumerator SequenceCoroutine(VFXSequenceData sequence, Vector3 position)
        {
            foreach (var step in sequence.Steps)
            {
                yield return new WaitForSeconds(step.Delay);
                ParticleManager.Instance.SpawnEffect(step.EffectName, position + step.Offset);
                step.OnExecute?.Invoke();
            }

            sequence.OnComplete?.Invoke();
        }
    }

    /// <summary>
    /// Custom particle behaviors (homing, orbit, spiral)
    /// </summary>
    public class CustomParticleBehavior : MonoBehaviour
    {
        [SerializeField] private BehaviorType behavior = BehaviorType.Homing;
        [SerializeField] private Transform target;
        [SerializeField] private float speed = 5f;
        [SerializeField] private float orbitRadius = 2f;
        [SerializeField] private float orbitSpeed = 2f;

        private float angle = 0f;

        private void Update()
        {
            switch (behavior)
            {
                case BehaviorType.Homing:
                    UpdateHoming();
                    break;
                case BehaviorType.Orbit:
                    UpdateOrbit();
                    break;
                case BehaviorType.Spiral:
                    UpdateSpiral();
                    break;
            }
        }

        private void UpdateHoming()
        {
            if (target == null) return;
            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;
        }

        private void UpdateOrbit()
        {
            if (target == null) return;
            angle += orbitSpeed * Time.deltaTime;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * orbitRadius;
            transform.position = target.position + offset;
        }

        private void UpdateSpiral()
        {
            if (target == null) return;
            angle += orbitSpeed * Time.deltaTime;
            orbitRadius += speed * Time.deltaTime;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * orbitRadius;
            transform.position = target.position + offset;
        }
    }

    /// <summary>
    /// VFX sequence data for multi-effect combos
    /// </summary>
    [System.Serializable]
    public class VFXSequenceData
    {
        public VFXSequenceStep[] Steps;
        public Action OnComplete;
    }

    [System.Serializable]
    public class VFXSequenceStep
    {
        public string EffectName;
        public float Delay;
        public Vector3 Offset;
        public Action OnExecute;
    }

    public enum DecalType { BulletHole, Blood, Footprint, Scorch }
    public enum BehaviorType { Homing, Orbit, Spiral }
}