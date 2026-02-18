using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// Custom bootstrap that creates a minimal ECS world with only the ballistics systems.
/// This avoids a hang caused by unknown built-in ECS systems in the default world.
/// </summary>
public class BallisticsBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world = new World(defaultWorldName, WorldFlags.Game);
        World.DefaultGameObjectInjectionWorld = world;

        // Only create our three ballistics systems
        var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

        // Required built-in systems for time management
        var timeSystem = world.GetOrCreateSystem<UpdateWorldTimeSystem>();
        simGroup.AddSystemToUpdateList(timeSystem);

        // Our custom systems in execution order
        var spawnSystem = world.GetOrCreateSystem<ProjectileSpawnSystem>();
        simGroup.AddSystemToUpdateList(spawnSystem);

        var moveSystem = world.GetOrCreateSystem<ProjectileMoveSystem>();
        simGroup.AddSystemToUpdateList(moveSystem);

        var cleanupSystem = world.GetOrCreateSystem<ProjectileCleanupSystem>();
        simGroup.AddSystemToUpdateList(cleanupSystem);

        simGroup.SortSystems();

        // Register the world's update in the player loop
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

        return true; // We handled world creation
    }
}
