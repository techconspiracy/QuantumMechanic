using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Networking
{
    /// <summary>
    /// Voice chat system with proximity and channel support
    /// </summary>
    public class VoiceChat : MonoBehaviour
    {
        private static VoiceChat _instance;
        public static VoiceChat Instance => _instance;

        public enum VoiceChannel { Global, Team, Proximity, Whisper }

        [SerializeField] private bool voiceEnabled = false;
        [SerializeField] private VoiceChannel currentChannel = VoiceChannel.Global;
        [SerializeField] private float proximityRange = 50f;
        [SerializeField] private bool spatialAudio = true;

        private void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
        }

        public void EnableVoiceChat(bool enable)
        {
            voiceEnabled = enable;
            Debug.Log($"[VoiceChat] Voice chat {(enable ? "enabled" : "disabled")}");
        }

        public void SetChannel(VoiceChannel channel)
        {
            currentChannel = channel;
            Debug.Log($"[VoiceChat] Switched to channel: {channel}");
        }

        public void SendVoiceData(byte[] audioData, string targetPlayerId = null)
        {
            if (!voiceEnabled) return;

            switch (currentChannel)
            {
                case VoiceChannel.Global:
                    BroadcastVoice(audioData);
                    break;
                case VoiceChannel.Team:
                    SendToTeam(audioData);
                    break;
                case VoiceChannel.Proximity:
                    SendToNearbyPlayers(audioData);
                    break;
                case VoiceChannel.Whisper:
                    SendToPlayer(audioData, targetPlayerId);
                    break;
            }
        }

        private void BroadcastVoice(byte[] data) { /* Send to all */ }
        private void SendToTeam(byte[] data) { /* Send to team members */ }
        private void SendToNearbyPlayers(byte[] data) { /* Send to players within range */ }
        private void SendToPlayer(byte[] data, string playerId) { /* Send to specific player */ }
    }

    /// <summary>
    /// Text chat system with multiple channels
    /// </summary>
    public class TextChat : MonoBehaviour
    {
        private static TextChat _instance;
        public static TextChat Instance => _instance;

        public enum ChatChannel { Global, Team, Whisper, System }

        public event Action<ChatMessage> OnMessageReceived;

        private List<ChatMessage> chatHistory = new List<ChatMessage>();
        private HashSet<string> profanityFilter = new HashSet<string>();

        private void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            LoadProfanityFilter();
        }

        public void SendMessage(string message, ChatChannel channel, string targetPlayer = null)
        {
            // Filter profanity
            message = FilterProfanity(message);

            ChatMessage chatMsg = new ChatMessage
            {
                sender = NetworkManager.Instance.IsHost ? "Host" : "Player",
                message = message,
                channel = channel,
                timestamp = DateTime.Now
            };

            chatHistory.Add(chatMsg);
            BroadcastMessage(chatMsg, targetPlayer);
            OnMessageReceived?.Invoke(chatMsg);
        }

        private string FilterProfanity(string text)
        {
            foreach (var word in profanityFilter)
            {
                text = text.Replace(word, new string('*', word.Length));
            }
            return text;
        }

        private void BroadcastMessage(ChatMessage msg, string targetPlayer) { /* Send message */ }
        private void LoadProfanityFilter() { /* Load filter words */ }
    }

    public struct ChatMessage
    {
        public string sender;
        public string message;
        public TextChat.ChatChannel channel;
        public DateTime timestamp;
    }

    /// <summary>
    /// Manages teams and team assignment
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        private Dictionary<int, List<string>> teams = new Dictionary<int, List<string>>();
        private Dictionary<string, int> playerTeams = new Dictionary<string, int>();

        public void AssignPlayerToTeam(string playerId, int teamId)
        {
            // Remove from old team
            if (playerTeams.TryGetValue(playerId, out int oldTeam))
            {
                teams[oldTeam].Remove(playerId);
            }

            // Add to new team
            if (!teams.ContainsKey(teamId))
                teams[teamId] = new List<string>();

            teams[teamId].Add(playerId);
            playerTeams[playerId] = teamId;

            Debug.Log($"[TeamManager] Player {playerId} assigned to team {teamId}");
        }

        public void BalanceTeams()
        {
            // Auto-balance teams by skill/count
            Debug.Log("[TeamManager] Balancing teams...");
        }

        public List<string> GetTeamMembers(int teamId)
        {
            return teams.TryGetValue(teamId, out var members) ? members : new List<string>();
        }

        public int GetPlayerTeam(string playerId)
        {
            return playerTeams.TryGetValue(playerId, out int team) ? team : -1;
        }
    }

    /// <summary>
    /// Spectator mode for observing matches
    /// </summary>
    public class SpectatorMode : MonoBehaviour
    {
        public enum SpectatorMode { FreeCam, FollowPlayer, FirstPerson }

        [SerializeField] private SpectatorMode currentMode = SpectatorMode.FreeCam;
        [SerializeField] private string followingPlayerId;
        [SerializeField] private float freeCamSpeed = 10f;

        private Camera spectatorCamera;

        public void EnableSpectator(bool enable)
        {
            if (enable)
            {
                SetupSpectatorCamera();
                Debug.Log("[SpectatorMode] Spectator mode enabled");
            }
        }

        public void SetMode(SpectatorMode mode)
        {
            currentMode = mode;
        }

        public void FollowPlayer(string playerId)
        {
            followingPlayerId = playerId;
            currentMode = SpectatorMode.FollowPlayer;
        }

        private void SetupSpectatorCamera()
        {
            if (spectatorCamera == null)
            {
                GameObject camObj = new GameObject("SpectatorCamera");
                spectatorCamera = camObj.AddComponent<Camera>();
            }
        }

        private void Update()
        {
            if (currentMode == SpectatorMode.FreeCam)
            {
                HandleFreeCamMovement();
            }
        }

        private void HandleFreeCamMovement()
        {
            // Free camera controls
        }
    }

    /// <summary>
    /// Records and plays back matches
    /// </summary>
    public class ReplayRecorder : MonoBehaviour
    {
        private List<ReplayFrame> recordedFrames = new List<ReplayFrame>();
        private bool isRecording = false;
        private bool isPlaying = false;
        private float playbackSpeed = 1f;

        private struct ReplayFrame
        {
            public float timestamp;
            public Dictionary<string, TransformData> entities;
        }

        private struct TransformData
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        public void StartRecording()
        {
            isRecording = true;
            recordedFrames.Clear();
            Debug.Log("[ReplayRecorder] Started recording");
        }

        public void StopRecording()
        {
            isRecording = false;
            Debug.Log($"[ReplayRecorder] Stopped recording. Frames: {recordedFrames.Count}");
        }

        public void PlayRecording()
        {
            if (recordedFrames.Count == 0) return;
            isPlaying = true;
            Debug.Log("[ReplayRecorder] Playing recording");
        }

        public void SaveReplay(string filename)
        {
            // Save replay to file
            Debug.Log($"[ReplayRecorder] Saved replay: {filename}");
        }

        public void LoadReplay(string filename)
        {
            // Load replay from file
            Debug.Log($"[ReplayRecorder] Loaded replay: {filename}");
        }
    }

    /// <summary>
    /// Anti-cheat system for detecting anomalies
    /// </summary>
    public class AntiCheat : MonoBehaviour
    {
        private Dictionary<string, PlayerValidation> playerValidations = new Dictionary<string, PlayerValidation>();

        private struct PlayerValidation
        {
            public int suspicionLevel;
            public List<string> flaggedActions;
            public float lastValidationTime;
        }

        public void ValidatePlayerAction(string playerId, string action, object data)
        {
            // Check if action is suspicious
            bool isSuspicious = DetectAnomaly(playerId, action, data);

            if (isSuspicious)
            {
                FlagPlayer(playerId, action);
            }
        }

        private bool DetectAnomaly(string playerId, string action, object data)
        {
            // Implement anomaly detection logic
            // Check for: impossible movements, rapid fire, wallhacks, etc.
            return false;
        }

        private void FlagPlayer(string playerId, string reason)
        {
            if (!playerValidations.ContainsKey(playerId))
            {
                playerValidations[playerId] = new PlayerValidation
                {
                    flaggedActions = new List<string>()
                };
            }

            var validation = playerValidations[playerId];
            validation.suspicionLevel++;
            validation.flaggedActions.Add(reason);
            playerValidations[playerId] = validation;

            Debug.LogWarning($"[AntiCheat] Player {playerId} flagged for: {reason}");

            if (validation.suspicionLevel >= 5)
            {
                KickPlayer(playerId);
            }
        }

        private void KickPlayer(string playerId)
        {
            Debug.LogWarning($"[AntiCheat] Kicking player {playerId} for cheating");
        }
    }
}