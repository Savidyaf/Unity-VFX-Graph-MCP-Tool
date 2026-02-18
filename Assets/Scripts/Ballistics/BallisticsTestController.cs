using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class BallisticsTestController : MonoBehaviour
{
    [Header("Battlefield Layout")]
    public int gunsPerTeam = 100;
    public float teamSpacing = 100f;
    public float gunSpacing = 2f;

    [Header("Fire Rates (per gun per second)")]
    public float bulletFireRate = 30f;
    public float missileFireRate = 5f;
    [Range(0f, 1f)]
    public float missileGunFraction = 0.3f;

    [Header("Projectile Settings")]
    public float bulletSpeed = 200f;
    public float missileSpeed = 80f;
    public float spreadAngle = 0.05f;
    public float projectileLifetime = 3f;

    [Header("State")]
    public bool spawning = true;
    public float fireRateMultiplier = 1f;

    private NativeArray<Entity> _teamAGuns;
    private NativeArray<Entity> _teamBGuns;
    private bool _initialized;
    private float _fpsTimer;
    private int _fpsCount;
    private float _displayFps;
    private int _lastBulletCount;
    private int _lastMissileCount;
    private int _lastTotalCount;

    void OnEnable()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 40f, -60f);
            cam.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            cam.farClipPlane = 500f;
        }
    }

    void Update()
    {
        HandleInput();
        UpdateFPS();

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        if (!_initialized)
        {
            CreateBattlefield(world);
            _initialized = true;
        }

        SyncGunState(world);
        CountProjectiles(world);
    }

    void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame)
            spawning = !spawning;
        if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed)
            fireRateMultiplier = math.min(fireRateMultiplier * 1.02f, 20f);
        if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed)
            fireRateMultiplier = math.max(fireRateMultiplier * 0.98f, 0.01f);
        if (kb.rKey.wasPressedThisFrame)
            ReassignTargets();
    }

    void UpdateFPS()
    {
        _fpsTimer += Time.unscaledDeltaTime;
        _fpsCount++;
        if (_fpsTimer >= 0.5f)
        {
            _displayFps = _fpsCount / _fpsTimer;
            _fpsTimer = 0f;
            _fpsCount = 0;
        }
    }

    void CreateBattlefield(World world)
    {
        var em = world.EntityManager;
        _teamAGuns = new NativeArray<Entity>(gunsPerTeam, Allocator.Persistent);
        _teamBGuns = new NativeArray<Entity>(gunsPerTeam, Allocator.Persistent);

        float halfZ = (gunsPerTeam - 1) * gunSpacing * 0.5f;
        float halfX = teamSpacing * 0.5f;
        var rng = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));

        int missileGunCount = Mathf.RoundToInt(gunsPerTeam * missileGunFraction);

        for (int i = 0; i < gunsPerTeam; i++)
        {
            float z = -halfZ + i * gunSpacing;
            bool isMissileGun = i < missileGunCount;

            var gunA = em.CreateEntity(typeof(GunEmplacement));
            em.SetComponentData(gunA, new GunEmplacement
            {
                Position = new float3(-halfX, 0f, z),
                AimDirection = new float3(1f, 0f, 0f),
                FireRate = isMissileGun ? missileFireRate : bulletFireRate,
                ProjectileType = isMissileGun ? 1 : 0,
                TeamId = 0,
                MinSpeed = isMissileGun ? missileSpeed * 0.8f : bulletSpeed * 0.9f,
                MaxSpeed = isMissileGun ? missileSpeed * 1.2f : bulletSpeed * 1.1f,
                SpreadAngle = spreadAngle,
                ProjectileLifetime = projectileLifetime,
                RandomSeed = rng.NextUInt(1, uint.MaxValue),
                Enabled = spawning
            });
            _teamAGuns[i] = gunA;

            var gunB = em.CreateEntity(typeof(GunEmplacement));
            em.SetComponentData(gunB, new GunEmplacement
            {
                Position = new float3(halfX, 0f, z),
                AimDirection = new float3(-1f, 0f, 0f),
                FireRate = isMissileGun ? missileFireRate : bulletFireRate,
                ProjectileType = isMissileGun ? 1 : 0,
                TeamId = 1,
                MinSpeed = isMissileGun ? missileSpeed * 0.8f : bulletSpeed * 0.9f,
                MaxSpeed = isMissileGun ? missileSpeed * 1.2f : bulletSpeed * 1.1f,
                SpreadAngle = spreadAngle,
                ProjectileLifetime = projectileLifetime,
                RandomSeed = rng.NextUInt(1, uint.MaxValue),
                Enabled = spawning
            });
            _teamBGuns[i] = gunB;
        }

        AssignTargets(em);
    }

    void AssignTargets(EntityManager em)
    {
        var rng = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));

        for (int i = 0; i < gunsPerTeam; i++)
        {
            int targetIdx = rng.NextInt(0, gunsPerTeam);
            var gunA = em.GetComponentData<GunEmplacement>(_teamAGuns[i]);
            gunA.TargetGun = _teamBGuns[targetIdx];
            var targetPosB = em.GetComponentData<GunEmplacement>(_teamBGuns[targetIdx]).Position;
            gunA.AimDirection = math.normalize(targetPosB - gunA.Position);
            em.SetComponentData(_teamAGuns[i], gunA);

            targetIdx = rng.NextInt(0, gunsPerTeam);
            var gunB = em.GetComponentData<GunEmplacement>(_teamBGuns[i]);
            gunB.TargetGun = _teamAGuns[targetIdx];
            var targetPosA = em.GetComponentData<GunEmplacement>(_teamAGuns[targetIdx]).Position;
            gunB.AimDirection = math.normalize(targetPosA - gunB.Position);
            em.SetComponentData(_teamBGuns[i], gunB);
        }
    }

    void ReassignTargets()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated || !_initialized) return;
        AssignTargets(world.EntityManager);
    }

    void SyncGunState(World world)
    {
        var em = world.EntityManager;
        if (!_teamAGuns.IsCreated) return;

        for (int i = 0; i < gunsPerTeam; i++)
        {
            if (em.Exists(_teamAGuns[i]))
            {
                var gun = em.GetComponentData<GunEmplacement>(_teamAGuns[i]);
                gun.Enabled = spawning;
                bool isMissile = gun.ProjectileType == 1;
                gun.FireRate = (isMissile ? missileFireRate : bulletFireRate) * fireRateMultiplier;
                em.SetComponentData(_teamAGuns[i], gun);
            }

            if (em.Exists(_teamBGuns[i]))
            {
                var gun = em.GetComponentData<GunEmplacement>(_teamBGuns[i]);
                gun.Enabled = spawning;
                bool isMissile = gun.ProjectileType == 1;
                gun.FireRate = (isMissile ? missileFireRate : bulletFireRate) * fireRateMultiplier;
                em.SetComponentData(_teamBGuns[i], gun);
            }
        }
    }

    void CountProjectiles(World world)
    {
        var em = world.EntityManager;
        using var query = em.CreateEntityQuery(typeof(Projectile));
        var projectiles = query.ToComponentDataArray<Projectile>(Allocator.Temp);

        int bullets = 0, missiles = 0;
        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i].Type == 0) bullets++;
            else missiles++;
        }

        _lastBulletCount = bullets;
        _lastMissileCount = missiles;
        _lastTotalCount = projectiles.Length;
        projectiles.Dispose();
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        var bgStyle = new GUIStyle(GUI.skin.box);

        GUILayout.BeginArea(new Rect(10, 10, 400, 240), bgStyle);
        GUILayout.Label("=== Battlefield Ballistics Test ===", style);
        GUILayout.Label($"Guns: {gunsPerTeam * 2} ({gunsPerTeam} per team)", style);
        GUILayout.Label($"Bullets: {_lastBulletCount:N0}  |  Missiles: {_lastMissileCount:N0}", style);
        GUILayout.Label($"Total Projectiles: {_lastTotalCount:N0}", style);
        GUILayout.Label($"Fire Rate Mult: {fireRateMultiplier:F2}x", style);
        GUILayout.Label($"Spawning: {(spawning ? "ON" : "OFF")}", style);
        GUILayout.Label($"FPS: {_displayFps:F1}", style);
        GUILayout.Label("[Space] Toggle | [+/-] Rate | [R] Retarget", style);
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            if (_teamAGuns.IsCreated)
            {
                for (int i = 0; i < _teamAGuns.Length; i++)
                    if (em.Exists(_teamAGuns[i])) em.DestroyEntity(_teamAGuns[i]);
                _teamAGuns.Dispose();
            }
            if (_teamBGuns.IsCreated)
            {
                for (int i = 0; i < _teamBGuns.Length; i++)
                    if (em.Exists(_teamBGuns[i])) em.DestroyEntity(_teamBGuns[i]);
                _teamBGuns.Dispose();
            }
        }
        else
        {
            if (_teamAGuns.IsCreated) _teamAGuns.Dispose();
            if (_teamBGuns.IsCreated) _teamBGuns.Dispose();
        }
    }
}
