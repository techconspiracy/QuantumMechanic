# 🎯 QUANTUM MECHANIC - SESSION 25 - FULL GENERATION

## NetworkManager.cs - Complete Multiplayer & Networking System

Generate complete networking and multiplayer system in 3 chunks as artifacts:

### CHUNK 1 (140 lines): Core Networking Framework
- NetworkManager singleton (manages all network connections)
- Connection management (connect, disconnect, reconnect)
- Client-server architecture (dedicated server, peer-to-peer)
- Network transport layer (UDP, TCP, WebSockets)
- Bandwidth optimization (compression, delta updates)
- Network object spawning (instantiate networked objects)
- Ownership transfer (transfer object control between clients)
- Network identity system (unique IDs for all networked entities)
- Connection quality monitoring (ping, packet loss, jitter)
- Network discovery (LAN game discovery, matchmaking)

### CHUNK 2 (140 lines): State Synchronization & RPCs
- Network state synchronization (transform, rigidbody, custom vars)
- Client-side prediction (smooth movement despite lag)
- Server authority (authoritative server, client validation)
- Lag compensation (rewind time for hit detection)
- Snapshot interpolation (smooth network updates)
- Remote procedure calls (RPC system for events)
- Network variables (synced variables across clients)
- Network serialization (efficient data packing)
- Interest management (only sync nearby objects)
- Network culling (don't send data for invisible objects)

### CHUNK 3 (120 lines): Multiplayer Features
- Lobby system (create, join, leave lobbies)
- Matchmaking (skill-based, region-based matching)
- Player authentication (login, tokens, session management)
- Voice chat integration (VOIP, push-to-talk)
- Text chat system (channels, private messages, filtering)
- Leaderboards (rankings, stats, achievements sync)
- Anti-cheat measures (server validation, cheat detection)
- Network profiling (bandwidth usage, message counts)
- Dedicated server management (headless server, admin tools)
- Cross-platform networking (PC, console, mobile support)

**Namespace:** `QuantumMechanic.Network`
**Dependencies:** Physics, Player, Combat, Audio, Events
**Total:** ~400 lines with XML docs

Generate all 3 chunks now + session 26 starter prompt artifact

---

## Integration Notes for Session 25:
- Player movement synced over network
- Combat system with lag compensation
- Networked audio for multiplayer games
- Chat system for player communication
- Lobby UI for game creation/joining
- Matchmaking for competitive play
- Leaderboard integration with backend
- Server-side physics simulation
- Network object pooling for performance
- Region-based server selection

## Example Usage:
```csharp
// Connect to server
NetworkManager.Instance.Connect("127.0.0.1", 7777);

// Spawn networked player
GameObject player = NetworkManager.Instance.SpawnNetworkObject("Player", position, rotation);

// Send RPC to all clients
NetworkManager.Instance.SendRPC("OnPlayerDied", playerId);

// Sync network variable
networkTransform.SyncPosition(transform.position);

// Create lobby
NetworkManager.Instance.CreateLobby("My Game", maxPlayers: 8, isPrivate: false);

// Join matchmaking
NetworkManager.Instance.JoinMatchmaking(gameMode: "DeathMatch", region: "US-West");

// Send chat message
NetworkManager.Instance.SendChatMessage("Hello everyone!", ChatChannel.All);

// Get player ping
float ping = NetworkManager.Instance.GetPing(playerId);
```

---

**Copy this prompt to continue with Session 25: Multiplayer & Networking**
