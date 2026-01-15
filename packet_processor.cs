using System;
using System.Text;
using UnityEngine;

namespace QuantumMechanic.Networking
{
    /// <summary>
    /// Defines all network packet types for the Mini-MORPG protocol.
    /// </summary>
    public enum PacketType : byte
    {
        Connect = 1,
        Disconnect = 2,
        Spawn = 3,
        Despawn = 4,
        Transform = 5,
        Chat = 6,
        Action = 7,
        Economy = 8,
        KeepAlive = 9
    }
    
    /// <summary>
    /// Base packet structure for all network communications.
    /// Uses JsonUtility for serialization with size-prefixed binary framing.
    /// </summary>
    [Serializable]
    public class NetworkPacket
    {
        public byte packetType;
        public uint senderId;
        public long timestamp;
        public string payload;
        
        public NetworkPacket() { }
        
        public NetworkPacket(PacketType type, uint sender, string data)
        {
            packetType = (byte)type;
            senderId = sender;
            timestamp = DateTime.UtcNow.Ticks;
            payload = data;
        }
    }
    
    /// <summary>
    /// Transform synchronization payload.
    /// </summary>
    [Serializable]
    public class TransformData
    {
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;
        public float velX, velY, velZ;
        
        public TransformData() { }
        
        public TransformData(Vector3 pos, Quaternion rot, Vector3 vel)
        {
            posX = pos.x; posY = pos.y; posZ = pos.z;
            rotX = rot.x; rotY = rot.y; rotZ = rot.z; rotW = rot.w;
            velX = vel.x; velY = vel.y; velZ = vel.z;
        }
        
        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);
        public Vector3 GetVelocity() => new Vector3(velX, velY, velZ);
    }
    
    /// <summary>
    /// Chat message payload.
    /// </summary>
    [Serializable]
    public class ChatData
    {
        public string username;
        public string message;
        public byte channel; // 0=global, 1=whisper, 2=party
        
        public ChatData() { }
        
        public ChatData(string user, string msg, byte chan = 0)
        {
            username = user;
            message = msg;
            channel = chan;
        }
    }
    
    /// <summary>
    /// Spawn packet for new network entities.
    /// </summary>
    [Serializable]
    public class SpawnData
    {
        public uint networkId;
        public string prefabName;
        public float posX, posY, posZ;
        public bool isLocalPlayer;
        
        public SpawnData() { }
        
        public SpawnData(uint id, string prefab, Vector3 pos, bool isLocal)
        {
            networkId = id;
            prefabName = prefab;
            posX = pos.x; posY = pos.y; posZ = pos.z;
            isLocalPlayer = isLocal;
        }
        
        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
    }
    
    /// <summary>
    /// High-performance packet serialization and deserialization engine.
    /// Implements size-prefixed framing for TCP stream reassembly.
    /// </summary>
    public static class PacketProcessor
    {
        private const int HEADER_SIZE = 4; // 4 bytes for packet size
        
        /// <summary>
        /// Serializes a NetworkPacket into a size-prefixed byte array for TCP transmission.
        /// Format: [4 bytes length][JSON payload]
        /// </summary>
        public static byte[] Serialize(NetworkPacket packet)
        {
            try
            {
                string json = JsonUtility.ToJson(packet);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
                byte[] packetBytes = new byte[HEADER_SIZE + payloadBytes.Length];
                
                // Write length prefix (big-endian)
                packetBytes[0] = (byte)(payloadBytes.Length >> 24);
                packetBytes[1] = (byte)(payloadBytes.Length >> 16);
                packetBytes[2] = (byte)(payloadBytes.Length >> 8);
                packetBytes[3] = (byte)(payloadBytes.Length);
                
                // Copy payload
                Buffer.BlockCopy(payloadBytes, 0, packetBytes, HEADER_SIZE, payloadBytes.Length);
                
                return packetBytes;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PacketProcessor] Serialization failed: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Deserializes a JSON payload into a NetworkPacket.
        /// </summary>
        public static NetworkPacket Deserialize(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                return JsonUtility.FromJson<NetworkPacket>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PacketProcessor] Deserialization failed: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Reads the packet length from a 4-byte header.
        /// </summary>
        public static int ReadPacketLength(byte[] header)
        {
            if (header.Length < HEADER_SIZE) return -1;
            return (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
        }
        
        /// <summary>
        /// Creates a transform packet from position/rotation data.
        /// </summary>
        public static NetworkPacket CreateTransformPacket(uint senderId, Vector3 pos, Quaternion rot, Vector3 vel)
        {
            TransformData data = new TransformData(pos, rot, vel);
            string payload = JsonUtility.ToJson(data);
            return new NetworkPacket(PacketType.Transform, senderId, payload);
        }
        
        /// <summary>
        /// Creates a chat packet.
        /// </summary>
        public static NetworkPacket CreateChatPacket(uint senderId, string username, string message, byte channel = 0)
        {
            ChatData data = new ChatData(username, message, channel);
            string payload = JsonUtility.ToJson(data);
            return new NetworkPacket(PacketType.Chat, senderId, payload);
        }
        
        /// <summary>
        /// Creates a spawn packet for network entity instantiation.
        /// </summary>
        public static NetworkPacket CreateSpawnPacket(uint networkId, string prefabName, Vector3 position, bool isLocal)
        {
            SpawnData data = new SpawnData(networkId, prefabName, position, isLocal);
            string payload = JsonUtility.ToJson(data);
            return new NetworkPacket(PacketType.Spawn, networkId, payload);
        }
        
        /// <summary>
        /// Parses transform data from packet payload.
        /// </summary>
        public static TransformData ParseTransformData(string payload)
        {
            try
            {
                return JsonUtility.FromJson<TransformData>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PacketProcessor] Failed to parse TransformData: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Parses chat data from packet payload.
        /// </summary>
        public static ChatData ParseChatData(string payload)
        {
            try
            {
                return JsonUtility.FromJson<ChatData>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PacketProcessor] Failed to parse ChatData: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Parses spawn data from packet payload.
        /// </summary>
        public static SpawnData ParseSpawnData(string payload)
        {
            try
            {
                return JsonUtility.FromJson<SpawnData>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PacketProcessor] Failed to parse SpawnData: {ex.Message}");
                return null;
            }
        }
    }
}