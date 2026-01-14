# 🎯 QUANTUM MECHANIC - SESSION 26 - FULL GENERATION

## SaveSystem.cs - Complete Save/Load & Persistence System

Generate complete save/load and data persistence system in 3 chunks as artifacts:

### CHUNK 1 (140 lines): Core Save System
- SaveManager singleton (manages all save operations)
- Save file management (create, load, delete, list saves)
- Multiple save slots (quick save, auto save, manual saves)
- Save data serialization (JSON, binary, encrypted)
- Compression (reduce save file sizes)
- Save validation (checksum, corruption detection)
- Cloud save integration (sync with cloud services)
- Save file versioning (handle save format updates)
- Save metadata (timestamps, playtime, level info)
- Platform-specific paths (Windows, Mac, Linux, Console)

### CHUNK 2 (140 lines): Data Persistence
- Component save system (save any component data)
- Scene persistence (save/load scene states)
- Player data (inventory, stats, progress, settings)
- World state (object positions, quest states, flags)
- Incremental saves (only save changed data)
- Save callbacks (pre-save, post-load hooks)
- Custom serialization (serialize custom types)
- Reference tracking (maintain object references)
- Save profiles (multiple player profiles)
- Save migration (upgrade old save formats)

### CHUNK 3 (120 lines): Advanced Features
- Auto-save system (periodic saves, checkpoint saves)
- Save backup (maintain multiple backup copies)
- Achievement tracking (unlock tracking, sync)
- Analytics integration (track player progression)
- Save encryption (prevent save file tampering)
- Steam Cloud integration (Steam API saves)
- Console platform saves (PS, Xbox, Switch)
- Cross-platform saves (save compatibility)
- Save debugging (inspect save data, fix corruption)
- Performance profiling (measure save/load times)

**Namespace:** `QuantumMechanic.Persistence`
**Dependencies:** Player, Inventory, Quest, Settings, Events
**Total:** ~400 lines with XML docs

Generate all 3 chunks now + session 27 starter prompt artifact

---

## Integration Notes for Session 26:
- Player inventory saved automatically
- Quest progress persisted between sessions
- Settings synced across devices
- Auto-save on level completion
- Cloud save for cross-device play
- Achievement unlock tracking
- Scene state restoration
- Save slot management UI
- Backup saves for safety
- Save file migration for updates

## Example Usage:
```csharp
// Quick save
SaveSystem.Instance.QuickSave();

// Save to specific slot
SaveSystem.Instance.SaveGame(slotIndex: 1, saveName: "Before Boss Fight");

// Load game
SaveSystem.Instance.LoadGame(slotIndex: 1);

// Auto-save checkpoint
SaveSystem.Instance.AutoSave("checkpoint_level3");

// Save player data
SaveSystem.Instance.SavePlayerData(playerController.GetSaveData());

// Save world state
SaveSystem.Instance.SaveWorldState(worldManager.GetWorldState());

// Enable cloud sync
SaveSystem.Instance.EnableCloudSync(true);

// List all saves
SaveSlot[] slots = SaveSystem.Instance.GetSaveSlots();

// Delete save
SaveSystem.Instance.DeleteSave(slotIndex: 2);

// Check for cloud saves
SaveSystem.Instance.SyncWithCloud();
```

---

**Copy this prompt to continue with Session 26: Save/Load & Persistence**
