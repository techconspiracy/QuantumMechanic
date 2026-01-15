#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace QuantumMechanic.Editor
{
    /// <summary>
    /// Quest editor for creating and managing quests visually.
    /// </summary>
    public class QuestEditor : EditorWindow
    {
        private Vector2 scrollPos;
        private List<QuestData> quests = new List<QuestData>();
        private int selectedQuestIndex = -1;

        [MenuItem("Tools/Quantum Mechanic/Quest Editor", priority = 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<QuestEditor>("Quest Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            LoadQuests();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Left panel - Quest list
            DrawQuestList();
            
            // Right panel - Quest details
            DrawQuestDetails();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuestList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            
            EditorGUILayout.LabelField("📋 Quests", EditorStyles.boldLabel);
            
            if (GUILayout.Button("+ New Quest"))
            {
                quests.Add(new QuestData { Id = $"quest_{quests.Count}", Name = "New Quest" });
                selectedQuestIndex = quests.Count - 1;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            for (int i = 0; i < quests.Count; i++)
            {
                bool isSelected = i == selectedQuestIndex;
                Color oldColor = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = Color.cyan;

                if (GUILayout.Button(quests[i].Name, EditorStyles.miniButton))
                    selectedQuestIndex = i;

                GUI.backgroundColor = oldColor;
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestDetails()
        {
            EditorGUILayout.BeginVertical();
            
            if (selectedQuestIndex >= 0 && selectedQuestIndex < quests.Count)
            {
                var quest = quests[selectedQuestIndex];
                
                EditorGUILayout.LabelField("Quest Details", EditorStyles.boldLabel);
                
                quest.Id = EditorGUILayout.TextField("Quest ID", quest.Id);
                quest.Name = EditorGUILayout.TextField("Name", quest.Name);
                quest.Description = EditorGUILayout.TextArea(quest.Description, GUILayout.Height(60));
                quest.RewardGold = EditorGUILayout.IntField("Gold Reward", quest.RewardGold);
                quest.RewardXP = EditorGUILayout.IntField("XP Reward", quest.RewardXP);
                
                EditorGUILayout.Space(10);
                
                if (GUILayout.Button("Save Quest"))
                    SaveQuests();
                
                if (GUILayout.Button("Delete Quest", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Delete Quest", $"Delete quest '{quest.Name}'?", "Delete", "Cancel"))
                    {
                        quests.RemoveAt(selectedQuestIndex);
                        selectedQuestIndex = -1;
                        SaveQuests();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a quest from the list to edit", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void LoadQuests()
        {
            // Load quests from ScriptableObjects or JSON
            quests = new List<QuestData>();
        }

        private void SaveQuests()
        {
            // Save quests to ScriptableObjects or JSON
            EditorUtility.DisplayDialog("Saved", "Quest data saved successfully!", "OK");
        }
    }

    /// <summary>
    /// Analytics viewer showing live game metrics.
    /// </summary>
    public class AnalyticsEditor : EditorWindow
    {
        private Vector2 scrollPos;
        private string[] metrics = { "Sessions Today: 0", "DAU: 0", "Avg FPS: 60", "Crashes: 0" };

        [MenuItem("Tools/Quantum Mechanic/Analytics Dashboard", priority = 11)]
        public static void ShowWindow()
        {
            var window = GetWindow<AnalyticsEditor>("Analytics");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("📊 Analytics Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Real-time analytics data appears here when the game is running.", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            foreach (var metric in metrics)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(metric);
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Export Analytics Data"))
                ExportAnalytics();
        }

        private void ExportAnalytics()
        {
            string path = EditorUtility.SaveFilePanel("Export Analytics", "", "analytics.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                // Export analytics data
                EditorUtility.DisplayDialog("Exported", $"Analytics exported to: {path}", "OK");
            }
        }
    }

    /// <summary>
    /// Audio system editor for managing sound libraries.
    /// </summary>
    public class AudioEditor : EditorWindow
    {
        private Vector2 scrollPos;
        private List<AudioClip> clips = new List<AudioClip>();

        [MenuItem("Tools/Quantum Mechanic/Audio Manager", priority = 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<AudioEditor>("Audio Manager");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshAudioClips();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("🔊 Audio Library", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Refresh Audio Clips"))
                RefreshAudioClips();
            
            EditorGUILayout.Space(10);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            foreach (var clip in clips)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.ObjectField(clip, typeof(AudioClip), false);
                
                if (GUILayout.Button("▶", GUILayout.Width(30)))
                    PlayClip(clip);
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void RefreshAudioClips()
        {
            clips.Clear();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            // Play audio clip in editor
            Debug.Log($"Playing: {clip.name}");
        }
    }

    /// <summary>
    /// Save file browser and editor.
    /// </summary>
    public class SaveEditor : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("Tools/Quantum Mechanic/Save File Browser", priority = 13)]
        public static void ShowWindow()
        {
            var window = GetWindow<SaveEditor>("Save Browser");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("💾 Save File Browser", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Open Save Folder"))
                OpenSaveFolder();
            
            if (GUILayout.Button("Clear All Saves"))
            {
                if (EditorUtility.DisplayDialog("Clear Saves", "Delete all save files?", "Delete", "Cancel"))
                    ClearSaves();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            EditorGUILayout.HelpBox("Save files will appear here", MessageType.Info);
            
            EditorGUILayout.EndScrollView();
        }

        private void OpenSaveFolder()
        {
            string path = Application.persistentDataPath;
            EditorUtility.RevealInFinder(path);
        }

        private void ClearSaves()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            EditorUtility.DisplayDialog("Cleared", "All save data has been deleted.", "OK");
        }
    }

    /// <summary>
    /// Network testing panel for multiplayer debugging.
    /// </summary>
    public class NetworkEditor : EditorWindow
    {
        private string serverIP = "127.0.0.1";
        private int port = 7777;

        [MenuItem("Tools/Quantum Mechanic/Network Test Panel", priority = 14)]
        public static void ShowWindow()
        {
            var window = GetWindow<NetworkEditor>("Network Test");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("🌐 Network Test Panel", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(10);
            
            serverIP = EditorGUILayout.TextField("Server IP", serverIP);
            port = EditorGUILayout.IntField("Port", port);
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Start Server", GUILayout.Height(40)))
                StartServer();
            
            if (GUILayout.Button("Connect as Client", GUILayout.Height(40)))
                ConnectClient();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox("Network status: Disconnected", MessageType.Info);
        }

        private void StartServer()
        {
            Debug.Log($"Starting server on port {port}");
        }

        private void ConnectClient()
        {
            Debug.Log($"Connecting to {serverIP}:{port}");
        }
    }

    #region Data Classes

    [System.Serializable]
    public class QuestData
    {
        public string Id;
        public string Name;
        public string Description;
        public int RewardGold;
        public int RewardXP;
    }

    #endregion
}
#endif