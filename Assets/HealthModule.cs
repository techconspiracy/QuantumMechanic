// USER EDITABLE FILE - Add your custom logic here

using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace RPG.Modules
{
    /// <summary>
    /// Custom logic for HealthModule with Voronoi-based destruction on death.
    /// This partial class extends the generated base WITHOUT redeclaring interface methods.
    /// Instead, we add helper methods and hook into Unity lifecycle events.
    /// </summary>
    public partial class HealthModule
    {
        [Header("Health Configuration")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _startingHealth = 100f;
        [SerializeField] private bool _canRegenerate = false;
        [SerializeField] private float _regenerationRate = 5f;
        [SerializeField] private float _regenerationDelay = 3f;
        
        [Header("Voronoi Destruction Settings")]
        [SerializeField] private int _voronoiSites = 15;
        [SerializeField] private float _explosionForce = 300f;
        [SerializeField] private float _explosionRadius = 5f;
        [SerializeField] private float _fragmentLifetime = 5f;
        [SerializeField] private Material _fragmentMaterial;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _deathVFX;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioClip _damageSound;
        
        private float _timeSinceLastDamage;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private AudioSource _audioSource;
        private bool _hasProcessedDeath;

        private void Awake()
        {
            // Cache components
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsServer)
            {
                // Initialize NetworkVariables
                _currentValue.Value = _startingHealth;
                _maxValue.Value = _maxHealth;
                _isDead.Value = false;
            }

            _timeSinceLastDamage = 0f;
            _hasProcessedDeath = false;
            
            // Subscribe to death state changes
            _isDead.OnValueChanged += OnDeathStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            _isDead.OnValueChanged -= OnDeathStateChanged;
            base.OnNetworkDespawn();
        }

        private void OnDeathStateChanged(bool previousValue, bool newValue)
        {
            if (newValue && !_hasProcessedDeath)
            {
                _hasProcessedDeath = true;
                TriggerVoronoiDestructionClientRpc();
            }
        }

        private void Update()
        {
            if (!IsServer || _isDead.Value) return;

            // Handle health regeneration
            if (_canRegenerate && _currentValue.Value < _maxValue.Value)
            {
                _timeSinceLastDamage += Time.deltaTime;

                if (_timeSinceLastDamage >= _regenerationDelay)
                {
                    // Use the base ModifyResource from generated file
                    float delta = _regenerationRate * Time.deltaTime;
                    _currentValue.Value = Mathf.Clamp(_currentValue.Value + delta, 0, _maxValue.Value);
                }
            }
        }

        // Hook that gets called by external systems (like combat)
        public void ApplyDamage(float amount, ulong attackerId)
        {
            if (!IsServer || _isDead.Value) return;

            _timeSinceLastDamage = 0f;
            
            // Modify health
            float newHealth = Mathf.Clamp(_currentValue.Value - amount, 0, _maxValue.Value);
            _currentValue.Value = newHealth;

            // Broadcast damage feedback
            PlayDamageFeedbackClientRpc();

            // Check for death
            if (newHealth <= 0 && !_isDead.Value)
            {
                _isDead.Value = true;
                Debug.Log($"[Server] Entity destroyed by client {attackerId}");
            }
        }

        [ClientRpc]
        private void PlayDamageFeedbackClientRpc()
        {
            if (_damageSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_damageSound);
            }
        }

        [ClientRpc]
        private void TriggerVoronoiDestructionClientRpc()
        {
            if (_deathVFX != null)
            {
                Instantiate(_deathVFX, transform.position, transform.rotation);
            }

            if (_deathSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_deathSound);
            }

            // Perform Voronoi destruction
            if (_meshFilter != null && _meshFilter.mesh != null)
            {
                PerformVoronoiDestruction();
            }

            // Hide original mesh
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }

            // Cleanup after fragments despawn
            if (IsServer)
            {
                Destroy(gameObject, _fragmentLifetime + 1f);
            }
        }

        private void PerformVoronoiDestruction()
        {
            Mesh originalMesh = _meshFilter.mesh;
            Vector3[] vertices = originalMesh.vertices;
            int[] triangles = originalMesh.triangles;
            Vector3[] normals = originalMesh.normals;
            Vector2[] uvs = originalMesh.uv;

            // Generate random Voronoi sites within mesh bounds
            Bounds bounds = originalMesh.bounds;
            List<Vector3> voronoiSites = GenerateVoronoiSites(bounds, _voronoiSites);

            // Group triangles by closest Voronoi site
            Dictionary<int, List<int>> siteTriangles = new Dictionary<int, List<int>>();
            for (int i = 0; i < voronoiSites.Count; i++)
            {
                siteTriangles[i] = new List<int>();
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 triangleCenter = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                int closestSite = FindClosestSite(triangleCenter, voronoiSites);
                siteTriangles[closestSite].Add(i);
            }

            // Create fragment GameObject for each Voronoi region
            Vector3 explosionCenter = transform.position;

            foreach (var kvp in siteTriangles)
            {
                if (kvp.Value.Count == 0) continue;

                CreateFragment(kvp.Key, kvp.Value, vertices, triangles, normals, uvs, voronoiSites, explosionCenter);
            }
        }

        private List<Vector3> GenerateVoronoiSites(Bounds bounds, int count)
        {
            List<Vector3> sites = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                Vector3 site = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)
                );
                sites.Add(site);
            }
            return sites;
        }

        private int FindClosestSite(Vector3 point, List<Vector3> sites)
        {
            int closest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < sites.Count; i++)
            {
                float dist = Vector3.SqrMagnitude(point - sites[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = i;
                }
            }

            return closest;
        }

        private void CreateFragment(int siteIndex, List<int> triangleIndices, Vector3[] vertices, int[] triangles, 
                                     Vector3[] normals, Vector2[] uvs, List<Vector3> voronoiSites, Vector3 explosionCenter)
        {
            // Create new mesh for fragment
            Mesh fragmentMesh = new Mesh();
            List<Vector3> fragVerts = new List<Vector3>();
            List<int> fragTris = new List<int>();
            List<Vector3> fragNormals = new List<Vector3>();
            List<Vector2> fragUVs = new List<Vector2>();
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            foreach (int triIndex in triangleIndices)
            {
                for (int i = 0; i < 3; i++)
                {
                    int vertIndex = triangles[triIndex + i];
                    
                    if (!vertexMap.ContainsKey(vertIndex))
                    {
                        vertexMap[vertIndex] = fragVerts.Count;
                        fragVerts.Add(vertices[vertIndex]);
                        fragNormals.Add(normals.Length > vertIndex ? normals[vertIndex] : Vector3.up);
                        fragUVs.Add(uvs.Length > vertIndex ? uvs[vertIndex] : Vector2.zero);
                    }

                    fragTris.Add(vertexMap[vertIndex]);
                }
            }

            fragmentMesh.vertices = fragVerts.ToArray();
            fragmentMesh.triangles = fragTris.ToArray();
            fragmentMesh.normals = fragNormals.ToArray();
            fragmentMesh.uv = fragUVs.ToArray();
            fragmentMesh.RecalculateBounds();

            // Create GameObject for fragment
            GameObject fragment = new GameObject($"Fragment_{siteIndex}");
            fragment.transform.position = transform.TransformPoint(voronoiSites[siteIndex]);
            fragment.transform.rotation = transform.rotation;
            fragment.transform.localScale = transform.localScale;

            MeshFilter mf = fragment.AddComponent<MeshFilter>();
            mf.mesh = fragmentMesh;

            MeshRenderer mr = fragment.AddComponent<MeshRenderer>();
            mr.material = _fragmentMaterial != null ? _fragmentMaterial : _meshRenderer.material;

            MeshCollider mc = fragment.AddComponent<MeshCollider>();
            mc.convex = true;

            Rigidbody rb = fragment.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;

            // Apply explosion force
            Vector3 explosionDir = (fragment.transform.position - explosionCenter).normalized;
            float randomForce = Random.Range(0.7f, 1.3f) * _explosionForce;
            rb.AddForce(explosionDir * randomForce + Vector3.up * (randomForce * 0.3f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * randomForce * 0.1f, ForceMode.Impulse);

            // Schedule destruction
            Destroy(fragment, _fragmentLifetime);
        }
    }
}