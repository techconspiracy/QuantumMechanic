// File: Assets/Editor/RPG/AtomicModuleGenerator.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

namespace RPG.Editor
{
    /// <summary>
    /// Unity 6 Editor Tool: Generates partial NetworkBehaviour modules with NGO best practices.
    /// Prevents overwriting user logic by using partial class architecture.
    /// </summary>
    public class AtomicModuleGenerator : EditorWindow
    {
        private string _moduleName = "MyRPGModule";
        private bool _implementIDamageable;
        private bool _implementIResourcePool;
        private bool _requireScriptableObjectConfig;
        private string _outputFolder = "Assets/Scripts/RPG/Modules";

        [MenuItem("RPG Tools/Atomic Module Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<AtomicModuleGenerator>("Module Generator");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity 6 NGO Module Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _moduleName = EditorGUILayout.TextField("Module Name", _moduleName);
            EditorGUILayout.Space();

            GUILayout.Label("Implement Interfaces:", EditorStyles.boldLabel);
            _implementIDamageable = EditorGUILayout.Toggle("IDamageable", _implementIDamageable);
            _implementIResourcePool = EditorGUILayout.Toggle("IResourcePool", _implementIResourcePool);
            EditorGUILayout.Space();

            _requireScriptableObjectConfig = EditorGUILayout.Toggle("Use ScriptableObject Config", _requireScriptableObjectConfig);
            EditorGUILayout.Space();

            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Module", GUILayout.Height(40)))
            {
                GenerateModule();
            }

            EditorGUILayout.HelpBox(
                "This generates TWO files:\n" +
                "1. [ModuleName].generated.cs (AUTO-GENERATED - DO NOT EDIT)\n" +
                "2. [ModuleName].cs (YOUR CUSTOM LOGIC GOES HERE)",
                MessageType.Info
            );
        }

        private void GenerateModule()
        {
            if (string.IsNullOrWhiteSpace(_moduleName))
            {
                EditorUtility.DisplayDialog("Error", "Module name cannot be empty!", "OK");
                return;
            }

            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            string generatedPath = Path.Combine(_outputFolder, $"{_moduleName}.generated.cs");
            string userPath = Path.Combine(_outputFolder, $"{_moduleName}.cs");

            // Generate the auto-generated partial class
            File.WriteAllText(generatedPath, GeneratePartialClass());
            
            // Generate the user-editable partial class (only if it doesn't exist)
            if (!File.Exists(userPath))
            {
                File.WriteAllText(userPath, GenerateUserClass());
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", 
                $"Module '{_moduleName}' generated!\n\n" +
                $"Generated: {generatedPath}\n" +
                $"User Logic: {userPath}", 
                "OK");
        }

        private string GeneratePartialClass()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("// AUTO-GENERATED FILE - DO NOT EDIT");
            sb.AppendLine("// Regenerate using RPG Tools > Atomic Module Generator");
            sb.AppendLine();
            sb.AppendLine("using Unity.Netcode;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using RPG.Core;");
            sb.AppendLine("using RPG.Contracts;");
            sb.AppendLine();
            sb.AppendLine("namespace RPG.Modules");
            sb.AppendLine("{");
            sb.AppendLine("    [RequireComponent(typeof(NetworkObject))]");
            sb.Append($"    public partial class {_moduleName} : BaseNetworkModule");

            // Add interfaces
            var interfaces = new System.Collections.Generic.List<string>();
            if (_implementIDamageable) interfaces.Add("IDamageable");
            if (_implementIResourcePool) interfaces.Add("IResourcePool");
            
            if (interfaces.Count > 0)
            {
                sb.Append($", {string.Join(", ", interfaces)}");
            }

            sb.AppendLine();
            sb.AppendLine("    {");

            // Generate NetworkVariables based on interfaces
            if (_implementIResourcePool)
            {
                sb.AppendLine("        private NetworkVariable<float> _currentValue = new NetworkVariable<float>(100f);");
                sb.AppendLine("        private NetworkVariable<float> _maxValue = new NetworkVariable<float>(100f);");
                sb.AppendLine();
            }

            if (_implementIDamageable)
            {
                sb.AppendLine("        private NetworkVariable<bool> _isDead = new NetworkVariable<bool>(false);");
                sb.AppendLine();
            }

            // Implement interface properties
            if (_implementIResourcePool)
            {
                sb.AppendLine("        public float CurrentValue => _currentValue.Value;");
                sb.AppendLine("        public float MaxValue => _maxValue.Value;");
                sb.AppendLine();
            }

            if (_implementIDamageable)
            {
                sb.AppendLine("        public bool IsDead => _isDead.Value;");
                sb.AppendLine();
            }

            // Generate stub methods
            if (_implementIDamageable)
            {
                sb.AppendLine("        public void TakeDamage(float amount, ulong attackerId)");
                sb.AppendLine("        {");
                sb.AppendLine("            // Implement in user partial class");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            if (_implementIResourcePool)
            {
                sb.AppendLine("        public void ModifyResource(float delta)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (!IsServer) return;");
                sb.AppendLine("            _currentValue.Value = Mathf.Clamp(_currentValue.Value + delta, 0, _maxValue.Value);");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        public void SetMaxValue(float newMax)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (!IsServer) return;");
                sb.AppendLine("            _maxValue.Value = Mathf.Max(0, newMax);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateUserClass()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("// USER EDITABLE FILE - Add your custom logic here");
            sb.AppendLine();
            sb.AppendLine("using Unity.Netcode;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace RPG.Modules");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {_moduleName}");
            sb.AppendLine("    {");
            sb.AppendLine("        // Add your custom fields, methods, and overrides here");
            sb.AppendLine();
            sb.AppendLine("        public override void OnModuleInitialized()");
            sb.AppendLine("        {");
            sb.AppendLine("            base.OnModuleInitialized();");
            sb.AppendLine("            // Your initialization logic");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override void OnModuleShutdown()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Your cleanup logic");
            sb.AppendLine("            base.OnModuleShutdown();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}