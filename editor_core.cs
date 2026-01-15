#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace QuantumMechanic.Editor
{
    /// <summary>
    /// Main editor dashboard providing overview of all Quantum Mechanic systems.
    /// Access via: Tools > Quantum Mechanic > Dashboard
    /// </summary>
    public class QuantumDashboard : EditorWindow
    {
        private Vector2 scrollPos;
        private SystemStatus[] systemStatuses;
        private GUIStyle headerStyle;
        private GUIStyle statusStyle;

        [MenuItem("Tools/Quantum Mechanic/Dashboard ⚡", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<QuantumDashboard>("QM Dashboard");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSystemStatuses();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnGUI()
        {
            InitializeStyles();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ QUANTUM MECHANIC DASHBOARD", headerStyle);
            EditorGUILayout.LabelField($"Unity {Application.unityVersion} | {DateTime.Now:HH:mm:ss}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            DrawQuickActions();
            
            EditorGUILayout.Space(10);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawSystemStatuses();
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            DrawBottomToolbar();
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🎮 Play", GUILayout.Height(30)))
                EditorApplication.isPlaying = !EditorApplication.isPlaying;
            
            if (GUILayout.Button("💾 Save All", GUILayout.Height(30)))
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
            }
            
            if (GUILayout.Button("🔧 Settings", GUILayout.Height(30)))
                QuantumSettings.ShowWindow();
            
            if (GUILayout.Button("📊 Analytics", GUILayout.Height(30)))
                AnalyticsEditor.ShowWindow();
            
            if (GUILayout.Button("🎯 Quests", GUILayout.Height(30)))
                QuestEditor.ShowWindow();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSystemStatuses()
        {
            EditorGUILayout.LabelField("System Status", EditorStyles.boldLabel);
            
            if (systemStatuses == null || systemStatuses.Length == 0)
            {
                EditorGUILayout.HelpBox("No systems detected. Systems will appear here when the game is running.", MessageType.Info);
                return;
            }

            foreach (var status in systemStatuses)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                // Status icon
                string icon = status.IsHealthy ? "✅" : "⚠️";
                EditorGUILayout.LabelField(icon, GUILayout.Width(30));
                
                // System name
                EditorGUILayout.LabelField(status.SystemName, EditorStyles.boldLabel, GUILayout.Width(150));
                
                // Status text
                Color oldColor = GUI.color;
                GUI.color = status.IsHealthy ? Color.green : Color.yellow;
                EditorGUILayout.LabelField(status.Status, statusStyle);
                GUI.color = oldColor;
                
                // Open editor button
                if (GUILayout.Button("Open", GUILayout.Width(60)))
                    status.OpenEditor?.Invoke();
                
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBottomToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton))
                RefreshSystemStatuses();
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"Systems: {systemStatuses?.Length ?? 0}", EditorStyles.toolbarButton);
            
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshSystemStatuses()
        {
            var statuses = new List<SystemStatus>();

            // Check each system
            statuses.Add(new SystemStatus
            {
                SystemName = "Audio System",
                IsHealthy = CheckSystemExists("AudioManager"),
                Status = GetSystemStatus("AudioManager"),
                OpenEditor = () => AudioEditor.ShowWindow()
            });

            statuses.Add(new SystemStatus
            {
                SystemName = "Save System",
                IsHealthy = CheckSystemExists("SaveManager"),
                Status = GetSystemStatus("SaveManager"),
                OpenEditor = () => SaveEditor.ShowWindow()
            });

            statuses.Add(new SystemStatus
            {
                SystemName = "Quest System",
                IsHealthy = CheckSystemExists("QuestManager"),
                Status = GetSystemStatus("QuestManager"),
                OpenEditor = () => QuestEditor.ShowWindow()
            });

            statuses.Add(new SystemStatus
            {
                SystemName = "Analytics",
                IsHealthy = CheckSystemExists("AnalyticsManager"),
                Status = GetSystemStatus("AnalyticsManager"),
                OpenEditor = () => AnalyticsEditor.ShowWindow()
            });

            statuses.Add(new SystemStatus
            {
                SystemName = "Networking",
                IsHealthy = CheckSystemExists("NetworkManager"),
                Status = GetSystemStatus("NetworkManager"),
                OpenEditor = () => NetworkEditor.ShowWindow()
            });

            systemStatuses = statuses.ToArray();
        }

        private bool CheckSystemExists(string systemName)
        {
            if (!Application.isPlaying) return false;
            return FindObjectOfType(Type.GetType($"QuantumMechanic.{systemName}")) != null;
        }

        private string GetSystemStatus(string systemName)
        {
            if (!Application.isPlaying) return "Not Running";
            return CheckSystemExists(systemName) ? "Active" : "Missing";
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
                RefreshSystemStatuses();
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic
                };
            }
        }
    }

    /// <summary>
    /// Global settings window for Quantum Mechanic project configuration.
    /// </summary>
    public class QuantumSettings : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("Tools/Quantum Mechanic/Settings", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<QuantumSettings>("QM Settings");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Quantum Mechanic Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawGeneralSettings();
            DrawDebugSettings();
            DrawPerformanceSettings();

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Save Settings"))
                SaveSettings();
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorPrefs.SetString("QM_ProjectName", EditorGUILayout.TextField("Project Name", 
                EditorPrefs.GetString("QM_ProjectName", "Quantum Mechanic")));
            
            EditorPrefs.SetBool("QM_AutoSave", EditorGUILayout.Toggle("Auto Save", 
                EditorPrefs.GetBool("QM_AutoSave", true)));
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);
        }

        private void DrawDebugSettings()
        {
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorPrefs.SetBool("QM_DebugMode", EditorGUILayout.Toggle("Debug Mode", 
                EditorPrefs.GetBool("QM_DebugMode", true)));
            
            EditorPrefs.SetBool("QM_VerboseLogging", EditorGUILayout.Toggle("Verbose Logging", 
                EditorPrefs.GetBool("QM_VerboseLogging", false)));
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);
        }

        private void DrawPerformanceSettings()
        {
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorPrefs.SetInt("QM_TargetFPS", EditorGUILayout.IntSlider("Target FPS", 
                EditorPrefs.GetInt("QM_TargetFPS", 60), 30, 144));
            
            EditorGUI.indentLevel--;
        }

        private void SaveSettings()
        {
            EditorUtility.DisplayDialog("Settings Saved", "Quantum Mechanic settings have been saved.", "OK");
        }
    }

    #region Data Structures

    public class SystemStatus
    {
        public string SystemName;
        public bool IsHealthy;
        public string Status;
        public Action OpenEditor;
    }

    #endregion
}
#endif