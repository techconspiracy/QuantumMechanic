using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuantumMechanic.Editor
{
    /// <summary>
    /// Project automation wizard for Quantum Mechanic framework.
    /// Provides one-click setup for folder structure, scenes, and build settings.
    /// Access via: Tools > Quantum Mechanic > Project Automator
    /// 
    /// This tool creates a complete project structure:
    /// - Organized folder hierarchy
    /// - Bootstrap and MainMenu scenes
    /// - Build settings configuration
    /// - System templates for quick development
    /// </summary>
    public class QuantumAutomator : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // EDITOR WINDOW SETUP
        // ═══════════════════════════════════════════════════════════════════════════════

        private Vector2 scrollPos;
        private bool isProcessing = false;
        private float progressValue = 0f;
        private string progressMessage = "";
        private List<string> logMessages = new List<string>();

        /// <summary>
        /// Creates the menu item to open this window.
        /// Found under Tools > Quantum Mechanic > Project Automator
        /// </summary>
        [MenuItem("Tools/Quantum Mechanic/Project Automator")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuantumAutomator>("Quantum Automator");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GUI RENDERING
        // ═══════════════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            // Header
            EditorGUILayout.Space(10);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Quantum Mechanic Project Automator", titleStyle);
            EditorGUILayout.LabelField("One-Click Project Setup", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(20);

            // Main button
            GUI.enabled = !isProcessing;
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 50
            };

            if (GUILayout.Button("🚀 Initialize Project Ecology", buttonStyle))
            {
                InitializeProjectEcology();
            }

            GUI.enabled = true;

            // Progress section
            if (isProcessing)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("Progress:", EditorStyles.boldLabel);
                Rect progressRect = EditorGUILayout.GetControlRect(false, 25);
                EditorGUI.ProgressBar(progressRect, progressValue, progressMessage);
            }

            // Log section
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Activity Log:", EditorStyles.boldLabel);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
            
            GUIStyle logStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = true
            };

            foreach (string msg in logMessages)
            {
                EditorGUILayout.LabelField(msg, logStyle);
            }

            EditorGUILayout.EndScrollView();

            // Info section
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "This tool will create:\n" +
                "• Complete folder structure (_Project/Scripts, Content, Scenes, etc.)\n" +
                "• Bootstrap.unity and MainMenu.unity scenes\n" +
                "• Build settings configuration\n" +
                "• Example system templates\n\n" +
                "Safe to run multiple times - existing files won't be overwritten.",
                MessageType.Info
            );

            // Additional tools section
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Additional Tools:", EditorStyles.boldLabel);

            if (GUILayout.Button("Scan All Systems"))
            {
                ScanAllSystems();
            }

            if (GUILayout.Button("Generate System Template"))
            {
                ShowSystemTemplateDialog();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // MAIN INITIALIZATION PROCESS
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Main entry point for project initialization.
        /// Orchestrates the entire setup process.
        /// </summary>
        private void InitializeProjectEcology()
        {
            isProcessing = true;
            logMessages.Clear();
            progressValue = 0f;

            LogMessage("🚀 Starting Quantum Mechanic project initialization...");

            try
            {
                // Step 1: Create folder structure
                UpdateProgress(0.1f, "Creating folder structure...");
                CreateFolderStructure();

                // Step 2: Create bootstrap scenes
                UpdateProgress(0.4f, "Creating bootstrap scenes...");
                CreateBootstrapScenes();

                // Step 3: Configure build settings
                UpdateProgress(0.7f, "Configuring build settings...");
                ConfigureBuildSettings();

                // Step 4: Create example templates
                UpdateProgress(0.9f, "Creating example templates...");
                CreateExampleTemplates();

                // Done!
                UpdateProgress(1f, "Complete!");
                LogMessage("✓ Project ecology initialized successfully!");
                LogMessage("Press Play to see the Quantum Mechanic framework in action.");
                
                EditorUtility.DisplayDialog(
                    "Success",
                    "Quantum Mechanic project ecology has been initialized!\n\n" +
                    "You can now:\n" +
                    "• Press Play to see the bootstrap process\n" +
                    "• Create new systems in _Project/Scripts/Systems/\n" +
                    "• Use the Interface Injector to convert existing scripts",
                    "Got it!"
                );
            }
            catch (System.Exception ex)
            {
                LogMessage($"❌ Error: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to initialize project:\n{ex.Message}", "OK");
            }
            finally
            {
                isProcessing = false;
                progressValue = 0f;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // FOLDER STRUCTURE CREATION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the complete folder hierarchy for a Quantum Mechanic project.
        /// Organizes code, content, and resources in a logical structure.
        /// </summary>
        private void CreateFolderStructure()
        {
            string[] folders = new string[]
            {
                // Core Scripts
                "Assets/_Project",
                "Assets/_Project/Scripts",
                "Assets/_Project/Scripts/Systems",
                "Assets/_Project/Scripts/Systems/Core",
                "Assets/_Project/Scripts/Systems/Combat",
                "Assets/_Project/Scripts/Systems/Progression",
                "Assets/_Project/Scripts/Systems/UI",
                "Assets/_Project/Scripts/Systems/Network",
                "Assets/_Project/Scripts/Systems/Audio",
                "Assets/_Project/Scripts/Gameplay",
                "Assets/_Project/Scripts/Gameplay/Player",
                "Assets/_Project/Scripts/Gameplay/NPCs",
                "Assets/_Project/Scripts/Gameplay/Items",
                "Assets/_Project/Scripts/Utilities",

                // Content
                "Assets/_Project/Content",
                "Assets/_Project/Content/Prefabs",
                "Assets/_Project/Content/Prefabs/Characters",
                "Assets/_Project/Content/Prefabs/Environment",
                "Assets/_Project/Content/Prefabs/UI",
                "Assets/_Project/Content/Materials",
                "Assets/_Project/Content/Textures",
                "Assets/_Project/Content/Models",
                "Assets/_Project/Content/Audio",
                "Assets/_Project/Content/Audio/Music",
                "Assets/_Project/Content/Audio/SFX",
                "Assets/_Project/Content/VFX",
                "Assets/_Project/Content/Animations",

                // Scenes
                "Assets/_Project/Scenes",
                "Assets/_Project/Scenes/Gameplay",
                "Assets/_Project/Scenes/UI",

                // Resources and Settings
                "Assets/_Project/Resources",
                "Assets/_Project/Settings",
                "Assets/_Project/Documentation"
            };

            int created = 0;
            int skipped = 0;

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string name = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            AssetDatabase.Refresh();
            LogMessage($"✓ Folder structure created: {created} new, {skipped} existing");
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // SCENE CREATION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the Bootstrap and MainMenu scenes programmatically.
        /// Bootstrap scene is the entry point that initializes all systems.
        /// </summary>
        private void CreateBootstrapScenes()
        {
            // Create Bootstrap scene
            string bootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
            if (!File.Exists(bootstrapPath))
            {
                Scene bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                
                // Add directional light
                GameObject light = new GameObject("Directional Light");
                Light lightComp = light.AddComponent<Light>();
                lightComp.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);

                // Add camera
                GameObject cam = new GameObject("Main Camera");
                cam.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.AddComponent<AudioListener>();

                // Save scene
                EditorSceneManager.SaveScene(bootstrapScene, bootstrapPath);
                LogMessage($"✓ Created Bootstrap scene at {bootstrapPath}");
            }
            else
            {
                LogMessage($"⊘ Bootstrap scene already exists at {bootstrapPath}");
            }

            // Create MainMenu scene
            string mainMenuPath = "Assets/_Project/Scenes/UI/MainMenu.unity";
            if (!File.Exists(mainMenuPath))
            {
                Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                
                // Add Canvas
                GameObject canvas = new GameObject("Canvas");
                Canvas canvasComp = canvas.AddComponent<Canvas>();
                canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                // Add EventSystem
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

                // Add title text
                GameObject titleObj = new GameObject("Title");
                titleObj.transform.SetParent(canvas.transform);
                UnityEngine.UI.Text titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
                titleText.text = "Main Menu";
                titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleText.fontSize = 48;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = Color.white;
                RectTransform titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0.5f, 0.7f);
                titleRect.anchorMax = new Vector2(0.5f, 0.7f);
                titleRect.sizeDelta = new Vector2(400, 100);

                EditorSceneManager.SaveScene(menuScene, mainMenuPath);
                LogMessage($"✓ Created MainMenu scene at {mainMenuPath}");
            }
            else
            {
                LogMessage($"⊘ MainMenu scene already exists at {mainMenuPath}");
            }

            AssetDatabase.Refresh();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // BUILD SETTINGS CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Configures Unity's build settings to include the bootstrap scene first.
        /// This ensures the Quantum Mechanic framework initializes before any other scenes.
        /// </summary>
        private void ConfigureBuildSettings()
        {
            string bootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
            string mainMenuPath = "Assets/_Project/Scenes/UI/MainMenu.unity";

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

            // Add Bootstrap scene first (index 0)
            if (File.Exists(bootstrapPath))
            {
                scenes.Add(new EditorBuildSettingsScene(bootstrapPath, true));
            }

            // Add MainMenu scene second
            if (File.Exists(mainMenuPath))
            {
                scenes.Add(new EditorBuildSettingsScene(mainMenuPath, true));
            }

            // Keep any existing scenes that aren't duplicates
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != bootstrapPath && existing.path != mainMenuPath)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            LogMessage($"✓ Build settings configured: {scenes.Count} scenes");
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TEMPLATE GENERATION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates example system templates to help developers get started quickly.
        /// </summary>
        private void CreateExampleTemplates()
        {
            // Create a simple README in the Documentation folder
            string readmePath = "Assets/_Project/Documentation/README.txt";
            if (!File.Exists(readmePath))
            {
                string readmeContent = @"QUANTUM MECHANIC FRAMEWORK
=========================

Your project has been initialized with the Quantum Mechanic framework!

FOLDER STRUCTURE:
- Scripts/Systems/Core: Foundational systems (Events, Save, Resources)
- Scripts/Systems/Combat: Combat-related systems
- Scripts/Systems/UI: User interface systems
- Scripts/Gameplay: Game-specific logic and components
- Content: Prefabs, materials, models, audio, etc.
- Scenes: Bootstrap (entry point) and gameplay scenes

GETTING STARTED:
1. Press Play to see the framework bootstrap
2. Create new systems by inheriting from BaseGameSystem
3. Use [QuantumSystem] attribute to mark your systems
4. Access systems via QuantumBootstrapper.Instance.GetSystem<T>()

NEXT STEPS:
• Check out EventSystem.cs in Systems/Core for an example
• Use Tools > Quantum Mechanic > Interface Injector to convert existing scripts
• Read the documentation at [your documentation URL]

Happy coding!
";
                File.WriteAllText(readmePath, readmeContent);
                AssetDatabase.Refresh();
                LogMessage("✓ Created README documentation");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ADDITIONAL TOOLS
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scans the project and displays all discovered Quantum systems.
        /// </summary>
        private void ScanAllSystems()
        {
            LogMessage("Scanning project for Quantum systems...");

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            int count = 0;

            foreach (var asm in assemblies)
            {
                try
                {
                    var types = asm.GetTypes();
                    foreach (var t in types)
                    {
                        var attr = t.GetCustomAttributes(typeof(QuantumSystemAttribute), false);
                        if (attr.Length > 0)
                        {
                            var qsAttr = (QuantumSystemAttribute)attr[0];
                            LogMessage($"  • {t.Name} (Phase: {qsAttr.Phase}, Priority: {qsAttr.Priority})");
                            count++;
                        }
                    }
                }
                catch { }
            }

            LogMessage($"✓ Found {count} Quantum systems");
        }

        /// <summary>
        /// Shows a dialog to generate a new system template.
        /// </summary>
        private void ShowSystemTemplateDialog()
        {
            string systemName = EditorInputDialog.Show("New System", "Enter system name:", "MySystem");
            
            if (!string.IsNullOrEmpty(systemName))
            {
                GenerateSystemTemplate(systemName);
            }
        }

        /// <summary>
        /// Generates a new system template file.
        /// </summary>
        private void GenerateSystemTemplate(string systemName)
        {
            string template = $@"using UnityEngine;
using QuantumMechanic;

namespace YourNamespace.Systems
{{
    /// <summary>
    /// TODO: Describe what this system does
    /// </summary>
    [QuantumSystem(InitializationPhase.GameLogic, priority: 100)]
    public class {systemName} : BaseGameSystem
    {{
        protected override async Awaitable OnInitialize()
        {{
            // TODO: Initialize your system here
            Log(""Initializing..."");
            
            await Awaitable.NextFrameAsync();
            
            Log(""Ready!"");
        }}

        public override bool IsHealthy()
        {{
            return base.IsHealthy();
        }}
    }}
}}
";

            string path = $"Assets/_Project/Scripts/Systems/{systemName}.cs";
            File.WriteAllText(path, template);
            AssetDatabase.Refresh();
            
            LogMessage($"✓ Created system template at {path}");
            EditorUtility.DisplayDialog("Success", $"System template created:\n{path}", "OK");
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════════════

        private void UpdateProgress(float value, string message)
        {
            progressValue = value;
            progressMessage = message;
            Repaint();
        }

        private void LogMessage(string message)
        {
            logMessages.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            Repaint();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // SIMPLE INPUT DIALOG UTILITY
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simple utility for getting text input from the user.
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string inputText = "";
        private string promptMessage = "";
        private System.Action<string> onComplete;

        public static string Show(string title, string prompt, string defaultValue = "")
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.promptMessage = prompt;
            window.inputText = defaultValue;
            window.minSize = new Vector2(300, 100);
            window.maxSize = new Vector2(300, 100);
            
            window.ShowModal();
            
            return window.inputText;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(promptMessage);
            inputText = EditorGUILayout.TextField(inputText);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("OK"))
            {
                Close();
            }
            
            if (GUILayout.Button("Cancel"))
            {
                inputText = "";
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
}