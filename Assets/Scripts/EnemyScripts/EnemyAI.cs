using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyType { Melee, Ranged, Creeper }

    [Header("Ellenség Típusa")]
    public EnemyType enemyType = EnemyType.Melee;

    [Header("Hivatkozások")]
    public Transform player;
    public LayerMask obstacleMask;      // Falak, akadályok rétege (a játékos NE legyen benne)

    [Header("Életerő")]
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("Writes every hit (damage and remaining health) to the Console.")]
    public bool logDamage = true;

    [Header("Támadás - Általános")]
    public float attackDamage = 15f;
    public float attackCooldown = 1.5f;
    public float turnToPlayerSpeed = 8f;

    [Header("1. Kardos (Melee) beállítások")]
    public float meleeRange = 2.2f;

    [Header("2. Íjász (Ranged) beállítások")]
    public GameObject projectilePrefab;   // A kilőtt nyílvessző prefabja
    public Transform firePoint;           // Honnan induljon a lövedék (pl. íj/kéz pozíciója)
    public float preferredRange = 12f;    // Ennél közelebb nem jön, ha rálát a játékosra
    public float retreatRange = 5f;       // Ha ennél közelebb jössz hozzá, hátrál
    public float projectileSpeed = 25f;

    [Header("3. Creeper (Robbanó) beállítások")]
    public float fuseStartDistance = 3f;  // Milyen közel kell érnie, hogy elkezdjen sziszegni
    public float fuseCancelDistance = 5.5f; // Ha ennyire elfutsz, leáll a visszaszámlálás
    public float fuseDuration = 3.5f;     // Hány mp után robban
    public float explosionRadius = 5f;    // Robbanás hatósugara
    public float explosionDamage = 60f;
    public GameObject explosionEffect;    // Opcionális részecskeeffekt robbanáskor

    [Header("Látás")]
    public float viewDistance = 15f;
    [Range(1f, 360f)] public float viewAngle = 110f;
    public float eyeHeight = 1.6f;
    public float playerTargetHeight = 1f;

    [Header("Mozgás")]
    public float chaseSpeed = 5f;
    public float returnSpeed = 2.5f;

    [Header("Memória")]
    public float memoryDuration = 60f;

    [Header("Keresés")]
    public float searchRotateSpeed = 120f;
    public float lookTurnSpeed = 90f;
    public float lookPauseMin = 0.5f;
    public float lookPauseMax = 2.5f;

    [Header("Roaming")]
    public bool roam = true;
    public float roamRadius = 10f;
    public float roamSpeed = 2f;
    public float roamWaitMin = 1f;
    public float roamWaitMax = 4f;

    private NavMeshAgent agent;
    private CharacterHealth playerHealth;
    private Vector3 startPosition;
    private Vector3 lastKnownPosition;
    private float timeSinceLastSeen = Mathf.Infinity;
    private bool hasMemory = false;
    private bool isDead = false;
    private float rotatedAmount = 0f;
    private bool fullRotationDone = false;
    private Quaternion lookTargetRotation;
    private float lookTimer = 0f;
    private Vector3 roamCenter;
    private bool roamMoving = false;
    private float roamWaitTimer = 0f;

    // Támadás és Creeper változók
    private float attackTimer = 0f;
    private float currentFuseTimer = 0f;
    private bool isFusing = false;

    private enum State { Idle, Roam, Chase, GoToLastKnown, Searching, Return }
    private State state = State.Roam;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        roamCenter = startPosition;
        currentHealth = maxHealth;
        SetPlayer();
    }

    public void SetPlayer()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null) player = playerMovement.transform;
            else Debug.LogWarning("EnemyAI: no PlayerMovement found in the scene.", this);
        }

        if (player != null)
        {
            playerHealth = player.GetComponentInParent<CharacterHealth>();
            if (playerHealth == null) playerHealth = player.GetComponentInChildren<CharacterHealth>();
        }
    }

    public void SetRoamArea(Vector3 center, float radius)
    {
        roamCenter = center;
        roamRadius = radius;
    }

    public static event System.Action<EnemyAI, bool> Damaged;

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Damaged?.Invoke(this, currentHealth <= 0f);
        if (logDamage) Debug.Log(name + " took " + amount.ToString("0.#") + " damage, health: " + Mathf.Max(0f, currentHealth).ToString("0.#") + " / " + maxHealth, this);

        if (player != null)
        {
            lastKnownPosition = player.position;
            timeSinceLastSeen = 0f;
            hasMemory = true;
            state = State.Chase;
        }

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        if (agent != null && agent.isOnNavMesh) agent.enabled = false;
        Destroy(gameObject);
    }

    void Update()
    {
        if (isDead) return;

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        bool canSee = CanSeePlayer();

        if (canSee)
        {
            lastKnownPosition = player.position;
            timeSinceLastSeen = 0f;
            hasMemory = true;
            state = State.Chase;
        }
        else if (hasMemory)
        {
            timeSinceLastSeen += Time.deltaTime;

            if (timeSinceLastSeen >= memoryDuration)
            {
                hasMemory = false;
                ResetCreeperFuse();
                state = State.Return;
            }
            else if (state == State.Chase)
            {
                ResetCreeperFuse();
                state = State.GoToLastKnown;
            }
        }

        switch (state)
        {
            case State.Idle: break;
            case State.Roam: DoRoam(); break;
            case State.Chase: DoChase(canSee); break;
            case State.GoToLastKnown: DoGoToLastKnown(); break;
            case State.Searching: DoSearching(); break;
            case State.Return: DoReturn(); break;
        }
    }

    void DoChase(bool canSee)
    {
        roamMoving = false;
        if (player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        switch (enemyType)
        {
            case EnemyType.Melee:
                HandleMeleeChase(distToPlayer);
                break;

            case EnemyType.Ranged:
                HandleRangedChase(distToPlayer, canSee);
                break;

            case EnemyType.Creeper:
                HandleCreeperChase(distToPlayer);
                break;
        }
    }

    // --- 1. KARDOS (MELEE) ---
    void HandleMeleeChase(float dist)
    {
        if (dist > meleeRange)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
            FacePlayer();

            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
                // Ide jöhet majd az ütés animáció: animator.SetTrigger("Attack");
            }
        }
    }

    // --- 2. ÍJÁSZ (RANGED) ---
    void HandleRangedChase(float dist, bool canSee)
    {
        // Ha túl messze van, vagy nem lát rá tisztán, közelebb megy
        if (dist > preferredRange || !canSee)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
        }
        // Ha a játékos túl közel jött hozzá, hátrál egy kicsit
        else if (dist < retreatRange)
        {
            Vector3 dirAway = (transform.position - player.position).normalized;
            Vector3 retreatTarget = transform.position + dirAway * 4f;
            if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed * 0.8f;
                agent.SetDestination(hit.position);
            }
            FacePlayer();
        }
        // Optimális lőtávon belül van és rálát: megáll és lő
        else
        {
            agent.isStopped = true;
            FacePlayer();

            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                ShootProjectile();
            }
        }
    }

    void ShootProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("EnemyAI (Ranged): nincs beállítva projectilePrefab!", this);
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * eyeHeight + transform.forward * 0.6f;
        Vector3 targetPos = player.position + Vector3.up * playerTargetHeight;
        Vector3 shootDir = (targetPos - spawnPos).normalized;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shootDir));

        // Ha van rajta Rigidbody, meglökjük
        if (proj.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.linearVelocity = shootDir * projectileSpeed;
        }
        // Ha a csapat lövedék scriptje saját maga mozog előre, a LookRotation már a játékos felé fordította!
    }

    // --- 3. CREEPER (ROBBANÓ) ---
    void HandleCreeperChase(float dist)
    {
        if (!isFusing)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            if (dist <= fuseStartDistance)
            {
                isFusing = true;
                currentFuseTimer = 0f;
                agent.isStopped = true;
            }
        }
        else
        {
            FacePlayer();

            // Ha a játékos elfutott, megszakad a robbanás és újra üldözi
            if (dist > fuseCancelDistance)
            {
                ResetCreeperFuse();
                return;
            }

            currentFuseTimer += Time.deltaTime;
            // Itt lehet pl. villogtatni vagy nagyítani a modellt a visszaszámlálás alatt!

            if (currentFuseTimer >= fuseDuration)
            {
                Explode();
            }
        }
    }

    void ResetCreeperFuse()
    {
        isFusing = false;
        currentFuseTimer = 0f;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
    }

    void Explode()
    {
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in hits)
        {
            CharacterHealth health = col.GetComponentInParent<CharacterHealth>();
            if (health != null && health == playerHealth)
            {
                // Távolságtól függő vagy fix sebzés
                health.TakeDamage(explosionDamage);
                break;
            }
        }

        Die();
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnToPlayerSpeed * Time.deltaTime);
        }
    }

    void DoGoToLastKnown()
    {
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(lastKnownPosition);

        if (ReachedDestination())
        {
            rotatedAmount = 0f;
            fullRotationDone = false;
            state = State.Searching;
        }
    }

    void DoSearching()
    {
        agent.ResetPath();

        if (!fullRotationDone)
        {
            float step = searchRotateSpeed * Time.deltaTime;
            transform.Rotate(0f, step, 0f);
            rotatedAmount += step;

            if (rotatedAmount >= 360f)
            {
                fullRotationDone = true;
                PickRandomLookDirection();
            }
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, lookTargetRotation, lookTurnSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, lookTargetRotation) < 1f)
        {
            lookTimer -= Time.deltaTime;
            if (lookTimer <= 0f)
                PickRandomLookDirection();
        }
    }

    void PickRandomLookDirection()
    {
        float randomYaw = Random.Range(0f, 360f);
        lookTargetRotation = Quaternion.Euler(0f, randomYaw, 0f);
        lookTimer = Random.Range(lookPauseMin, lookPauseMax);
    }

    void DoReturn()
    {
        agent.isStopped = false;
        agent.speed = returnSpeed;
        agent.SetDestination(startPosition);

        if (ReachedDestination())
        {
            roamMoving = false;
            roamWaitTimer = Random.Range(roamWaitMin, roamWaitMax);
            state = roam ? State.Roam : State.Idle;
        }
    }

    void DoRoam()
    {
        if (!roam || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.speed = roamSpeed;

        if (roamMoving)
        {
            if (ReachedDestination())
            {
                roamMoving = false;
                roamWaitTimer = Random.Range(roamWaitMin, roamWaitMax);
            }
            return;
        }

        roamWaitTimer -= Time.deltaTime;
        if (roamWaitTimer > 0f) return;

        if (TryGetRoamPoint(out Vector3 point))
        {
            agent.SetDestination(point);
            roamMoving = true;
        }
        else
        {
            roamWaitTimer = 0.5f;
        }
    }

    bool TryGetRoamPoint(out Vector3 point)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 offset = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = roamCenter + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }
        point = transform.position;
        return false;
    }

    bool ReachedDestination()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = player.position + Vector3.up * playerTargetHeight;
        Vector3 dir = targetPos - eyePos;
        float dist = dir.magnitude;

        if (dist > viewDistance) return false;

        float angle = Vector3.Angle(transform.forward, new Vector3(dir.x, 0f, dir.z));
        if (angle > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, obstacleMask))
        {
            bool hitPlayer = hit.transform == player || hit.transform.IsChildOf(player);
            bool hitSelf = hit.transform.IsChildOf(transform);
            if (!hitPlayer && !hitSelf)
                return false;
        }

        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);

        // Hatótávok vizualizálása típus szerint
        if (enemyType == EnemyType.Melee)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, meleeRange);
        }
        else if (enemyType == EnemyType.Ranged)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, preferredRange);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, retreatRange);
        }
        else if (enemyType == EnemyType.Creeper)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, fuseStartDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

        if (roam)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Application.isPlaying ? roamCenter : transform.position, roamRadius);
        }
    }
}