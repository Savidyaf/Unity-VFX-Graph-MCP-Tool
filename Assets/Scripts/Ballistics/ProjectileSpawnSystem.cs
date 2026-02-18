using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct ProjectileSpawnSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = math.min(SystemAPI.Time.DeltaTime, 0.05f);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (gun, gunEntity) in SystemAPI.Query<RefRW<GunEmplacement>>().WithEntityAccess())
        {
            if (!gun.ValueRO.Enabled)
                continue;

            gun.ValueRW.Accumulator += gun.ValueRO.FireRate * dt;

            int toSpawn = math.min((int)gun.ValueRO.Accumulator, 5);
            if (toSpawn <= 0) continue;

            gun.ValueRW.Accumulator -= toSpawn;

            uint seed = gun.ValueRO.RandomSeed;
            if (seed == 0) seed = 1;
            var random = Random.CreateFromIndex(seed);
            gun.ValueRW.RandomSeed = random.NextUInt(1, uint.MaxValue);

            float3 origin = gun.ValueRO.Position;
            float3 aimDir = gun.ValueRO.AimDirection;
            float spread = gun.ValueRO.SpreadAngle;
            float minSpd = gun.ValueRO.MinSpeed;
            float maxSpd = gun.ValueRO.MaxSpeed;
            float life = gun.ValueRO.ProjectileLifetime;
            int type = gun.ValueRO.ProjectileType;

            for (int i = 0; i < toSpawn; i++)
            {
                float3 deviation = new float3(
                    random.NextFloat(-spread, spread),
                    random.NextFloat(-spread * 0.3f, spread * 0.3f),
                    random.NextFloat(-spread, spread)
                );
                float3 dir = math.normalize(aimDir + deviation);
                float speed = random.NextFloat(minSpd, maxSpd);

                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new Projectile
                {
                    Position = origin,
                    Velocity = dir * speed,
                    Lifetime = life,
                    MaxLifetime = life,
                    Type = type
                });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
