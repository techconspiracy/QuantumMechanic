---
description: Repository Information Overview
alwaysApply: true
---

# Quantum Mechanic Information

## Summary
Quantum Mechanic is a modular, session-persistent development system for building a **Mini-MORPG** in Unity. The project follows an "agentic readability" philosophy, utilizing self-contained C# artifacts that integrate into a cohesive game framework. It features custom TCP networking, an AES-encrypted save system, procedural model generation, and a server-authoritative combat and damage system.

## Structure
- **Root**: Contains the core C# scripts for the game's primary systems (Networking, Combat, Economy, Persistence).
- **Prompts.DevNotes/**: Contains documentation regarding the project's roadmap, implementation framework, and technical specifications.
- **.zencoder/workflows/**: Automation and development workflows.
- **.zenflow/workflows/**: Custom flow definitions for project management and execution.

## Language & Runtime
**Language**: C#  
**Runtime**: Unity (Universal Render Pipeline - URP)  
**Build System**: Unity Editor  
**Package Manager**: Unity Package Manager (implied by URP and Unity-standard APIs)

## Dependencies
**Main Dependencies**:
- **UnityEngine**: Core game engine functionality.
- **UnityEditor**: Custom editor tools and project bootstrapping.
- **System.Net.Sockets**: TCP-based network communication.
- **System.Security.Cryptography**: AES-256-CBC encryption for secure data persistence.
- **System.Text.Json / JsonUtility**: Serialization and packet processing.

## Build & Installation
1.  **Open Project**: Load the repository folder as a new Unity project.
2.  **Initialize**: Navigate to the Unity top menu and select `Project > Initialize Masterpiece`. This executes `ProjectBootstrapper.cs`, which generates the `Assets/_QuantumMechanic` folder structure, materials, prefabs, and the main scene.
3.  **Run**: Open `Assets/_QuantumMechanic/Scenes/Main.unity` and press Play.

## Main Files & Resources
- **project_bootstrapper.cs**: The entry point for project setup, automating folder creation and asset generation.
- **server_host.cs / client_manager.cs**: Core networking components handling TCP connections and multi-threaded packet queues.
- **damage_system.cs**: Server-authoritative logic for combat math, resistances, and health management.
- **save_system.cs**: Handles AES-encrypted serialization of `PlayerData` to `Application.persistentDataPath`.
- **combat_02_weapon_controller.cs**: Manages client-side weapon prediction and server-side hit validation.
- **procedural_model_factory.cs**: Generates meshes and models programmatically to reduce external asset reliance.

## Testing & Validation
**Testing Approach**: 
The project utilizes a **Modular Implementation Framework** where each script is a self-contained module. Verification is primarily performed through:
- **Unity Editor Initialization**: Bootstrapping ensures all internal links (Prefabs, Materials) are correctly hooked.
- **Server Validation**: The `DamageSystem` and `WeaponController` include server-side anti-cheat and validation logic for every action (fire rate, ammo, hit detection).
- **Log-Based Debugging**: Extensive logging in `NetworkIdentity`, `SaveSystem`, and `ServerHost` for runtime state tracking.

**Run Command**:
Currently, the systems are validated by running the Unity Editor and using the `Initialize Masterpiece` command followed by entering Play Mode to observe the `GameNetworkManager` console output.
