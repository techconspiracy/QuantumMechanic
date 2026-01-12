// MODULE: Visual-01
// FILE: ProceduralModelFactory.cs
// DEPENDENCIES: UnityEngine, System.Collections.Generic
// INTEGRATES WITH: ProjectBootstrapper (prefab generation), Future character/weapon systems
// PURPOSE: Parametric mesh generation for infinite visual content variety

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace QuantumMechanic.Visual
{
    /// <summary>
    /// Defines the type of model to generate
    /// </summary>
    public enum ModelType
    {
        Humanoid,
        Weapon,
        Armor,
        Creature,
        Environmental
    }

    /// <summary>
    /// Defines the visual style/aesthetic for model generation
    /// </summary>
    public enum ModelStyle
    {
        Cyberpunk,
        Fantasy,
        Organic,
        Mechanical
    }

    /// <summary>
    /// Request object for procedural model generation
    /// </summary>
    public class ModelRequest
    {
        public ModelType Type;
        public ModelStyle Style;
        public float ScaleFactor = 1.0f;
        public Dictionary<string, float> Parameters;
        public string SubType; // e.g., "Sword", "Helmet", "Orc"

        public ModelRequest()
        {
            Parameters = new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Factory class for generating procedural 3D models without external assets.
    /// Provides parametric mesh generation for characters, weapons, armor, and creatures
    /// with multiple style variations.
    /// </summary>
    public static class ProceduralModelFactory
    {
        private const string MATERIAL_PATH = "Materials/";

        /// <summary>
        /// Main entry point for generating complete GameObjects with procedural meshes and materials.
        /// </summary>
        /// <param name="request">Model request specifying type, style, and parameters</param>
        /// <returns>GameObject with MeshFilter, MeshRenderer, and generated mesh</returns>
        public static GameObject GenerateModel(ModelRequest request)
        {
            GameObject obj = new GameObject($"Generated_{request.Type}_{request.SubType}");
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();

            Mesh generatedMesh = null;

            switch (request.Type)
            {
                case ModelType.Humanoid:
                    generatedMesh = GenerateHumanoidMesh(request.Parameters, request.Style);
                    break;
                case ModelType.Weapon:
                    generatedMesh = GenerateWeaponMesh(request.SubType, request.Parameters, request.Style);
                    break;
                case ModelType.Armor:
                    generatedMesh = GenerateArmorMesh(request.SubType, request.Parameters, request.Style);
                    break;
                case ModelType.Creature:
                    generatedMesh = GenerateCreatureMesh(request.SubType, request.Parameters, request.Style);
                    break;
                case ModelType.Environmental:
                    generatedMesh = GenerateEnvironmentalMesh(request.SubType, request.Parameters, request.Style);
                    break;
            }

            if (generatedMesh != null)
            {
                filter.mesh = generatedMesh;
                renderer.material = GetStyleMaterial(request.Style);
                obj.transform.localScale = Vector3.one * request.ScaleFactor;
            }

            return obj;
        }

        /// <summary>
        /// Generates a low-poly humanoid character mesh with parametric body proportions.
        /// </summary>
        /// <param name="parameters">Dictionary containing height, bulk, musculature values</param>
        /// <param name="style">Visual style affecting vertex positioning and proportions</param>
        /// <returns>Complete humanoid mesh with torso, head, arms, and legs</returns>
        public static Mesh GenerateHumanoidMesh(Dictionary<string, float> parameters, ModelStyle style)
        {
            Mesh mesh = new Mesh();
            mesh.name = "ProceduralHumanoid";

            float height = GetParameter(parameters, "height", 2.0f);
            float bulk = GetParameter(parameters, "bulk", 1.0f);
            float musculature = GetParameter(parameters, "musculature", 0.5f);

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            // Style-specific adjustments
            float angularity = style == ModelStyle.Cyberpunk || style == ModelStyle.Mechanical ? 1.2f : 0.8f;
            float smoothness = style == ModelStyle.Organic ? 1.3f : 1.0f;

            // Generate torso (box shape)
            int torsoStart = vertices.Count;
            float torsoWidth = 0.5f * bulk * angularity;
            float torsoDepth = 0.3f * bulk;
            float torsoHeight = height * 0.4f;

            AddBox(vertices, triangles, uvs, colors, 
                   Vector3.zero, 
                   new Vector3(torsoWidth, torsoHeight, torsoDepth),
                   GetStyleColor(style, 0.8f));

            // Generate head (sphere approximation)
            int headStart = vertices.Count;
            float headRadius = 0.25f * bulk * smoothness;
            Vector3 headPos = new Vector3(0, height * 0.5f + headRadius, 0);
            AddSphere(vertices, triangles, uvs, colors, headPos, headRadius, 8, GetStyleColor(style, 1.0f));

            // Generate arms (cylinders)
            float armLength = height * 0.35f;
            float armRadius = 0.1f * bulk * (1.0f + musculature * 0.3f);
            Vector3 leftArmPos = new Vector3(-torsoWidth - armRadius, height * 0.3f, 0);
            Vector3 rightArmPos = new Vector3(torsoWidth + armRadius, height * 0.3f, 0);

            AddCylinder(vertices, triangles, uvs, colors, leftArmPos, armRadius, armLength, 8, GetStyleColor(style, 0.7f));
            AddCylinder(vertices, triangles, uvs, colors, rightArmPos, armRadius, armLength, 8, GetStyleColor(style, 0.7f));

            // Generate legs (cylinders)
            float legLength = height * 0.45f;
            float legRadius = 0.12f * bulk * (1.0f + musculature * 0.2f);
            Vector3 leftLegPos = new Vector3(-torsoWidth * 0.4f, -legLength * 0.5f, 0);
            Vector3 rightLegPos = new Vector3(torsoWidth * 0.4f, -legLength * 0.5f, 0);

            AddCylinder(vertices, triangles, uvs, colors, leftLegPos, legRadius, legLength, 8, GetStyleColor(style, 0.6f));
            AddCylinder(vertices, triangles, uvs, colors, rightLegPos, legRadius, legLength, 8, GetStyleColor(style, 0.6f));

            AssignMeshData(mesh, vertices, triangles, uvs, colors);
            return mesh;
        }

        /// <summary>
        /// Generates weapon meshes based on type (melee, ranged, energy).
        /// </summary>
        /// <param name="weaponType">Weapon subtype like "Sword", "Bow", "Plasma"</param>
        /// <param name="parameters">Dictionary containing length, sharpness, curve values</param>
        /// <param name="style">Visual style affecting weapon aesthetics</param>
        /// <returns>Complete weapon mesh</returns>
        public static Mesh GenerateWeaponMesh(string weaponType, Dictionary<string, float> parameters, ModelStyle style)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"Procedural_{weaponType}";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            if (weaponType == "Sword" || weaponType == null)
            {
                GenerateSword(vertices, triangles, uvs, colors, parameters, style);
            }
            else if (weaponType == "Axe")
            {
                GenerateAxe(vertices, triangles, uvs, colors, parameters, style);
            }
            else if (weaponType == "Bow")
            {
                GenerateBow(vertices, triangles, uvs, colors, parameters, style);
            }
            else if (weaponType == "Plasma")
            {
                GeneratePlasmaWeapon(vertices, triangles, uvs, colors, parameters, style);
            }

            AssignMeshData(mesh, vertices, triangles, uvs, colors);
            return mesh;
        }

        /// <summary>
        /// Generates armor piece meshes (helmets, chest plates, boots).
        /// </summary>
        /// <param name="armorType">Armor subtype like "Helmet", "Chest", "Boots"</param>
        /// <param name="parameters">Dictionary containing coverage, thickness values</param>
        /// <param name="style">Visual style affecting armor design</param>
        /// <returns>Complete armor mesh</returns>
        public static Mesh GenerateArmorMesh(string armorType, Dictionary<string, float> parameters, ModelStyle style)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"Procedural_{armorType}";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            float thickness = GetParameter(parameters, "thickness", 0.1f);
            Color armorColor = GetStyleColor(style, 0.9f);

            if (armorType == "Helmet" || armorType == null)
            {
                AddSphere(vertices, triangles, uvs, colors, Vector3.zero, 0.3f, 12, armorColor);
            }
            else if (armorType == "Chest")
            {
                AddBox(vertices, triangles, uvs, colors, Vector3.zero, new Vector3(0.6f, 0.8f, 0.4f), armorColor);
            }
            else if (armorType == "Boots")
            {
                AddBox(vertices, triangles, uvs, colors, Vector3.zero, new Vector3(0.15f, 0.3f, 0.25f), armorColor);
            }

            AssignMeshData(mesh, vertices, triangles, uvs, colors);
            return mesh;
        }

        /// <summary>
        /// Generates creature/enemy meshes with organic or monstrous forms.
        /// </summary>
        /// <param name="creatureType">Creature subtype like "Orc", "Drone", "Beast"</param>
        /// <param name="parameters">Dictionary containing size, aggression values</param>
        /// <param name="style">Visual style affecting creature appearance</param>
        /// <returns>Complete creature mesh</returns>
        public static Mesh GenerateCreatureMesh(string creatureType, Dictionary<string, float> parameters, ModelStyle style)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"Procedural_{creatureType}";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            float size = GetParameter(parameters, "size", 1.5f);
            float aggression = GetParameter(parameters, "aggression", 0.7f);

            // Generate hostile creature body
            AddBox(vertices, triangles, uvs, colors, 
                   new Vector3(0, size * 0.5f, 0), 
                   new Vector3(0.6f * size, size, 0.5f * size),
                   GetStyleColor(style, 0.3f));

            // Add menacing head
            Vector3 headPos = new Vector3(0, size * 1.2f, 0);
            AddSphere(vertices, triangles, uvs, colors, headPos, 0.3f * size * (1.0f + aggression * 0.5f), 10, GetStyleColor(style, 0.5f));

            // Add limbs
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 limbPos = new Vector3(Mathf.Cos(angle) * 0.4f * size, size * 0.3f, Mathf.Sin(angle) * 0.4f * size);
                AddCylinder(vertices, triangles, uvs, colors, limbPos, 0.08f * size, 0.5f * size, 6, GetStyleColor(style, 0.4f));
            }

            AssignMeshData(mesh, vertices, triangles, uvs, colors);
            return mesh;
        }

        /// <summary>
        /// Retrieves the appropriate URP material based on visual style.
        /// </summary>
        /// <param name="style">Visual style to match with material</param>
        /// <returns>URP material instance</returns>
        public static Material GetStyleMaterial(ModelStyle style)
        {
            string matName = style switch
            {
                ModelStyle.Cyberpunk => "PlayerMaterial",
                ModelStyle.Fantasy => "GroundMaterial",
                ModelStyle.Organic => "GroundMaterial",
                ModelStyle.Mechanical => "EnemyMaterial",
                _ => "PlayerMaterial"
            };

            Material mat = Resources.Load<Material>(MATERIAL_PATH + matName);
            if (mat == null)
            {
                Debug.LogWarning($"Material {matName} not found in Resources. Creating default.");
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            return mat;
        }

        // === PRIVATE HELPER METHODS ===

        private static Mesh GenerateEnvironmentalMesh(string envType, Dictionary<string, float> parameters, ModelStyle style)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"Procedural_{envType}";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            // Simple platform/prop generation
            float scale = GetParameter(parameters, "scale", 1.0f);
            AddBox(vertices, triangles, uvs, colors, Vector3.zero, Vector3.one * scale, GetStyleColor(style, 0.5f));

            AssignMeshData(mesh, vertices, triangles, uvs, colors);
            return mesh;
        }

        private static void GenerateSword(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Dictionary<string, float> p, ModelStyle style)
        {
            float length = GetParameter(p, "length", 1.5f);
            float sharpness = GetParameter(p, "sharpness", 0.8f);
            float bladeWidth = 0.1f * (1.0f - sharpness * 0.3f);

            // Blade (flattened box with sharp tip)
            Vector3 bladeBase = new Vector3(0, 0, 0);
            Vector3 bladeTip = new Vector3(0, length, 0);
            AddTaperedBox(v, t, uv, c, bladeBase, bladeTip, bladeWidth, 0.02f, GetStyleColor(style, 1.0f));

            // Hilt (cylinder)
            Vector3 hiltPos = new Vector3(0, -0.15f, 0);
            AddCylinder(v, t, uv, c, hiltPos, 0.05f, 0.3f, 8, GetStyleColor(style, 0.4f));

            // Guard (flat box)
            Vector3 guardPos = new Vector3(0, 0, 0);
            AddBox(v, t, uv, c, guardPos, new Vector3(0.3f, 0.05f, 0.05f), GetStyleColor(style, 0.6f));
        }

        private static void GenerateAxe(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Dictionary<string, float> p, ModelStyle style)
        {
            float length = GetParameter(p, "length", 1.2f);

            // Handle
            AddCylinder(v, t, uv, c, Vector3.zero, 0.05f, length, 8, GetStyleColor(style, 0.3f));

            // Axe head (wedge shape)
            Vector3 headPos = new Vector3(0, length * 0.5f, 0);
            AddBox(v, t, uv, c, headPos, new Vector3(0.4f, 0.15f, 0.1f), GetStyleColor(style, 0.8f));
        }

        private static void GenerateBow(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Dictionary<string, float> p, ModelStyle style)
        {
            float length = GetParameter(p, "length", 1.5f);
            float curve = GetParameter(p, "curve", 0.3f);

            // Bow limbs (curved cylinders approximation)
            for (int i = 0; i < 10; i++)
            {
                float t_val = i / 9.0f;
                float y = (t_val - 0.5f) * length;
                float x = Mathf.Sin(t_val * Mathf.PI) * curve;
                Vector3 pos = new Vector3(x, y, 0);
                AddSphere(v, t, uv, c, pos, 0.03f, 4, GetStyleColor(style, 0.6f));
            }

            // Grip
            AddCylinder(v, t, uv, c, Vector3.zero, 0.05f, 0.3f, 8, GetStyleColor(style, 0.4f));
        }

        private static void GeneratePlasmaWeapon(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Dictionary<string, float> p, ModelStyle style)
        {
            float length = GetParameter(p, "length", 0.8f);

            // Handle/grip
            AddCylinder(v, t, uv, c, new Vector3(0, -0.2f, 0), 0.06f, 0.4f, 8, GetStyleColor(style, 0.3f));

            // Energy chamber
            AddBox(v, t, uv, c, Vector3.zero, new Vector3(0.15f, 0.15f, 0.15f), GetStyleColor(style, 0.9f));

            // Barrel
            AddCylinder(v, t, uv, c, new Vector3(0, length * 0.5f, 0), 0.05f, length, 8, GetStyleColor(style, 0.7f));
        }

        private static void AddBox(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Vector3 center, Vector3 size, Color color)
        {
            int startIdx = v.Count;
            Vector3 hs = size * 0.5f; // Half size

            // 8 vertices of a box
            v.Add(center + new Vector3(-hs.x, -hs.y, -hs.z));
            v.Add(center + new Vector3(hs.x, -hs.y, -hs.z));
            v.Add(center + new Vector3(hs.x, -hs.y, hs.z));
            v.Add(center + new Vector3(-hs.x, -hs.y, hs.z));
            v.Add(center + new Vector3(-hs.x, hs.y, -hs.z));
            v.Add(center + new Vector3(hs.x, hs.y, -hs.z));
            v.Add(center + new Vector3(hs.x, hs.y, hs.z));
            v.Add(center + new Vector3(-hs.x, hs.y, hs.z));

            // UVs (simple box unwrap)
            for (int i = 0; i < 8; i++)
            {
                uv.Add(new Vector2(i % 2, i / 4));
                c.Add(color);
            }

            // 12 triangles (6 faces * 2 triangles each)
            int[] indices = {
                0,1,5, 0,5,4, // Front
                1,2,6, 1,6,5, // Right
                2,3,7, 2,7,6, // Back
                3,0,4, 3,4,7, // Left
                4,5,6, 4,6,7, // Top
                3,2,1, 3,1,0  // Bottom
            };

            foreach (int idx in indices)
                t.Add(startIdx + idx);
        }

        private static void AddSphere(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Vector3 center, float radius, int segments, Color color)
        {
            int startIdx = v.Count;
            int rings = segments / 2;

            for (int lat = 0; lat <= rings; lat++)
            {
                float theta = lat * Mathf.PI / rings;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= segments; lon++)
                {
                    float phi = lon * 2 * Mathf.PI / segments;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    Vector3 pos = new Vector3(
                        radius * sinTheta * cosPhi,
                        radius * cosTheta,
                        radius * sinTheta * sinPhi
                    );

                    v.Add(center + pos);
                    uv.Add(new Vector2((float)lon / segments, (float)lat / rings));
                    c.Add(color);
                }
            }

            for (int lat = 0; lat < rings; lat++)
            {
                for (int lon = 0; lon < segments; lon++)
                {
                    int current = startIdx + lat * (segments + 1) + lon;
                    int next = current + segments + 1;

                    t.Add(current);
                    t.Add(next);
                    t.Add(current + 1);

                    t.Add(current + 1);
                    t.Add(next);
                    t.Add(next + 1);
                }
            }
        }

        private static void AddCylinder(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Vector3 center, float radius, float height, int segments, Color color)
        {
            int startIdx = v.Count;
            float halfHeight = height * 0.5f;

            // Bottom circle
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, -halfHeight, Mathf.Sin(angle) * radius);
                v.Add(pos);
                uv.Add(new Vector2((float)i / segments, 0));
                c.Add(color);
            }

            // Top circle
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, halfHeight, Mathf.Sin(angle) * radius);
                v.Add(pos);
                uv.Add(new Vector2((float)i / segments, 1));
                c.Add(color);
            }

            // Side triangles
            for (int i = 0; i < segments; i++)
            {
                int bottom1 = startIdx + i;
                int bottom2 = startIdx + i + 1;
                int top1 = startIdx + segments + 1 + i;
                int top2 = startIdx + segments + 1 + i + 1;

                t.Add(bottom1); t.Add(top1); t.Add(bottom2);
                t.Add(bottom2); t.Add(top1); t.Add(top2);
            }

            // Cap triangles
            v.Add(center + new Vector3(0, -halfHeight, 0)); // Bottom center
            v.Add(center + new Vector3(0, halfHeight, 0));  // Top center
            uv.Add(new Vector2(0.5f, 0)); c.Add(color);
            uv.Add(new Vector2(0.5f, 1)); c.Add(color);

            int bottomCenter = v.Count - 2;
            int topCenter = v.Count - 1;

            for (int i = 0; i < segments; i++)
            {
                t.Add(bottomCenter); t.Add(startIdx + i + 1); t.Add(startIdx + i);
                t.Add(topCenter); t.Add(startIdx + segments + 1 + i); t.Add(startIdx + segments + 1 + i + 1);
            }
        }

        private static void AddTaperedBox(List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> c, Vector3 basePos, Vector3 tipPos, float baseWidth, float tipWidth, Color color)
        {
            int startIdx = v.Count;
            Vector3 dir = (tipPos - basePos).normalized;
            Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized;
            if (right.magnitude < 0.1f) right = Vector3.Cross(dir, Vector3.up).normalized;

            // Base quad
            v.Add(basePos - right * baseWidth);
            v.Add(basePos + right * baseWidth);

            // Tip quad
            v.Add(tipPos - right * tipWidth);
            v.Add(tipPos + right * tipWidth);

            for (int i = 0; i < 4; i++)
            {
                uv.Add(new Vector2(i % 2, i / 2));
                c.Add(color);
            }

            // Front face
            t.Add(startIdx); t.Add(startIdx + 2); t.Add(startIdx + 1);
            t.Add(startIdx + 1); t.Add(startIdx + 2); t.Add(startIdx + 3);

            // Back face (mirrored)
            t.Add(startIdx + 1); t.Add(startIdx + 3); t.Add(startIdx);
            t.Add(startIdx); t.Add(startIdx + 3); t.Add(startIdx + 2);
        }

        private static Color GetStyleColor(ModelStyle style, float brightness)
        {
            Color baseColor = style switch
            {
                ModelStyle.Cyberpunk => new Color(0.2f, 0.8f, 1.0f),
                ModelStyle.Fantasy => new Color(0.6f, 0.5f, 0.3f),
                ModelStyle.Organic => new Color(0.3f, 0.6f, 0.3f),
                ModelStyle.Mechanical => new Color(0.7f, 0.7f, 0.8f),
                _ => Color.white
            };

            return baseColor * brightness;
        }

        private static float GetParameter(Dictionary<string, float> parameters, string key, float defaultValue)
        {
            return parameters != null && parameters.ContainsKey(key) ? parameters[key] : defaultValue;
        }

        private static void AssignMeshData(Mesh mesh, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors)
        {
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}

