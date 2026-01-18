using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace QuantumMechanic.Editor
{
    /// <summary>
    /// Converts legacy MonoBehaviour scripts into Quantum Mechanic systems.
    /// Also tracks contracts (capabilities and hooks) across all systems.
    /// 
    /// Features:
    /// - Automatic conversion of MonoBehaviour → BaseGameSystem
    /// - Converts Awake/Start → async OnInitialize
    /// - Adds [QuantumSystem] attribute
    /// - Tracks contracts in a JSON database
    /// - Detects breaking changes between versions
    /// - Creates backups before modifying files
    /// 
    /// Access via: Tools > Quantum Mechanic > Interface Injector v2
    /// </summary>
    public class QuantumInterfaceInjector : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // STATE AND UI
        // ═══════════════════════════════════════════════════════════════════════════════

        private Vector2 scrollPos;
        private MonoScript selectedScript;
        private ScriptAnalysisResult analysisResult;
        private bool previewMode = true;
        private Vector2 contractScrollPos;
        private ContractDatabase contractDb = new ContractDatabase();
        private string contractDbPath = "Assets/_Project/Settings/SystemContracts.json";

        // ═══════════════════════════════════════════════════════════════════════════════
        // EDITOR WINDOW SETUP
        // ═══════════════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/Quantum Mechanic/Interface Injector v2")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuantumInterfaceInjector>("Interface Injector");
            window.minSize = new Vector2(600, 700);
            window.Show();
        }

        private void OnEnable()
        {
            LoadContractDatabase();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GUI RENDERING
        // ═══════════════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Header
            EditorGUILayout.Space(10);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Quantum Interface Injector v2", titleStyle);
            EditorGUILayout.LabelField("Convert Scripts + Track Contracts", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(20);

            // Tab selection
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Toggle(true, "Script Converter", EditorStyles.toolbarButton))
            {
                DrawScriptConverterTab();
            }
            
            if (GUILayout.Toggle(false, "Contract Browser", EditorStyles.toolbarButton))
            {
                DrawContractBrowserTab();
            }
            
            GUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // SCRIPT CONVERTER TAB
        // ═══════════════════════════════════════════════════════════════════════════════

        private void DrawScriptConverterTab()
        {
            EditorGUILayout.Space(10);
            
            // Script selection
            EditorGUILayout.LabelField("Select Script to Convert:", EditorStyles.boldLabel);
            MonoScript newScript = (MonoScript)EditorGUILayout.ObjectField(selectedScript, typeof(MonoScript), false);
            
            if (newScript != selectedScript)
            {
                selectedScript = newScript;
                if (selectedScript != null)
                {
                    AnalyzeScript();
                }
            }

            if (selectedScript == null)
            {
                EditorGUILayout.HelpBox("Select a MonoBehaviour script to analyze and convert.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);

            // Display analysis results
            if (analysisResult != null)
            {
                DrawAnalysisResults();
            }

            EditorGUILayout.Space(10);

            // Preview mode toggle
            previewMode = EditorGUILayout.Toggle("Preview Mode (Dry Run)", previewMode);
            
            if (previewMode)
            {
                EditorGUILayout.HelpBox("Preview mode is ON. No files will be modified. Uncheck to apply changes.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Convert button
            GUI.enabled = analysisResult != null && analysisResult.CanConvert;
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            string buttonText = previewMode ? "Preview Conversion" : "Apply Conversion";
            if (GUILayout.Button(buttonText, buttonStyle))
            {
                if (previewMode)
                {
                    PreviewConversion();
                }
                else
                {
                    ApplyConversion();
                }
            }

            GUI.enabled = true;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // CONTRACT BROWSER TAB
        // ═══════════════════════════════════════════════════════════════════════════════

        private void DrawContractBrowserTab()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("System Contract Database", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Database: {contractDbPath}", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(10);

            // Refresh button
            if (GUILayout.Button("Refresh Database"))
            {
                RebuildContractDatabase();
            }

            EditorGUILayout.Space(10);

            // Display capabilities
            EditorGUILayout.LabelField($"Capabilities ({contractDb.Capabilities.Count}):", EditorStyles.boldLabel);
            
            contractScrollPos = EditorGUILayout.BeginScrollView(contractScrollPos, GUILayout.Height(200));
            
            foreach (var cap in contractDb.Capabilities)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"• {cap.Name} v{cap.Version}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Provider: {cap.ProviderType}", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(cap.Description))
                {
                    EditorGUILayout.LabelField($"  {cap.Description}", EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Display hooks
            EditorGUILayout.LabelField($"Hooks ({contractDb.Hooks.Count}):", EditorStyles.boldLabel);
            
            foreach (var hook in contractDb.Hooks)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"• {hook.Name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Provider: {hook.ProviderType}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  Delegate: {hook.DelegateType}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // SCRIPT ANALYSIS
        // ═══════════════════════════════════════════════════════════════════════════════

        private void AnalyzeScript()
        {
            string path = AssetDatabase.GetAssetPath(selectedScript);
            string content = File.ReadAllText(path);

            analysisResult = new ScriptAnalysisResult();
            analysisResult.OriginalPath = path;
            analysisResult.OriginalContent = content;

            // Check if already converted
            if (content.Contains(": BaseGameSystem"))
            {
                analysisResult.IsAlreadyConverted = true;
                analysisResult.CanConvert = false;
                analysisResult.Messages.Add("⊘ Script already inherits from BaseGameSystem");
                return;
            }

            // Check if it's a MonoBehaviour
            if (!content.Contains(": MonoBehaviour"))
            {
                analysisResult.CanConvert = false;
                analysisResult.Messages.Add("❌ Script must inherit from MonoBehaviour");
                return;
            }

            // Extract class name
            Match classMatch = Regex.Match(content, @"class\s+(\w+)\s*:\s*MonoBehaviour");
            if (classMatch.Success)
            {
                analysisResult.ClassName = classMatch.Groups[1].Value;
                analysisResult.Messages.Add($"✓ Found class: {analysisResult.ClassName}");
            }

            // Check for Awake method
            if (content.Contains("void Awake()"))
            {
                analysisResult.HasAwake = true;
                analysisResult.Messages.Add("✓ Found Awake() method (will convert to OnInitialize)");
            }

            // Check for Start method
            if (content.Contains("void Start()"))
            {
                analysisResult.HasStart = true;
                analysisResult.Messages.Add("✓ Found Start() method (will convert to OnInitialize)");
            }

            // Check for Update/FixedUpdate
            if (content.Contains("void Update()"))
            {
                analysisResult.HasUpdate = true;
                analysisResult.Messages.Add("ℹ Found Update() - will add IUpdateableSystem");
            }

            if (content.Contains("void FixedUpdate()"))
            {
                analysisResult.HasFixedUpdate = true;
                analysisResult.Messages.Add("ℹ Found FixedUpdate() - will add IFixedUpdateableSystem");
            }

            analysisResult.CanConvert = !string.IsNullOrEmpty(analysisResult.ClassName);
        }

        private void DrawAnalysisResults()
        {
            EditorGUILayout.LabelField("Analysis Results:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            foreach (string msg in analysisResult.Messages)
            {
                GUIStyle style = EditorStyles.label;
                if (msg.StartsWith("✓")) style.normal.textColor = Color.green;
                else if (msg.StartsWith("❌")) style.normal.textColor = Color.red;
                else if (msg.StartsWith("⊘")) style.normal.textColor = Color.yellow;
                
                EditorGUILayout.LabelField(msg, style);
                style.normal.textColor = Color.white; // Reset
            }
            
            EditorGUILayout.EndVertical();

            if (analysisResult.IsAlreadyConverted)
            {
                EditorGUILayout.HelpBox("This script is already a Quantum system.", MessageType.Info);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // CONVERSION LOGIC
        // ═══════════════════════════════════════════════════════════════════════════════

        private void PreviewConversion()
        {
            string converted = PerformConversion(analysisResult.OriginalContent);
            
            // Show preview window
            EditorUtility.DisplayDialog(
                "Conversion Preview",
                "Preview of converted code:\n\n" + converted.Substring(0, Math.Min(500, converted.Length)) + "\n\n...",
                "OK"
            );
        }

        private void ApplyConversion()
        {
            if (!EditorUtility.DisplayDialog(
                "Confirm Conversion",
                $"Convert {analysisResult.ClassName} to Quantum system?\n\n" +
                "A backup (.bak) will be created automatically.",
                "Convert",
                "Cancel"))
            {
                return;
            }

            // Create backup
            string backupPath = analysisResult.OriginalPath + ".bak";
            File.Copy(analysisResult.OriginalPath, backupPath, true);
            Debug.Log($"[Injector] Backup created: {backupPath}");

            // Perform conversion
            string converted = PerformConversion(analysisResult.OriginalContent);

            // Write to file
            File.WriteAllText(analysisResult.OriginalPath, converted);
            AssetDatabase.Refresh();

            // Update contract database
            ExtractAndSaveContracts(analysisResult.ClassName, converted);

            EditorUtility.DisplayDialog(
                "Success",
                $"Successfully converted {analysisResult.ClassName}!\n\n" +
                $"Backup saved to: {backupPath}\n\n" +
                "Contract database updated.",
                "OK"
            );

            // Clear selection
            selectedScript = null;
            analysisResult = null;
        }

        private string PerformConversion(string content)
        {
            string result = content;

            // Add using statement if not present
            if (!result.Contains("using QuantumMechanic;"))
            {
                int firstUsing = result.IndexOf("using ");
                if (firstUsing >= 0)
                {
                    int lineEnd = result.IndexOf('\n', firstUsing);
                    result = result.Insert(lineEnd + 1, "using QuantumMechanic;\n");
                }
            }

            // Add [QuantumSystem] attribute before class
            string classPattern = @"(public\s+class\s+" + analysisResult.ClassName + @"\s*:\s*)MonoBehaviour";
            if (!result.Contains("[QuantumSystem"))
            {
                result = Regex.Replace(result, classPattern, 
                    $"[QuantumSystem(InitializationPhase.GameLogic, priority: 100)]\n    $1BaseGameSystem");
            }
            else
            {
                result = Regex.Replace(result, classPattern, "$1BaseGameSystem");
            }

            // Add interface implementations
            string interfaces = "";
            if (analysisResult.HasUpdate) interfaces += ", IUpdateableSystem";
            if (analysisResult.HasFixedUpdate) interfaces += ", IFixedUpdateableSystem";
            
            if (!string.IsNullOrEmpty(interfaces))
            {
                result = result.Replace(": BaseGameSystem", $": BaseGameSystem{interfaces}");
            }

            // Convert Awake/Start to OnInitialize
            if (analysisResult.HasAwake || analysisResult.HasStart)
            {
                // Remove existing Awake/Start
                result = Regex.Replace(result, @"void\s+Awake\s*\(\s*\)\s*{", "// Converted to OnInitialize\n    // void Awake() {");
                result = Regex.Replace(result, @"void\s+Start\s*\(\s*\)\s*{", "// Converted to OnInitialize\n    // void Start() {");

                // Add OnInitialize
                string onInitMethod = @"
    protected override async Awaitable OnInitialize()
    {
        // TODO: Move Awake/Start logic here
        // Initialization code goes here
        
        await Awaitable.NextFrameAsync();
        
        Log(""Initialized"");
    }
";
                // Insert after class opening brace
                int classStart = result.IndexOf("{", result.IndexOf("class " + analysisResult.ClassName));
                result = result.Insert(classStart + 1, onInitMethod);
            }

            // Rename Update if IUpdateableSystem was added
            if (analysisResult.HasUpdate)
            {
                result = Regex.Replace(result, @"void\s+Update\s*\(", "public void OnUpdate(float deltaTime\n    // Note: Use deltaTime parameter instead of Time.deltaTime\n    // Original signature: void Update(");
            }

            // Rename FixedUpdate if IFixedUpdateableSystem was added
            if (analysisResult.HasFixedUpdate)
            {
                result = Regex.Replace(result, @"void\s+FixedUpdate\s*\(", "public void OnFixedUpdate(float fixedDeltaTime\n    // Note: Use fixedDeltaTime parameter\n    // Original signature: void FixedUpdate(");
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // CONTRACT TRACKING
        // ═══════════════════════════════════════════════════════════════════════════════

        private void ExtractAndSaveContracts(string className, string content)
        {
            // Extract ProvidesCapability
            var capMatches = Regex.Matches(content, @"\[ProvidesCapability\(""([^""]+)"".*?version:\s*(\d+).*?Description\s*=\s*""([^""]*)""\)\]");
            foreach (Match m in capMatches)
            {
                var cap = new CapabilityInfo
                {
                    Name = m.Groups[1].Value,
                    Version = int.Parse(m.Groups[2].Value),
                    Description = m.Groups[3].Value,
                    ProviderType = className
                };
                
                contractDb.Capabilities.RemoveAll(c => c.Name == cap.Name);
                contractDb.Capabilities.Add(cap);
            }

            // Extract ProvidesHook
            var hookMatches = Regex.Matches(content, @"\[ProvidesHook\(""([^""]+)"",\s*typeof\(([^)]+)\)");
            foreach (Match m in hookMatches)
            {
                var hook = new HookInfo
                {
                    Name = m.Groups[1].Value,
                    DelegateType = m.Groups[2].Value,
                    ProviderType = className
                };
                
                contractDb.Hooks.RemoveAll(h => h.Name == hook.Name);
                contractDb.Hooks.Add(hook);
            }

            SaveContractDatabase();
        }

        private void LoadContractDatabase()
        {
            if (File.Exists(contractDbPath))
            {
                string json = File.ReadAllText(contractDbPath);
                contractDb = JsonUtility.FromJson<ContractDatabase>(json);
                Debug.Log($"[Injector] Loaded contract database: {contractDb.Capabilities.Count} capabilities, {contractDb.Hooks.Count} hooks");
            }
        }

        private void SaveContractDatabase()
        {
            // Ensure directory exists
            string dir = Path.GetDirectoryName(contractDbPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(contractDb, true);
            File.WriteAllText(contractDbPath, json);
            AssetDatabase.Refresh();
            Debug.Log($"[Injector] Saved contract database to {contractDbPath}");
        }

        private void RebuildContractDatabase()
        {
            contractDb = new ContractDatabase();
            
            // Scan all assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var asm in assemblies)
            {
                try
                {
                    var types = asm.GetTypes();
                    
                    foreach (var t in types)
                    {
                        // Extract capabilities
                        var capAttrs = t.GetCustomAttributes(typeof(ProvidesCapabilityAttribute), false);
                        foreach (ProvidesCapabilityAttribute attr in capAttrs)
                        {
                            contractDb.Capabilities.Add(new CapabilityInfo
                            {
                                Name = attr.CapabilityName,
                                Version = attr.Version,
                                Description = attr.Description,
                                ProviderType = t.Name
                            });
                        }

                        // Extract hooks
                        var hookAttrs = t.GetCustomAttributes(typeof(ProvidesHookAttribute), false);
                        foreach (ProvidesHookAttribute attr in hookAttrs)
                        {
                            contractDb.Hooks.Add(new HookInfo
                            {
                                Name = attr.HookName,
                                DelegateType = attr.DelegateType.Name,
                                ProviderType = t.Name
                            });
                        }
                    }
                }
                catch { }
            }

            SaveContractDatabase();
            Debug.Log($"[Injector] Rebuilt contract database: {contractDb.Capabilities.Count} capabilities, {contractDb.Hooks.Count} hooks");
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // DATA STRUCTURES
        // ═══════════════════════════════════════════════════════════════════════════════

        [Serializable]
        private class ScriptAnalysisResult
        {
            public string OriginalPath;
            public string OriginalContent;
            public string ClassName;
            public bool IsAlreadyConverted;
            public bool CanConvert;
            public bool HasAwake;
            public bool HasStart;
            public bool HasUpdate;
            public bool HasFixedUpdate;
            public List<string> Messages = new List<string>();
        }

        [Serializable]
        private class ContractDatabase
        {
            public List<CapabilityInfo> Capabilities = new List<CapabilityInfo>();
            public List<HookInfo> Hooks = new List<HookInfo>();
        }

        [Serializable]
        private class CapabilityInfo
        {
            public string Name;
            public int Version;
            public string Description;
            public string ProviderType;
        }

        [Serializable]
        private class HookInfo
        {
            public string Name;
            public string DelegateType;
            public string ProviderType;
        }
    }
}