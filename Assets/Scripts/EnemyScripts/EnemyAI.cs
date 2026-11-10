using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Hivatkozások")]
    public Transform player;
    public LayerMask obstacleMask;      // Falak, akadályok rétege (a játékos NE legyen benne)

    [Header("Életerő")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Látás")]
    public float viewDistance = 15f;
    [Range(1f, 360f)] public float viewAngle = 110f;
    public float eyeHeight = 1.6f;      // Honnan "néz" az ellenség
    public float playerTargetHeight = 1f; // A játékos mely pontját célozza a Raycast

    [Header("Mozgás")]
    public float chaseSpeed = 5f;
    public float returnSpeed = 2.5f;

    [Header("Memória")]
    public float memoryDuration = 60f;  // Ennyi ideig emlékszik (másodperc)

    [Header("Keresés")]
    public float searchRotateSpeed = 120f; // Az első teljes körbefordulás sebessége (fok/mp)
    public float lookTurnSpeed = 90f;      // Random nézelődésnél a fordulás sebessége (fok/mp)
    public float lookPauseMin = 0.5f;      // Min. ennyit néz egy irányba (mp)
    public float lookPauseMax = 2.5f;      // Max. ennyit néz egy irányba (mp)

    private NavMeshAgent agent;
    private Vector3 startPosition;
    private Vector3 lastKnownPosition;
    private float timeSinceLastSeen = Mathf.Infinity;
    private bool hasMemory = false;
    private bool isDead = false;
    private float rotatedAmount = 0f;
    private bool fullRotationDone = false;
    private Quaternion lookTargetRotation;
    private float lookTimer = 0f;

    private enum State { Idle, Chase, GoToLastKnown, Searching, Return }
    private State state = State.Idle;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        currentHealth = maxHealth;
        SetPlayer();
    }

    public void SetPlayer(){
        if (player == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null) player = playerMovement.transform;
            else Debug.LogWarning("EnemyAI: no PlayerMovement found in the scene.", this);
        }
    }

    // Ezt hívd meg a sebzést okozó scriptből (lövedék, kard, stb.)
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        // Ha megsebzik, azonnal tudja, hol van a játékos
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
        agent.enabled = false;
        Destroy(gameObject); // Eltűnik a pályáról
    }

    void Update()
    {
        if (isDead) return;

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

            // Lejárt az 1 perc -> elfelejti a játékost
            if (timeSinceLastSeen >= memoryDuration)
            {
                hasMemory = false;
                state = State.Return;
            }
            else if (state == State.Chase)
            {
                state = State.GoToLastKnown;
            }
        }

        switch (state)
        {
            case State.Idle:          break;
            case State.Chase:         DoChase();         break;
            case State.GoToLastKnown: DoGoToLastKnown(); break;
            case State.Searching:     DoSearching();     break;
            case State.Return:        DoReturn();        break;
        }
    }

    void DoChase()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
    }

    void DoGoToLastKnown()
    {
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
        // Megáll az utolsó látott helyen és "körülnéz"
        agent.ResetPath();

        // 1) Egy teljes körbefordulás
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

        // 2) Random irányokba nézelődik (a memória lejártáig, ami után hazamegy)
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
        agent.speed = returnSpeed;
        agent.SetDestination(startPosition);

        if (ReachedDestination())
            state = State.Idle;
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

        // Látószög ellenőrzés
        float angle = Vector3.Angle(transform.forward, new Vector3(dir.x, 0f, dir.z));
        if (angle > viewAngle * 0.5f) return false;

        // Takarásban van-e (fal, akadály)
        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, obstacleMask))
        {
            // Ha a találat maga a játékos (vagy az ellenség), az nem akadály
            bool hitPlayer = hit.transform == player || hit.transform.IsChildOf(player);
            bool hitSelf = hit.transform.IsChildOf(transform);
            if (!hitPlayer && !hitSelf)
                return false;
        }

        return true;
    }

    // Szerkesztőben látható segédvonalak
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);

        if (Application.isPlaying && hasMemory)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.5f);
        }
    }
}