// EXAMPLE USAGE (for ProjectBootstrapper or other systems):
/*
using QuantumMechanic.Visual;

public class ExampleUsage : MonoBehaviour
{
    void Start()
    {
        // Generate a cyberpunk humanoid
        ModelRequest humanoidRequest = new ModelRequest
        {
            Type = ModelType.Humanoid,
            Style = ModelStyle.Cyberpunk,
            ScaleFactor = 1.0f,
            Parameters = new Dictionary<string, float>
            {
                {"height", 2.0f},
                {"bulk", 1.2f},
                {"musculature", 0.7f}
            }
        };
        GameObject character = ProceduralModelFactory.GenerateModel(humanoidRequest);
        
        // Generate a fantasy sword
        ModelRequest swordRequest = new ModelRequest
        {
            Type = ModelType.Weapon,
            SubType = "Sword",
            Style = ModelStyle.Fantasy,
            Parameters = new Dictionary<string, float>
            {
                {"length", 1.5f},
                {"sharpness", 0.9f}
            }
        };
        GameObject sword = ProceduralModelFactory.GenerateModel(swordRequest);
        
        // Generate an organic creature
        ModelRequest creatureRequest = new ModelRequest
        {
            Type = ModelType.Creature,
            SubType = "Beast",
            Style = ModelStyle.Organic,
            Parameters = new Dictionary<string, float>
            {
                {"size", 1.8f},
                {"aggression", 0.8f}
            }
        };
        GameObject creature = ProceduralModelFactory.GenerateModel(creatureRequest);
    }
}
*/