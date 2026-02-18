using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct ProjectileMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = math.min(SystemAPI.Time.DeltaTime, 0.05f);
        foreach (var projectile in SystemAPI.Query<RefRW<Projectile>>())
        {
            projectile.ValueRW.Position += projectile.ValueRO.Velocity * dt;
            projectile.ValueRW.Velocity.y -= 9.81f * dt;
            projectile.ValueRW.Lifetime -= dt;
        }
    }
}
