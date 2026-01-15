#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace QuantumMechanic.Editor
{
    /// <summary>
    /// Automated build pipeline for creating builds across multiple platforms.
    /// </summary>
    public class BuildAutomation : EditorWindow
    {
        private BuildTarget selectedTarget = BuildTarget.StandaloneWindows64;
        private bool developmentBuild = true;
        private string buildPath = "Builds";

        [MenuItem("Tools/Quantum Mechanic/Build Automation", priority = 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildAutomation>("Build Automation");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("🏗️ Build Automation", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(10);
            
            selectedTarget = (BuildTarget)EditorGUILayout.EnumPopup("Target Platform", selectedTarget);
            developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);
            buildPath = EditorGUILayout.TextField("Build Path", buildPath);
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("🚀 Build Now", GUILayout.Height(40)))
                StartBuild();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Quick Build Presets:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Windows", GUILayout.Height(30)))
                QuickBuild(BuildTarget.StandaloneWindows64);
            
            if (GUILayout.Button("macOS", GUILayout.Height(30)))
                QuickBuild(BuildTarget.StandaloneOSX);
            
            if (GUILayout.Button("Linux", GUILayout.Height(30)))
                QuickBuild(BuildTarget.StandaloneLinux64);
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Android", GUILayout.Height(30)))
                QuickBuild(BuildTarget.Android);
            
            if (GUILayout.Button("iOS", GUILayout.Height(30)))
                QuickBuild(BuildTarget.iOS);
            
            if (GUILayout.Button("WebGL", GUILayout.Height(30)))
                QuickBuild(BuildTarget.WebGL);
            
            EditorGUILayout.EndHorizontal();
        }

        private void StartBuild()
        {
            string fullPath = Path.Combine(buildPath, GetBuildFileName());
            
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = fullPath,
                target = selectedTarget,
                options = developmentBuild ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build Successful", 
                    $"Build completed in {report.summary.totalTime.TotalSeconds:F1}s\nSize: {report.summary.totalSize / 1048576}MB", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Build Failed", 
                    $"Build failed with {report.summary.totalErrors} errors", 
                    "OK");
            }
        }

        private void QuickBuild(BuildTarget target)
        {
            selectedTarget = target;
            StartBuild();
        }

        private string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        private string GetBuildFileName()
        {
            string ext = selectedTarget switch
            {
                BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.Android => ".apk",
                BuildTarget.WebGL => "",
                _ => ""
            };
            return $"QuantumMechanic_{selectedTarget}{ext}";
        }
    }

    /// <summary>
    /// Scene validation tool to check for common issues before building.
    /// </summary>
    public class SceneValidator : EditorWindow
    {
        private Vector2 scrollPos;
        private List<ValidationIssue> issues = new List<ValidationIssue>();

        [MenuItem("Tools/Quantum Mechanic/Scene Validator", priority = 21)]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneValidator>("Scene Validator");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("✅ Scene Validator", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🔍 Validate Current Scene", GUILayout.Height(40)))
                ValidateScene();
            
            EditorGUILayout.Space(10);
            
            if (issues.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {issues.Count} issue(s):", EditorStyles.boldLabel);
                
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                
                foreach (var issue in issues)
                {
                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = GetIssueColor(issue.Severity);
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"{GetSeverityIcon(issue.Severity)} {issue.Message}");
                    EditorGUILayout.LabelField($"Object: {issue.ObjectName}", EditorStyles.miniLabel);
                    
                    if (GUILayout.Button("Select Object", GUILayout.Width(100)))
                        SelectObject(issue.ObjectName);
                    
                    EditorGUILayout.EndVertical();
                    
                    GUI.backgroundColor = oldColor;
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No issues found. Scene looks good!", MessageType.Info);
            }
        }

        private void ValidateScene()
        {
            issues.Clear();
            
            // Check for missing scripts
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                var components = obj.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Error,
                            Message = "Missing script reference",
                            ObjectName = obj.name
                        });
                    }
                }
            }
            
            // Check for null references in public fields
            // Check for missing audio sources
            // Check for disabled cameras
            // etc.
            
            Repaint();
        }

        private void SelectObject(string objectName)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null)
                Selection.activeGameObject = obj;
        }

        private Color GetIssueColor(IssueSeverity severity)
        {
            return severity switch
            {
                IssueSeverity.Error => new Color(1f, 0.5f, 0.5f),
                IssueSeverity.Warning => new Color(1f, 1f, 0.5f),
                _ => Color.white
            };
        }

        private string GetSeverityIcon(IssueSeverity severity)
        {
            return severity switch
            {
                IssueSeverity.Error => "❌",
                IssueSeverity.Warning => "⚠️",
                _ => "ℹ️"
            };
        }
    }

    /// <summary>
    /// Asset dependency analyzer to find unused or missing assets.
    /// </summary>
    public class AssetAnalyzer : EditorWindow
    {
        private Vector2 scrollPos;
        private List<string> unusedAssets = new List<string>();
        private long totalAssetSize = 0;

        [MenuItem("Tools/Quantum Mechanic/Asset Analyzer", priority = 22)]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetAnalyzer>("Asset Analyzer");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("📦 Asset Analyzer", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🔎 Analyze Project", GUILayout.Height(40)))
                AnalyzeAssets();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField($"Total Asset Size: {totalAssetSize / 1048576}MB");
            EditorGUILayout.LabelField($"Unused Assets: {unusedAssets.Count}");
            
            EditorGUILayout.Space(10);
            
            if (unusedAssets.Count > 0)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                
                foreach (var asset in unusedAssets)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(asset);
                    
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        SelectAsset(asset);
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }
        }

        private void AnalyzeAssets()
        {
            unusedAssets.Clear();
            totalAssetSize = 0;
            
            string[] allAssets = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/"))
                .ToArray();
            
            foreach (var asset in allAssets)
            {
                FileInfo file = new FileInfo(asset);
                if (file.Exists)
                    totalAssetSize += file.Length;
            }
            
            // Find unused assets (simplified - would need dependency analysis)
            EditorUtility.DisplayDialog("Analysis Complete", 
                $"Analyzed {allAssets.Length} assets\nTotal size: {totalAssetSize / 1048576}MB", 
                "OK");
        }

        private void SelectAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
                Selection.activeObject = asset;
        }
    }

    /// <summary>
    /// Final build checklist before shipping.
    /// </summary>
    public class BuildChecklist : EditorWindow
    {
        private Vector2 scrollPos;
        private Dictionary<string, bool> checklistItems = new Dictionary<string, bool>();

        [MenuItem("Tools/Quantum Mechanic/Build Checklist", priority = 23)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildChecklist>("Build Checklist");
            window.minSize = new Vector2(500, 500);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeChecklist();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("📋 Pre-Build Checklist", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(10);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            var keys = checklistItems.Keys.ToList();
            foreach (var item in keys)
            {
                checklistItems[item] = EditorGUILayout.ToggleLeft(item, checklistItems[item]);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            int completed = checklistItems.Values.Count(v => v);
            EditorGUILayout.LabelField($"Progress: {completed}/{checklistItems.Count}");
            
            if (completed == checklistItems.Count)
            {
                EditorGUILayout.HelpBox("✅ All checks complete! Ready to build.", MessageType.Info);
                
                if (GUILayout.Button("🚀 Proceed to Build", GUILayout.Height(40)))
                    BuildAutomation.ShowWindow();
            }
        }

        private void InitializeChecklist()
        {
            checklistItems = new Dictionary<string, bool>
            {
                {"✅ All scenes included in build settings", false},
                {"✅ Version number updated", false},
                {"✅ Debug logs disabled for release", false},
                {"✅ All compiler warnings resolved", false},
                {"✅ Performance tested on target hardware", false},
                {"✅ Save system tested", false},
                {"✅ All assets compressed", false},
                {"✅ Audio mixing finalized", false},
                {"✅ UI tested on all resolutions", false},
                {"✅ Localization complete", false},
                {"✅ Analytics configured", false},
                {"✅ Credits updated", false}
            };
        }
    }

    #region Data Structures

    public class ValidationIssue
    {
        public IssueSeverity Severity;
        public string Message;
        public string ObjectName;
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    #endregion
}
#endif