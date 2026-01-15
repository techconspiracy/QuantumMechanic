// MODULE: Visual-02
// FILE: TextureGenerator.cs
// DEPENDENCIES: UnityEngine, System.Collections.Generic
// INTEGRATES WITH: ProceduralModelFactory (material texturing), Future character/weapon customization
// PURPOSE: Runtime procedural texture generation for infinite visual variety

using UnityEngine;
using System.Collections.Generic;

namespace QuantumMechanic.Visual
{
    /// <summary>
    /// Noise algorithm types for texture generation
    /// </summary>
    public enum NoiseType
    {
        Perlin,
        Cellular,
        Voronoi,
        FBM,
        Turbulence
    }

    /// <summary>
    /// Gradient interpolation patterns
    /// </summary>
    public enum GradientType
    {
        Linear,
        Radial,
        Angular,
        Diamond
    }

    /// <summary>
    /// High-level texture pattern categories
    /// </summary>
    public enum PatternType
    {
        Solid,
        Gradient,
        Noise,
        PanelLines,
        Organic,
        Mechanical
    }

    /// <summary>
    /// Visual style matching ProceduralModelFactory
    /// </summary>
    public enum ModelStyle
    {
        Cyberpunk,
        Fantasy,
        Organic,
        Mechanical
    }

    /// <summary>
    /// Comprehensive texture generation request
    /// </summary>
    public class TextureRequest
    {
        public PatternType Pattern = PatternType.Solid;
        public int Resolution = 256;
        public Color PrimaryColor = Color.white;
        public Color SecondaryColor = Color.black;
        public float NoiseScale = 5.0f;
        public float NoiseIntensity = 0.5f;
        public ModelStyle Style = ModelStyle.Cyberpunk;
        public Dictionary<string, float> Parameters = new Dictionary<string, float>();

        public TextureRequest() { }

        public TextureRequest(PatternType pattern, ModelStyle style, int resolution = 256)
        {
            Pattern = pattern;
            Style = style;
            Resolution = resolution;
        }
    }

    /// <summary>
    /// Runtime procedural texture generator with style-aware pattern synthesis
    /// </summary>
    /// <example>
    /// // Generate cyberpunk panel texture
    /// var request = new TextureRequest(PatternType.PanelLines, ModelStyle.Cyberpunk);
    /// request.PrimaryColor = new Color(0.1f, 0.1f, 0.15f);
    /// request.SecondaryColor = new Color(0, 1, 1);
    /// Texture2D texture = TextureGenerator.GenerateTexture(request);
    /// Material mat = TextureGenerator.ApplyTextureToMaterial(baseMaterial, texture);
    /// </example>
    public static class TextureGenerator
    {
        /// <summary>
        /// Main entry point for texture generation from request parameters
        /// </summary>
        public static Texture2D GenerateTexture(TextureRequest request)
        {
            Texture2D texture = null;

            switch (request.Pattern)
            {
                case PatternType.Solid:
                    texture = GenerateSolidColor(request.PrimaryColor, request.Resolution, request.NoiseIntensity);
                    break;

                case PatternType.Gradient:
                    GradientType gradType = GetGradientTypeForStyle(request.Style);
                    texture = GenerateGradient(request.PrimaryColor, request.SecondaryColor, gradType, request.Resolution);
                    break;

                case PatternType.Noise:
                    NoiseType noiseType = GetNoiseTypeForStyle(request.Style);
                    texture = GenerateNoise(noiseType, request.Resolution, request.NoiseScale, request.NoiseIntensity);
                    break;

                case PatternType.PanelLines:
                    int panelCount = request.Parameters.ContainsKey("panelCount") 
                        ? Mathf.RoundToInt(request.Parameters["panelCount"]) 
                        : 4;
                    texture = GeneratePanelLines(request.Resolution, request.PrimaryColor, request.SecondaryColor, panelCount);
                    break;

                case PatternType.Organic:
                    texture = GenerateOrganicPattern(request.Style, request.Resolution, request.PrimaryColor, request.SecondaryColor);
                    break;

                case PatternType.Mechanical:
                    texture = GenerateMechanicalPattern(request.Style, request.Resolution, request.PrimaryColor, request.NoiseIntensity);
                    break;

                default:
                    texture = GenerateSolidColor(request.PrimaryColor, request.Resolution, 0f);
                    break;
            }

            return texture;
        }

        /// <summary>
        /// Generate solid color texture with optional noise variation
        /// </summary>
        public static Texture2D GenerateSolidColor(Color color, int resolution, float noiseAmount)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Color pixelColor = color;

                    if (noiseAmount > 0f)
                    {
                        float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                        float variation = (noise - 0.5f) * noiseAmount;
                        pixelColor.r = Mathf.Clamp01(color.r + variation);
                        pixelColor.g = Mathf.Clamp01(color.g + variation);
                        pixelColor.b = Mathf.Clamp01(color.b + variation);
                    }

