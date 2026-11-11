using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Keeps a set number of enemies alive inside a sphere. When one dies, a new one spawns at a random
// spot inside the sphere: on the ground (NavMesh), never under the map, under objects or inside walls.
// If the object has a SphereCollider, its size is the spawn area (it is made a trigger, so nothing collides with it).
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemies")]
    [SerializeField] EnemyAI enemyPrefab;
    [SerializeField] int maxAlive = 5;
    [Tooltip("Enemies already placed in the scene inside the sphere are counted too.")]
    [SerializeField] bool countExistingEnemies = true;
    [SerializeField] bool fillOnStart = true;

    [Header("Timing")]
    [Tooltip("Seconds after an enemy dies before a new one spawns.")]
    [SerializeField] float respawnDelay = 3f;
    [SerializeField] float checkInterval = 0.5f;

    [Header("Spawn area")]
    [Tooltip("Used when there is no SphereCollider on this object.")]
    [SerializeField] float radius = 20f;
    [Tooltip("What counts as ground for the spawn raycast.")]
    [SerializeField] LayerMask groundMask = ~0;
    [Tooltip("Spawned enemies roam inside the sphere.")]
    [SerializeField] bool roamInsideArea = true;

    [Header("Safe spawn")]
    [SerializeField] float minDistanceFromPlayer = 8f;
    [Tooltip("Try not to spawn right in front of the player's camera.")]
    [SerializeField] bool avoidPlayerView = true;
    [SerializeField] float enemyRadius = 0.5f;
    [SerializeField] float enemyHeight = 2f;
    [Tooltip("Things the enemy must not spawn inside (walls, other enemies...).")]
    [SerializeField] LayerMask blockingMask = ~0;
    [SerializeField] int attemptsPerSpawn = 30;

    readonly List<EnemyAI> alive = new List<EnemyAI>();
    readonly Collider[] overlap = new Collider[8];
    SphereCollider area;
    Transform player;
    Camera playerCamera;
    float nextCheck;
    float respawnAt = -1f;

    public int AliveCount
    {
        get { Prune(); return alive.Count; }
    }

    void Awake()
    {
        area = GetComponent<SphereCollider>();
        if (area != null && !area.isTrigger) area.isTrigger = true;
    }

    void Start()
    {
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
        playerCamera = Camera.main;

        if (enemyPrefab == null)
        {
            Debug.LogWarning("EnemySpawner: no Enemy Prefab assigned.", this);
            enabled = false;
            return;
        }

        if (countExistingEnemies) AdoptExistingEnemies();

        if (fillOnStart)
        {
            int guard = maxAlive * 2;
            while (AliveCount < maxAlive && guard-- > 0)
            {
                if (TryFindSpawnPoint(out Vector3 point)) Spawn(point);
            }
        }
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        if (AliveCount >= maxAlive)
        {
            respawnAt = -1f;
            return;
        }

        // One enemy is missing: wait respawnDelay, then spawn one
        if (respawnAt < 0f) respawnAt = Time.time + respawnDelay;
        if (Time.time < respawnAt) return;

        if (TryFindSpawnPoint(out Vector3 point))
        {
            Spawn(point);
            respawnAt = -1f;
        }
    }

    void Prune()
    {
        alive.RemoveAll(e => e == null);   // destroyed enemies become null
    }

    void AdoptExistingEnemies()
    {
        GetArea(out Vector3 center, out float r);
        foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            if ((enemy.transform.position - center).sqrMagnitude > r * r || alive.Contains(enemy)) continue;
            alive.Add(enemy);
            if (roamInsideArea) enemy.SetRoamArea(center, r);
        }
    }

    void Spawn(Vector3 point)
    {
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        EnemyAI enemy = Instantiate(enemyPrefab, point, rotation);

        if (roamInsideArea)
        {
            GetArea(out Vector3 center, out float r);
            enemy.SetRoamArea(center, r);
        }
        alive.Add(enemy);
    }

    void GetArea(out Vector3 center, out float r)
    {
        if (area != null)
        {
            center = transform.TransformPoint(area.center);
            Vector3 s = transform.lossyScale;
            r = area.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        else
        {
            center = transform.position;
            r = radius;
        }
    }

    // First pass avoids the player's view; if nothing is found, a second pass allows it
    bool TryFindSpawnPoint(out Vector3 point)
    {
        if (TryFindSpawnPoint(avoidPlayerView, out point)) return true;
        return avoidPlayerView && TryFindSpawnPoint(false, out point);
    }

    bool TryFindSpawnPoint(bool checkView, out Vector3 point)
    {
        GetArea(out Vector3 center, out float r);

        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            // Random column inside the sphere, cast down from the sphere's surface at that spot
            Vector2 offset = Random.insideUnitCircle * r;
            float halfHeight = Mathf.Sqrt(Mathf.Max(0f, r * r - offset.sqrMagnitude));
            Vector3 top = center + new Vector3(offset.x, halfHeight, offset.y);

            // The first thing below must be the ground: a wall or roof here means "under something" -> skip
            if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, halfHeight * 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (((1 << hit.collider.gameObject.layer) & groundMask) == 0) continue;

            // Snap to the NavMesh, on the same floor
            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit nav, 1.5f, NavMesh.AllAreas)) continue;
            Vector3 p = nav.position;
            if (Mathf.Abs(p.y - hit.point.y) > 1f) continue;

            if (player != null && (p - player.position).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer) continue;
            if (checkView && IsVisibleToPlayer(p)) continue;
            if (IsBlocked(p)) continue;

            point = p;
            return true;
        }

        point = center;
        return false;
    }

    bool IsBlocked(Vector3 p)
    {
        Vector3 bottom = p + Vector3.up * (enemyRadius + 0.05f);
        Vector3 topPoint = p + Vector3.up * Mathf.Max(enemyRadius + 0.05f, enemyHeight - enemyRadius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, topPoint, enemyRadius, overlap, blockingMask, QueryTriggerInteraction.Ignore);
        return count > 0;
    }

    bool IsVisibleToPlayer(Vector3 p)
    {
        if (playerCamera == null) return false;

        Vector3 target = p + Vector3.up * (enemyHeight * 0.5f);
        Vector3 viewport = playerCamera.WorldToViewportPoint(target);
        bool onScreen = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
        if (!onScreen) return false;

        // On screen, but hidden behind something counts as not visible
        Vector3 from = playerCamera.transform.position;
        Vector3 dir = target - from;
        if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            bool hitPlayer = player != null && hit.transform.IsChildOf(player);
            if (!hitPlayer) return false;
        }
        return true;
    }

    void OnDrawGizmos()
    {
        if (area == null) area = GetComponent<SphereCollider>();
        GetArea(out Vector3 center, out float r);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(center, r);
    }
}
