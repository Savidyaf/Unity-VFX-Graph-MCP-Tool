using Unity.Mathematics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct ProjectileRenderData
{
    public float3 Position;
    public float3 Velocity;
    public float Life;
    public float Type;
}