                    texture.SetPixel(x, y, pixelColor);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Generate gradient texture with specified interpolation type
        /// </summary>
        public static Texture2D GenerateGradient(Color colorA, Color colorB, GradientType type, int resolution)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float t = CalculateGradientValue(x, y, resolution, type);
                    Color color = Color.Lerp(colorA, colorB, Mathf.Clamp01(t));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Generate noise-based texture using specified algorithm
        /// </summary>
        public static Texture2D GenerateNoise(NoiseType type, int resolution, float scale, float intensity)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float noise = CalculateNoiseValue(x, y, resolution, scale, type);
                    float value = Mathf.Clamp01(noise * intensity);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Generate panel line texture for cyberpunk/mechanical aesthetics
        /// </summary>
        public static Texture2D GeneratePanelLines(int resolution, Color baseColor, Color lineColor, int panelCount)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    texture.SetPixel(x, y, baseColor);
                }
            }

            int lineThickness = Mathf.Max(1, resolution / 128);
            int panelSize = resolution / panelCount;

            for (int i = 0; i <= panelCount; i++)
            {
                int linePos = i * panelSize;

                for (int y = 0; y < resolution; y++)
                {
                    for (int t = 0; t < lineThickness; t++)
                    {
                        if (linePos + t < resolution)
                        {
                            texture.SetPixel(linePos + t, y, lineColor);
                        }
                    }
                }

                for (int x = 0; x < resolution; x++)
                {
                    for (int t = 0; t < lineThickness; t++)
                    {
                        if (linePos + t < resolution)
                        {
                            texture.SetPixel(x, linePos + t, lineColor);
                        }
                    }
                }
            }

            float noiseScale = 0.05f;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Color current = texture.GetPixel(x, y);
                    if (current != lineColor)
                    {
                        float noise = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
                        float variation = (noise - 0.5f) * 0.1f;
                        current.r = Mathf.Clamp01(current.r + variation);
                        current.g = Mathf.Clamp01(current.g + variation);
                        current.b = Mathf.Clamp01(current.b + variation);
                        texture.SetPixel(x, y, current);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Apply generated texture to material's base map property
        /// </summary>
        public static Material ApplyTextureToMaterial(Material baseMaterial, Texture2D texture)
        {
            Material newMaterial = new Material(baseMaterial);
            newMaterial.SetTexture("_BaseMap", texture);
            return newMaterial;
        }

        /// <summary>
        /// Generate organic patterns (scales, bark, fur) based on style
        /// </summary>
        private static Texture2D GenerateOrganicPattern(ModelStyle style, int resolution, Color primaryColor, Color secondaryColor)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float pattern = 0f;

                    switch (style)
                    {
                        case ModelStyle.Organic:
                            pattern = GenerateScalePattern(x, y, resolution);
                            break;
                        case ModelStyle.Fantasy:
                            pattern = GenerateBarkPattern(x, y, resolution);
                            break;
                        default:
                            pattern = GenerateFurPattern(x, y, resolution);
                            break;
                    }

                    Color color = Color.Lerp(primaryColor, secondaryColor, pattern);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Generate mechanical patterns (brushed metal, carbon fiber) based on style
        /// </summary>
        private static Texture2D GenerateMechanicalPattern(ModelStyle style, int resolution, Color baseColor, float intensity)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float pattern = 0f;

                    switch (style)
                    {
                        case ModelStyle.Mechanical:
                            pattern = GenerateBrushedMetalPattern(x, y, resolution);
                            break;
                        case ModelStyle.Cyberpunk:
                            pattern = GenerateCarbonFiberPattern(x, y, resolution);
                            break;
                        default:
                            pattern = GenerateScratchPattern(x, y, resolution);
                            break;
                    }

                    float variation = pattern * intensity;
                    Color color = new Color(
                        Mathf.Clamp01(baseColor.r + variation),
                        Mathf.Clamp01(baseColor.g + variation),
                        Mathf.Clamp01(baseColor.b + variation),
                        baseColor.a
                    );
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Calculate gradient interpolation value based on type
        /// </summary>
        private static float CalculateGradientValue(int x, int y, int resolution, GradientType type)
        {
            float t = 0f;

            switch (type)
            {
                case GradientType.Linear:
                    t = (float)y / resolution;
                    break;

                case GradientType.Radial:
                    float dx = (x - resolution * 0.5f) / (resolution * 0.5f);
                    float dy = (y - resolution * 0.5f) / (resolution * 0.5f);
                    t = Mathf.Sqrt(dx * dx + dy * dy);
                    break;

                case GradientType.Angular:
                    t = Mathf.Atan2(y - resolution * 0.5f, x - resolution * 0.5f) / (2f * Mathf.PI) + 0.5f;
                    break;

                case GradientType.Diamond:
                    float distX = Mathf.Abs(x - resolution * 0.5f) / (resolution * 0.5f);
                    float distY = Mathf.Abs(y - resolution * 0.5f) / (resolution * 0.5f);
                    t = Mathf.Max(distX, distY);
                    break;
            }

            return t;
        }

        /// <summary>
        /// Calculate noise value using specified algorithm
        /// </summary>
        private static float CalculateNoiseValue(int x, int y, int resolution, float scale, NoiseType type)
        {
            float noise = 0f;
            float nx = x * scale / resolution;
            float ny = y * scale / resolution;

            switch (type)
            {
                case NoiseType.Perlin:
                    noise = Mathf.PerlinNoise(nx, ny);
                    break;

                case NoiseType.FBM:
                    noise = FractalBrownianMotion(nx, ny, 4);
                    break;

                case NoiseType.Turbulence:
                    noise = Mathf.Abs(FractalBrownianMotion(nx, ny, 4) * 2f - 1f);
                    break;

                case NoiseType.Cellular:
                    noise = CellularNoise(nx, ny, 5f);
                    break;

                case NoiseType.Voronoi:
                    noise = VoronoiNoise(nx, ny, 5f);
                    break;
            }

            return noise;
        }

        /// <summary>
        /// Multi-octave Perlin noise for detailed texture generation
        /// </summary>
        private static float FractalBrownianMotion(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return value / maxValue;
        }

        /// <summary>
        /// Cellular noise using distance to nearest grid point
        /// </summary>
        private static float CellularNoise(float x, float y, float cellSize)
        {
            int cellX = Mathf.FloorToInt(x * cellSize);
            int cellY = Mathf.FloorToInt(y * cellSize);

            float minDist = float.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = cellX + dx;
                    int cy = cellY + dy;

                    float pointX = cx + Hash2D(cx, cy);
                    float pointY = cy + Hash2D(cy, cx);

                    float dist = Vector2.Distance(
                        new Vector2(x * cellSize, y * cellSize),
                        new Vector2(pointX, pointY)
                    );

                    minDist = Mathf.Min(minDist, dist);
                }
            }

            return Mathf.Clamp01(minDist / cellSize);
        }

        /// <summary>
        /// Voronoi noise using cell center distance
        /// </summary>
        private static float VoronoiNoise(float x, float y, float cellSize)
        {
            int cellX = Mathf.FloorToInt(x * cellSize);
            int cellY = Mathf.FloorToInt(y * cellSize);

            float minDist = float.MaxValue;
            float secondMinDist = float.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = cellX + dx;
                    int cy = cellY + dy;

                    float pointX = cx + Hash2D(cx, cy);
                    float pointY = cy + Hash2D(cy, cx);

                    float dist = Vector2.Distance(
                        new Vector2(x * cellSize, y * cellSize),
                        new Vector2(pointX, pointY)
                    );

                    if (dist < minDist)
                    {
                        secondMinDist = minDist;
                        minDist = dist;
                    }
                    else if (dist < secondMinDist)
                    {
                        secondMinDist = dist;
                    }
                }
            }

            return Mathf.Clamp01((secondMinDist - minDist) / cellSize);
        }

        /// <summary>
        /// Simple 2D hash function for pseudo-random values
        /// </summary>
        private static float Hash2D(int x, int y)
        {
            int hash = x * 374761393 + y * 668265263;
            hash = (hash ^ (hash >> 13)) * 1274126177;
            return ((hash ^ (hash >> 16)) & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        /// <summary>
        /// Generate reptilian scale pattern
        /// </summary>
        private static float GenerateScalePattern(int x, int y, int resolution)
        {
            float scale = 8f;
            float nx = x * scale / resolution;
            float ny = y * scale / resolution;

            float hexX = nx;
            float hexY = ny * 0.866f + (Mathf.Floor(nx) % 2) * 0.433f;

            int cellX = Mathf.FloorToInt(hexX);
            int cellY = Mathf.FloorToInt(hexY);

            float fx = hexX - cellX;
            float fy = hexY - cellY;

            float dist = Mathf.Sqrt(Mathf.Pow(fx - 0.5f, 2f) + Mathf.Pow(fy - 0.5f, 2f));
            float scaleEdge = Mathf.SmoothStep(0.4f, 0.5f, dist);

            float noise = Mathf.PerlinNoise(nx * 2f, ny * 2f) * 0.2f;
            return Mathf.Clamp01(scaleEdge + noise);
        }

        /// <summary>
        /// Generate tree bark texture pattern
        /// </summary>
        private static float GenerateBarkPattern(int x, int y, int resolution)
        {
            float nx = x / (float)resolution;
            float ny = y / (float)resolution;

            float vertical = Mathf.PerlinNoise(nx * 2f, ny * 20f) * 0.5f;
            float detail = FractalBrownianMotion(nx * 10f, ny * 10f, 3) * 0.3f;
            float cracks = Mathf.Abs(Mathf.Sin(nx * 40f + Mathf.PerlinNoise(ny * 5f, 0) * 3f)) < 0.1f ? -0.2f : 0f;

            return Mathf.Clamp01(0.4f + vertical + detail + cracks);
        }

        /// <summary>
        /// Generate fur-like texture pattern
        /// </summary>
        private static float GenerateFurPattern(int x, int y, int resolution)
        {
            float nx = x / (float)resolution;
            float ny = y / (float)resolution;

            float strands = Mathf.PerlinNoise(nx * 50f, ny * 2f) * 0.6f;
            float softness = FractalBrownianMotion(nx * 20f, ny * 20f, 2) * 0.4f;

            return Mathf.Clamp01(strands + softness);
        }

        /// <summary>
        /// Generate brushed metal anisotropic pattern
        /// </summary>
        private static float GenerateBrushedMetalPattern(int x, int y, int resolution)
        {
            float ny = y / (float)resolution;
            float horizontal = Mathf.PerlinNoise(0, ny * 100f) * 0.3f;
            float scratches = Mathf.PerlinNoise(x * 0.1f, ny * 500f) * 0.1f;

            return horizontal + scratches - 0.15f;
        }

        /// <summary>
        /// Generate carbon fiber weave pattern
        /// </summary>
        private static float GenerateCarbonFiberPattern(int x, int y, int resolution)
        {
            float scale = 16f;
            float nx = x * scale / resolution;
            float ny = y * scale / resolution;

            float weaveX = Mathf.Sin(nx * Mathf.PI) * 0.5f + 0.5f;
            float weaveY = Mathf.Sin(ny * Mathf.PI) * 0.5f + 0.5f;

            float weave = weaveX * weaveY;
            float alternate = ((Mathf.FloorToInt(nx) + Mathf.FloorToInt(ny)) % 2) * 0.2f;

            return Mathf.Clamp01(weave * 0.3f + alternate);
        }

        /// <summary>
        /// Generate random scratch marks pattern
        /// </summary>
        private static float GenerateScratchPattern(int x, int y, int resolution)
        {
            float nx = x / (float)resolution;
            float ny = y / (float)resolution;

            float scratches = 0f;
            scratches += Mathf.Abs(Mathf.PerlinNoise(nx * 100f, ny * 2f) - 0.5f) < 0.05f ? -0.3f : 0f;
            scratches += Mathf.Abs(Mathf.PerlinNoise(nx * 2f, ny * 100f) - 0.5f) < 0.05f ? -0.3f : 0f;
            scratches += FractalBrownianMotion(nx * 20f, ny * 20f, 2) * 0.1f;

            return scratches;
        }

        /// <summary>
        /// Map ModelStyle to appropriate NoiseType
        /// </summary>
        private static NoiseType GetNoiseTypeForStyle(ModelStyle style)
        {
            return style switch
            {
                ModelStyle.Cyberpunk => NoiseType.Voronoi,
                ModelStyle.Fantasy => NoiseType.Perlin,
                ModelStyle.Organic => NoiseType.FBM,
                ModelStyle.Mechanical => NoiseType.Cellular,
                _ => NoiseType.Perlin
            };
        }

        /// <summary>
        /// Map ModelStyle to appropriate GradientType
        /// </summary>
        private static GradientType GetGradientTypeForStyle(ModelStyle style)
        {
            return style switch
            {
                ModelStyle.Cyberpunk => GradientType.Angular,
                ModelStyle.Fantasy => GradientType.Linear,
                ModelStyle.Organic => GradientType.Radial,
                ModelStyle.Mechanical => GradientType.Linear,
                _ => GradientType.Linear
            };
        }
    }
}

// USAGE EXAMPLE - Integration with ProceduralModelFactory:
//
// public static Material GetStyleMaterial(ModelStyle style)
// {
//     Material baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//     
//     var request = new TextureRequest(PatternType.PanelLines, style, 512);
//     request.PrimaryColor = style == ModelStyle.Cyberpunk ? new Color(0.1f, 0.1f, 0.15f) : Color.gray;
//     request.SecondaryColor = style == ModelStyle.Cyberpunk ? new Color(0, 1, 1) : Color.black;
//     
//     Texture2D texture = TextureGenerator.GenerateTexture(request);
//     return TextureGenerator.ApplyTextureToMaterial(baseMat, texture);
// }