using UnityEngine;
using UnityEngine.VFX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public class ProjectileVFXBridge : MonoBehaviour
{
    [Header("VFX References")]
    public VisualEffect tracerVFX;
    public VisualEffect trailVFX;

    [Header("Auto-find by name (used if references are null)")]
    public string tracerObjectName = "BulletTracerVFX";
    public string trailObjectName = "MissileTrailVFX";

    [Header("Buffer Settings")]
    public int maxProjectiles = 65536;

    private GraphicsBuffer _tracerBuffer;
    private GraphicsBuffer _trailBuffer;
    private NativeArray<ProjectileRenderData> _cpuBuffer;
    private EntityQuery _projectileQuery;
    private bool _queryCreated;

    private static readonly int ProjectileBufferID = Shader.PropertyToID("ProjectileBuffer");
    private static readonly int ProjectileCountID = Shader.PropertyToID("ProjectileCount");

    void OnEnable()
    {
        if (tracerVFX == null && !string.IsNullOrEmpty(tracerObjectName))
        {
            var go = GameObject.Find(tracerObjectName);
            if (go != null) tracerVFX = go.GetComponent<VisualEffect>();
        }
        if (trailVFX == null && !string.IsNullOrEmpty(trailObjectName))
        {
            var go = GameObject.Find(trailObjectName);
            if (go != null) trailVFX = go.GetComponent<VisualEffect>();
        }

        int stride = System.Runtime.InteropServices.Marshal.SizeOf<ProjectileRenderData>();
        _tracerBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxProjectiles, stride);
        _trailBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxProjectiles, stride);
        _cpuBuffer = new NativeArray<ProjectileRenderData>(maxProjectiles, Allocator.Persistent);
    }

    void LateUpdate()
    {
        if (Time.frameCount <= 5)
            Debug.Log($"[VFXBridge] LateUpdate START frame={Time.frameCount}");

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        if (!_queryCreated)
        {
            _projectileQuery = world.EntityManager.CreateEntityQuery(typeof(Projectile));
            _queryCreated = true;
        }

        var projectiles = _projectileQuery.ToComponentDataArray<Projectile>(Allocator.Temp);
        int count = math.min(projectiles.Length, maxProjectiles);

        int bulletCount = 0;
        for (int i = 0; i < count; i++)
        {
            var p = projectiles[i];
            if (p.Type == 0)
            {
                _cpuBuffer[bulletCount++] = new ProjectileRenderData
                {
                    Position = p.Position,
                    Velocity = p.Velocity,
                    Life = math.saturate(p.Lifetime / math.max(p.MaxLifetime, 0.001f)),
                    Type = 0
                };
            }
        }

        if (bulletCount > 0)
            _tracerBuffer.SetData(_cpuBuffer, 0, 0, bulletCount);

        if (tracerVFX != null && tracerVFX.HasGraphicsBuffer(ProjectileBufferID))
        {
            tracerVFX.SetGraphicsBuffer(ProjectileBufferID, _tracerBuffer);
            if (tracerVFX.HasInt(ProjectileCountID))
                tracerVFX.SetInt(ProjectileCountID, bulletCount);
        }

        int missileCount = 0;
        for (int i = 0; i < count; i++)
        {
            var p = projectiles[i];
            if (p.Type == 1)
            {
                _cpuBuffer[missileCount++] = new ProjectileRenderData
                {
                    Position = p.Position,
                    Velocity = p.Velocity,
                    Life = math.saturate(p.Lifetime / math.max(p.MaxLifetime, 0.001f)),
                    Type = 1
                };
            }
        }

        if (missileCount > 0)
            _trailBuffer.SetData(_cpuBuffer, 0, 0, missileCount);

        if (trailVFX != null && trailVFX.HasGraphicsBuffer(ProjectileBufferID))
        {
            trailVFX.SetGraphicsBuffer(ProjectileBufferID, _trailBuffer);
            if (trailVFX.HasInt(ProjectileCountID))
                trailVFX.SetInt(ProjectileCountID, missileCount);
        }

        projectiles.Dispose();

        if (Time.frameCount <= 5)
            Debug.Log($"[VFXBridge] LateUpdate END frame={Time.frameCount} bullets={bulletCount} missiles={missileCount}");
    }

    void OnDisable()
    {
        _tracerBuffer?.Release();
        _tracerBuffer = null;
        _trailBuffer?.Release();
        _trailBuffer = null;
        if (_cpuBuffer.IsCreated) _cpuBuffer.Dispose();
        _queryCreated = false;
    }
}