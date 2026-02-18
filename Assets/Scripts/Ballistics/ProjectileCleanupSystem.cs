using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct ProjectileCleanupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (projectile, entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
        {
            if (projectile.ValueRO.Lifetime <= 0f)
                ecb.DestroyEntity(entity);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
