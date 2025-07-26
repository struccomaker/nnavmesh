using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    [Header("AI Behavior")]
    public float detectionRange = 15f;
    public float updateInterval = 0.5f;

    [Header("Tactical Response")]
    public float cautionDistance = 8f;
    public float fleeDistance = 3f;

    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;

    private NavMeshAgent agent;
    private Transform player;
    private PlayerWeaponSystem playerWeapon;
    private TacticalWeightSystem tacticalSystem;

    private float lastUpdateTime;
    private Vector3 lastKnownPlayerPosition;
    private bool playerDetected = false;
    private float playerLostTime = 0f; // When did we lose sight of player
    private float searchDuration = 10f; // How long to search before giving up

    // Current tactical state
    private TacticalBehavior currentBehavior = TacticalBehavior.Patrol;
    private PlayerWeaponSystem.WeaponType lastKnownWeaponType;
    private Vector3 currentCoverPosition;
    private bool isInCover = false;
    private bool isSeekingCover = false; // New flag to track if we're committed to seeking cover
    private float observationTimer = 0f;
    private float observationDuration = 2f; // How long to observe before making decision
    private float behaviorCommitTime = 1f; // Minimum time to stick with a behavior
    private float lastBehaviorChangeTime = 0f;

    public enum TacticalBehavior
    {
        Patrol,           // No player detected - random movement
        Search,           // Lost player, searching around last known position
        Approach,         // Player detected, moving to attack
        SeekCover,        // Player has ranged weapon, seeking cover
        ObserveFromCover, // Hidden behind cover, watching player
        Flee,             // Too close to player with dangerous weapon
        FlankFromCover    // Moving from cover to flank player
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerWeapon = player?.GetComponent<PlayerWeaponSystem>();
        tacticalSystem = FindObjectOfType<TacticalWeightSystem>();

        if (agent != null)
        {
            agent.speed = walkSpeed;
        }

        Debug.Log($"Zombie AI initialized. Detection range: {detectionRange}m");
    }

    void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateTacticalBehavior();
            lastUpdateTime = Time.time;
        }
    }

    void UpdateTacticalBehavior()
    {
        if (player == null || playerWeapon == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer();

        // Update player detection
        if (canSeePlayer && distanceToPlayer <= detectionRange)
        {
            playerDetected = true;
            lastKnownPlayerPosition = player.position;
            lastKnownWeaponType = playerWeapon.currentWeapon;
            playerLostTime = 0f; // Reset lost time since we can see player
        }
        else if (playerDetected && !canSeePlayer)
        {
            // We had the player but lost sight - start searching
            if (playerLostTime == 0f)
            {
                playerLostTime = Time.time; // Mark when we lost the player
                Debug.Log($"Zombie {name} lost sight of player, starting search");
            }

            // Check if we've been searching too long
            if (Time.time - playerLostTime > searchDuration)
            {
                playerDetected = false;
                playerLostTime = 0f;
                Debug.Log($"Zombie {name} gave up searching, returning to patrol");
            }
        }

        // Determine behavior based on player detection state
        if (!playerDetected)
        {
            SetBehavior(TacticalBehavior.Patrol);
            return;
        }
        else if (playerDetected && !canSeePlayer && playerLostTime > 0f)
        {
            // We know player exists but can't see them - search mode
            SetBehavior(TacticalBehavior.Search);
            ExecuteBehavior();
            return;
        }

        // DEBUG: Print current state every few updates
        if (Time.time % 2f < updateInterval) // Print every 2 seconds
        {
            Debug.Log($"Zombie {name} UPDATE: Distance={distanceToPlayer:F1}m, CanSee={canSeePlayer}, Weapon={lastKnownWeaponType}, ThreatRadius={playerWeapon.CurrentThreatRadius:F1}m, Behavior={currentBehavior}");
        }

        // Tactical decision based on weapon type and distance
        TacticalBehavior newBehavior = DetermineTacticalBehavior(distanceToPlayer, lastKnownWeaponType);
        SetBehavior(newBehavior);
        ExecuteBehavior();
    }

    TacticalBehavior DetermineTacticalBehavior(float distance, PlayerWeaponSystem.WeaponType weaponType)
    {
        switch (weaponType)
        {
            case PlayerWeaponSystem.WeaponType.Melee:
                // For melee: aggressive chase until very close
                if (distance < 0.8f) // Only flee when touching
                    return TacticalBehavior.Flee;
                else if (isInCover)
                    return TacticalBehavior.FlankFromCover; // Leave cover to chase
                else
                    return TacticalBehavior.Approach; // CHASE AGGRESSIVELY

            case PlayerWeaponSystem.WeaponType.Ranged:
                // For ranged: avoid line of sight completely
                if (distance < 0.8f) // Emergency flee only
                    return TacticalBehavior.Flee;

                // Check if within threat range
                float threatRadius = playerWeapon != null ? playerWeapon.CurrentThreatRadius : 10f;

                if (distance <= threatRadius)
                {
                    // Within threat range - check if we're already safe
                    if (isInCover && !CanSeePlayer() && !HasLineOfSightToPlayer(transform.position))
                        return TacticalBehavior.ObserveFromCover; // Safe and hidden
                    else if (isSeekingCover) // COMMITTED TO SEEKING COVER
                        return TacticalBehavior.SeekCover; // Continue to cover destination
                    else
                        return TacticalBehavior.SeekCover; // Find cover immediately
                }
                else
                {
                    // Outside threat range - can approach normally
                    return TacticalBehavior.Approach;
                }

            default:
                return TacticalBehavior.Approach;
        }
    }

    void SetBehavior(TacticalBehavior newBehavior)
    {
        if (currentBehavior != newBehavior)
        {
            // Prevent rapid behavior switching (except for immediate threats)
            float timeSinceLastChange = Time.time - lastBehaviorChangeTime;
            bool isImmediateThreat = newBehavior == TacticalBehavior.Flee;
            bool canChangeBehavior = timeSinceLastChange >= behaviorCommitTime || isImmediateThreat;

            // SPECIAL CASE: Don't interrupt seeking cover unless it's an emergency OR weapon changed to melee
            if (currentBehavior == TacticalBehavior.SeekCover && isSeekingCover && !isImmediateThreat)
            {
                // Allow behavior change if weapon changed to melee (should flank/approach)
                bool weaponChangedToMelee = (newBehavior == TacticalBehavior.Approach || newBehavior == TacticalBehavior.FlankFromCover)
                                          && lastKnownWeaponType == PlayerWeaponSystem.WeaponType.Melee;

                if (!weaponChangedToMelee)
                {
                    Debug.Log($"Zombie {name} COMMITTED to seeking cover - ignoring behavior change to {newBehavior}");
                    return;
                }
                else
                {
                    Debug.Log($"Zombie {name} WEAPON CHANGED TO MELEE - abandoning cover search to attack!");
                    isSeekingCover = false; // Release cover commitment for weapon change
                }
            }

            if (!canChangeBehavior)
            {
                Debug.Log($"Zombie {name} ignoring behavior change (too soon): {currentBehavior} ? {newBehavior}");
                return;
            }

            Debug.Log($"Zombie {name} behavior: {currentBehavior} ? {newBehavior} | Player weapon: {lastKnownWeaponType} | Distance: {Vector3.Distance(transform.position, lastKnownPlayerPosition):F1}m | InCover: {isInCover}");

            // Reset states when changing behavior
            if (newBehavior == TacticalBehavior.SeekCover && currentBehavior != TacticalBehavior.SeekCover)
            {
                // Starting fresh cover search
                currentCoverPosition = Vector3.zero;
                isInCover = false;
                isSeekingCover = true; // COMMIT to seeking cover
            }
            else if (newBehavior != TacticalBehavior.SeekCover)
            {
                // Reset seeking cover flag when changing to other behaviors
                isSeekingCover = false;
                if (newBehavior == TacticalBehavior.Approach || newBehavior == TacticalBehavior.FlankFromCover)
                {
                    isInCover = false; // Leave cover when attacking
                }
            }

            currentBehavior = newBehavior;
            lastBehaviorChangeTime = Time.time;
        }
    }

    void ExecuteBehavior()
    {
        switch (currentBehavior)
        {
            case TacticalBehavior.Patrol:
                Patrol();
                break;

            case TacticalBehavior.Search:
                SearchForPlayer();
                break;

            case TacticalBehavior.Approach:
                Approach();
                break;

            case TacticalBehavior.SeekCover:
                SeekCover();
                break;

            case TacticalBehavior.Flee:
                FleeFromPlayer();
                break;

            case TacticalBehavior.ObserveFromCover:
                ObserveFromCover();
                break;

            case TacticalBehavior.FlankFromCover:
                FlankFromCover();
                break;
        }
    }

    void Patrol()
    {
        agent.speed = walkSpeed;

        // Simple patrol - move to random nearby point
        if ((!agent.hasPath || agent.remainingDistance < 1f) && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            Vector3 randomPoint = GetRandomPatrolPoint();
            if (randomPoint != Vector3.zero)
            {
                agent.SetDestination(randomPoint);
                Debug.Log($"Zombie {name} patrolling to {randomPoint}");
            }
        }
    }

    void SearchForPlayer()
    {
        agent.speed = walkSpeed * 1.1f; // Slightly faster when searching

        Debug.Log($"Zombie {name} searching for player around last known position");

        // Move around the last known player position in a search pattern
        if ((!agent.hasPath || agent.remainingDistance < 2f) && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            Vector3 searchPoint = GetSearchPoint();
            if (searchPoint != Vector3.zero)
            {
                agent.SetDestination(searchPoint);
                Debug.Log($"Zombie {name} searching at {searchPoint}");
            }
        }
    }

    void Approach()
    {
        agent.speed = runSpeed;

        // For melee weapons: AGGRESSIVE DIRECT CHASE
        if (lastKnownWeaponType == PlayerWeaponSystem.WeaponType.Melee)
        {
            Debug.Log($"Zombie {name} AGGRESSIVELY CHASING player with melee weapon!");

            // Direct approach - get as close as possible
            if (agent.isOnNavMesh && agent.isActiveAndEnabled)
            {
                agent.SetDestination(lastKnownPlayerPosition);
            }
        }
        else
        {
            // For ranged weapons outside threat range: normal approach
            Vector3 flankPosition = FindFlankingPosition();
            if (flankPosition != Vector3.zero && agent.isOnNavMesh && agent.isActiveAndEnabled)
            {
                agent.SetDestination(flankPosition);
            }
            else
            {
                agent.SetDestination(lastKnownPlayerPosition);
            }
        }
    }

    void SeekCover()
    {
        agent.speed = runSpeed;

        // Find a covered position using tactical weights
        Vector3 coverPosition = FindCoverPosition();
        if (coverPosition != Vector3.zero && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.SetDestination(coverPosition);
        }
        else
        {
            // No cover found or NavMesh issue, flee instead
            FleeFromPlayer();
        }
    }

    void FleeFromPlayer()
    {
        agent.speed = runSpeed;
        isInCover = false; // Not in cover when fleeing
        isSeekingCover = false; // Reset seeking cover commitment

        // Check if we're already in a safe position (player can't see us)
        if (!CanSeePlayer() && !HasLineOfSightToPlayer(transform.position))
        {
            // We're safe from player's line of sight - STOP FLEEING
            agent.speed = 0f;
            agent.ResetPath(); // Stop moving
            isInCover = true; // Consider this a cover position
            currentCoverPosition = transform.position; // Mark current position as cover

            Debug.Log($"Zombie {name} found safe position while fleeing - stopping to observe");
            SetBehavior(TacticalBehavior.ObserveFromCover);
            return;
        }

        // Still need to flee - find a safe position
        Vector3 safePosition = FindSafeFleePosition();
        if (safePosition != Vector3.zero)
        {
            agent.SetDestination(safePosition);
            Debug.Log($"Zombie {name} fleeing to safe position at {safePosition}");
        }
        else
        {
            // Fallback: just move away from player
            Vector3 fleeDirection = (transform.position - lastKnownPlayerPosition).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * 10f;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(fleeTarget, out hit, 10f, NavMesh.AllAreas))
            {
                if (agent.isOnNavMesh && agent.isActiveAndEnabled)
                {
                    agent.SetDestination(hit.position);
                }
            }
        }
    }

    Vector3 FindSafeFleePosition()
    {
        // Find a position that's both away from player AND blocks line of sight
        Vector3 awayFromPlayer = (transform.position - lastKnownPlayerPosition).normalized;

        // Try multiple distances and angles to find safe spot
        for (int distance = 8; distance <= 15; distance += 2)
        {
            for (int angle = -60; angle <= 60; angle += 20)
            {
                Vector3 direction = Quaternion.Euler(0, angle, 0) * awayFromPlayer;
                Vector3 testPos = transform.position + direction * distance;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(testPos, out hit, 3f, NavMesh.AllAreas))
                {
                    // Check if this position is safe from player's line of sight
                    if (!HasLineOfSightToPlayer(hit.position))
                    {
                        Debug.Log($"Zombie {name} found safe flee position that blocks player line of sight");
                        return hit.position;
                    }
                }
            }
        }

        return Vector3.zero; // No safe position found
    }

    void FlankPlayer()
    {
        agent.speed = walkSpeed;

        // Try to approach player using covered routes
        Vector3 flankPosition = FindFlankingPosition();
        if (flankPosition != Vector3.zero && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.SetDestination(flankPosition);
        }
        else
        {
            // No flanking route found, seek cover instead
            SeekCover();
        }
    }

    void ObserveFromCover()
    {
        agent.speed = 0f; // STAY PUT - don't move from cover

        // Look towards last known player position
        if (player != null)
        {
            Vector3 lookDirection = (lastKnownPlayerPosition - transform.position).normalized;
            lookDirection.y = 0f;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        // Check if player is still visible
        bool canSeePlayerNow = CanSeePlayer();

        if (canSeePlayerNow)
        {
            // Player spotted again - check weapon type
            lastKnownPlayerPosition = player.position; // Update position
            playerLostTime = 0f; // Reset lost time since we can see player again

            if (lastKnownWeaponType == PlayerWeaponSystem.WeaponType.Melee)
            {
                // Player has melee - GIVE CHASE!
                Debug.Log($"Zombie {name} sees player with melee - giving chase!");
                isInCover = false;
                SetBehavior(TacticalBehavior.FlankFromCover);
            }
            else
            {
                // Player still has ranged - RELOCATE!
                Debug.Log($"Zombie {name} sees player with ranged - relocating!");
                isInCover = false;
                SetBehavior(TacticalBehavior.SeekCover);
            }
        }
        else
        {
            // Can't see player from cover - start searching if we haven't already
            if (playerLostTime == 0f)
            {
                playerLostTime = Time.time;
                Debug.Log($"Zombie {name} lost sight of player from cover, will start searching soon");
            }
        }

        // Continue observing if player not visible
        Debug.Log($"Zombie {name} observing from cover... Can see player: {canSeePlayerNow}");
    }

    void FlankFromCover()
    {
        agent.speed = runSpeed * 1.2f; // Even faster when flanking
        isSeekingCover = false; // No longer seeking cover when flanking

        Debug.Log($"Zombie {name} AGGRESSIVELY flanking from cover towards player with melee weapon");

        // Move very aggressively towards player - direct path
        if (agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.SetDestination(lastKnownPlayerPosition);
        }

        // Once we start flanking, we're no longer in cover
        if (Vector3.Distance(transform.position, currentCoverPosition) > 2f)
        {
            isInCover = false;
            currentCoverPosition = Vector3.zero;
        }
    }

    // Method to fix NavMesh positioning
    void FixNavMeshPosition()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Debug.Log($"Fixed {name} position to {hit.position}");
        }
        else
        {
            Debug.LogError($"Could not find NavMesh near {name}! Manual positioning required.");
        }
    }

    Vector3 FindCoverPosition()
    {
        if (tacticalSystem == null)
        {
            // Fallback: Find position behind walls, away from player line of sight
            return FindCoverAwayFromPlayer();
        }

        // Find the safest nearby position (lowest tactical weight)
        Vector3 bestPosition = Vector3.zero;
        float bestWeight = float.MaxValue;

        // Sample positions in a radius around the zombie
        for (int i = 0; i < 16; i++) // More samples for better cover
        {
            float angle = i * 22.5f * Mathf.Deg2Rad; // 360/16 = 22.5 degrees
            Vector3 testPos = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 8f; // Larger radius

            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPos, out hit, 3f, NavMesh.AllAreas))
            {
                // Check if this position is away from player line of sight
                if (!HasLineOfSightToPlayer(hit.position))
                {
                    float weight = tacticalSystem.GetTacticalWeight(hit.position);
                    if (weight < bestWeight)
                    {
                        bestWeight = weight;
                        bestPosition = hit.position;
                    }
                }
            }
        }

        // If no tactical position found, use fallback
        if (bestPosition == Vector3.zero)
        {
            bestPosition = FindCoverAwayFromPlayer();
        }

        return bestPosition;
    }

    Vector3 FindCoverAwayFromPlayer()
    {
        // Find a position behind walls, away from player
        Vector3 awayFromPlayer = (transform.position - lastKnownPlayerPosition).normalized;

        // Try multiple distances and angles
        for (int distance = 5; distance <= 12; distance += 2)
        {
            for (int angle = -45; angle <= 45; angle += 15)
            {
                Vector3 direction = Quaternion.Euler(0, angle, 0) * awayFromPlayer;
                Vector3 testPos = transform.position + direction * distance;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(testPos, out hit, 3f, NavMesh.AllAreas))
                {
                    // Check if this position blocks line of sight to player
                    if (!HasLineOfSightToPlayer(hit.position))
                    {
                        Debug.Log($"Zombie {name} found cover position that blocks line of sight");
                        return hit.position;
                    }
                }
            }
        }

        Debug.LogWarning($"Zombie {name} couldn't find good cover position!");
        return Vector3.zero;
    }

    bool HasLineOfSightToPlayer(Vector3 fromPosition)
    {
        if (player == null) return false;

        Vector3 directionToPlayer = (player.position - fromPosition).normalized;
        float distanceToPlayer = Vector3.Distance(fromPosition, player.position);

        // Raycast to check line of sight - if it hits something, there's no line of sight
        RaycastHit hit;
        if (Physics.Raycast(fromPosition + Vector3.up * 0.5f, directionToPlayer, out hit, distanceToPlayer))
        {
            // If we hit the player directly, there IS line of sight
            if (hit.collider.CompareTag("Player"))
                return true;
            else
                return false; // Hit an obstacle, no line of sight
        }

        return true; // No obstacles, clear line of sight
    }

    Vector3 FindFlankingPosition()
    {
        // Find a position that provides cover but still allows approach to player
        // This is a simplified version - could be enhanced with more sophisticated pathfinding
        Vector3 toPlayer = (lastKnownPlayerPosition - transform.position).normalized;
        Vector3 rightFlank = Vector3.Cross(toPlayer, Vector3.up);

        // Try flanking from the right
        Vector3 flankPos = transform.position + rightFlank * 5f + toPlayer * 2f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(flankPos, out hit, 3f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // Try flanking from the left
        flankPos = transform.position - rightFlank * 5f + toPlayer * 2f;
        if (NavMesh.SamplePosition(flankPos, out hit, 3f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return Vector3.zero;
    }

    Vector3 GetRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 15f; // Larger patrol area
        randomDirection += transform.position;
        randomDirection.y = transform.position.y;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 15f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;
    }

    Vector3 GetSearchPoint()
    {
        // Search around the last known player position
        Vector3 searchCenter = lastKnownPlayerPosition;

        // Create a search pattern around the last known position
        Vector3 randomOffset = Random.insideUnitSphere * 10f; // Search within 10 units
        randomOffset.y = 0f;
        Vector3 searchTarget = searchCenter + randomOffset;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(searchTarget, out hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // Fallback: search around current position
        Vector3 fallbackDirection = Random.insideUnitSphere * 8f;
        fallbackDirection += transform.position;
        fallbackDirection.y = transform.position.y;

        if (NavMesh.SamplePosition(fallbackDirection, out hit, 8f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Raycast to check line of sight
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, directionToPlayer, out hit, distanceToPlayer))
        {
            return hit.collider.CompareTag("Player");
        }

        return false;
    }

    // Called by PlayerWeaponSystem when weapon changes
    public void OnPlayerWeaponChanged(PlayerWeaponSystem.WeaponType newWeapon, Vector3 playerPosition, float threatRadius)
    {
        if (playerDetected)
        {
            lastKnownWeaponType = newWeapon;
            lastKnownPlayerPosition = playerPosition;

            // Reset observation timer on weapon change
            observationTimer = 0f;

            // IMPORTANT: Release cover commitment on weapon change
            if (newWeapon == PlayerWeaponSystem.WeaponType.Melee && isSeekingCover)
            {
                Debug.Log($"Zombie {name} WEAPON CHANGED TO MELEE - releasing cover commitment!");
                isSeekingCover = false;
                currentCoverPosition = Vector3.zero;
            }

            // Force immediate tactical reassessment by resetting behavior commit time
            lastBehaviorChangeTime = 0f;

            float distance = Vector3.Distance(transform.position, playerPosition);
            TacticalBehavior newBehavior = DetermineTacticalBehavior(distance, newWeapon);
            SetBehavior(newBehavior);

            Debug.Log($"Zombie {name} reacting to weapon change: {newWeapon} - New behavior: {newBehavior}");
        }
    }

    // Visualization
    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Behavior-specific visualization
        switch (currentBehavior)
        {
            case TacticalBehavior.Approach:
                Gizmos.color = Color.red;
                break;
            case TacticalBehavior.Search:
                Gizmos.color = Color.orange;
                break;
            case TacticalBehavior.SeekCover:
                Gizmos.color = Color.blue;
                break;
            case TacticalBehavior.ObserveFromCover:
                Gizmos.color = Color.cyan;
                break;
            case TacticalBehavior.FlankFromCover:
                Gizmos.color = Color.green;
                break;
            case TacticalBehavior.Flee:
                Gizmos.color = Color.magenta;
                break;
            default:
                Gizmos.color = Color.white;
                break;
        }

        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);

        // Show current cover position
        if (isInCover && currentCoverPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentCoverPosition, 1f);
            Gizmos.DrawLine(transform.position, currentCoverPosition);
        }

        // Line of sight to player
        if (playerDetected && player != null)
        {
            Gizmos.color = CanSeePlayer() ? Color.red : Color.gray;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, lastKnownPlayerPosition + Vector3.up * 0.5f);
        }
    }
}