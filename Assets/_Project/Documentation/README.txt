QUANTUM MECHANIC FRAMEWORK
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
