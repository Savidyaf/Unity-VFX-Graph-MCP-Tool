using Unity.Entities;
using Unity.Mathematics;

public struct Projectile : IComponentData
{
    public float3 Position;
    public float3 Velocity;
    public float Lifetime;
    public float MaxLifetime;
    public int Type; // 0=bullet, 1=missile
}

public struct GunEmplacement : IComponentData
{
    public float3 Position;
    public float3 AimDirection;
    public Entity TargetGun;
    public float FireRate;
    public float Accumulator;
    public int ProjectileType; // 0=bullet, 1=missile
    public int TeamId;         // 0=Team A, 1=Team B
    public float MinSpeed;
    public float MaxSpeed;
    public float SpreadAngle;
    public float ProjectileLifetime;
    public uint RandomSeed;
    public bool Enabled;
}
