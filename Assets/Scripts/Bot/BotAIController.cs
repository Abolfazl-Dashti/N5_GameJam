// Attach to: Each Bot GameObject.
// Requires: NavMeshAgent component on the same GameObject.
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class BotAIController : MonoBehaviour, IDiscInteractor, IStaggerable, IResettable
{
    // -------------------------------------------------------------------------
    // FSM STATES
    // -------------------------------------------------------------------------
    public enum BotState
    {
        Idle,           // Doing nothing — match not started or post-goal freeze
        ChaseDisc,      // Moving toward the free disc to pick it up
        HoldAndPass,    // Has disc — deciding whether to pass or shoot
        ShootAtGoal,    // Moving into shoot range and firing at active goal
        Defend,         // Moving to defensive position near own goal
        Intercept       // Dashing toward enemy disc carrier
    }

    // -------------------------------------------------------------------------
    // INSPECTOR REFERENCES
    // -------------------------------------------------------------------------
    [Header("Data")]
    [SerializeField] private BotData botData;

    [Header("Scene References")]
    [SerializeField] private DiscController disc;
    [SerializeField] private PossessionManager possessionManager;

    [Tooltip("This bot's own goal (the one it defends).")]
    [SerializeField] private GoalController ownGoal;

    [Tooltip("The opposing goal (the one it shoots at).")]
    [SerializeField] private GoalController opposingGoal;

    [Tooltip("Teammate bot or player Transform — used for pass targeting.")]
    [SerializeField] private Transform teammate;

    [Header("Events")]
    public UnityEvent onBotCaughtDisc;
    public UnityEvent onBotThrewDisc;
    public UnityEvent onBotStaggered;
    public UnityEvent onBotRecovered;

    // -------------------------------------------------------------------------
    // PRIVATE STATE
    // -------------------------------------------------------------------------
    private BotState _currentState = BotState.Idle;
    private NavMeshAgent _agent;

    // Disc interaction
    private bool _isHoldingDisc;
    private float _holdTimer;

    // Stagger
    private bool _isStaggered;
    private float _staggerTimer;
    private float _staggerDuration = 1.5f;

    // Freeze (for MatchManager center spawn)
    private bool _isFrozen;

    // Dash cooldown
    private bool _dashOnCooldown;
    private float _dashCooldownTimer;

    // FSM evaluation interval
    private float _stateEvalTimer;

    // Cached disc Rigidbody
    private Rigidbody _discRigidbody;

    // -------------------------------------------------------------------------
    // PUBLIC READ-ONLY
    // -------------------------------------------------------------------------
    public BotState CurrentState => _currentState;
    public TeamType Team { get { return botData ? botData.team : TeamType.None; } }

    // -------------------------------------------------------------------------
    // UNITY LIFECYCLE
    // -------------------------------------------------------------------------
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        ValidateReferences();
        ConfigureAgent();

        if (disc)
        {
            _discRigidbody = disc.GetComponent<Rigidbody>();
        }
    }

    private void OnEnable()
    {
        SubscribeToDiscEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromDiscEvents();
    }

    private void Update()
    {
        if (_isFrozen || _isStaggered)
        {
            _agent.ResetPath();
            return;
        }

        TickStagger();
        TickDashCooldown();

        // Re-evaluate FSM state on interval — not every frame (performance)
        _stateEvalTimer += Time.deltaTime;
        if (_stateEvalTimer >= botData.stateEvaluationInterval)
        {
            _stateEvalTimer = 0f;
            EvaluateState();
        }

        ExecuteCurrentState();

        if (_isHoldingDisc)
        {
            _holdTimer += Time.deltaTime;
        }
    }

    // -------------------------------------------------------------------------
    // DISC EVENT SUBSCRIPTIONS
    // -------------------------------------------------------------------------
    private void SubscribeToDiscEvents()
    {
        if (!disc) return;
        disc.onDiscReleased.AddListener(OnDiscReleased);
        disc.onDiscHeld.AddListener(OnDiscHeld);
    }

    private void UnsubscribeFromDiscEvents()
    {
        if (!disc) return;
        disc.onDiscReleased.RemoveListener(OnDiscReleased);
        disc.onDiscHeld.RemoveListener(OnDiscHeld);
    }

    private void OnDiscReleased(Transform lastHolder)
    {
        // If this bot lost the disc externally (stagger), clear holding flag
        if (_isHoldingDisc && (lastHolder == null || lastHolder == transform))
        {
            _isHoldingDisc = false;
            _holdTimer = 0f;
        }
    }

    private void OnDiscHeld(Transform newHolder)
    {
        // If someone else caught the disc, make sure this bot knows it's free
        if (newHolder != transform)
        {
            // Another character has the disc — do nothing, FSM will react
        }
    }

    // -------------------------------------------------------------------------
    // FSM — STATE EVALUATION
    // Re-evaluated every stateEvaluationInterval seconds
    // -------------------------------------------------------------------------
    private void EvaluateState()
    {
        if (_isFrozen)
        {
            TransitionTo(BotState.Idle);
            return;
        }

        // Priority 1: If holding disc — decide to pass or shoot
        if (_isHoldingDisc)
        {
            if (ShouldShoot())
            {
                TransitionTo(BotState.ShootAtGoal);
            }
            else
            {
                TransitionTo(BotState.HoldAndPass);
            }
            return;
        }

        // Priority 2: If enemy has the disc near us — intercept
        if (ShouldIntercept())
        {
            TransitionTo(BotState.Intercept);
            return;
        }

        // Priority 3: If disc is free — chase it
        if (IsDiscFree())
        {
            TransitionTo(BotState.ChaseDisc);
            return;
        }

        // Priority 4: Enemy team has disc — defend
        if (EnemyTeamHasDisc())
        {
            TransitionTo(BotState.Defend);
            return;
        }

        // Priority 5: Teammate has disc — move to a support position
        if (TeammateHasDisc())
        {
            TransitionTo(BotState.HoldAndPass);
            return;
        }

        TransitionTo(BotState.Idle);
    }

    // -------------------------------------------------------------------------
    // FSM — STATE EXECUTION (runs every Update)
    // -------------------------------------------------------------------------
    private void ExecuteCurrentState()
    {
        switch (_currentState)
        {
            case BotState.Idle:
                ExecuteIdle();
                break;

            case BotState.ChaseDisc:
                ExecuteChaseDisc();
                break;

            case BotState.HoldAndPass:
                ExecuteHoldAndPass();
                break;

            case BotState.ShootAtGoal:
                ExecuteShootAtGoal();
                break;

            case BotState.Defend:
                ExecuteDefend();
                break;

            case BotState.Intercept:
                ExecuteIntercept();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // STATE: IDLE
    // -------------------------------------------------------------------------
    private void ExecuteIdle()
    {
        _agent.ResetPath();
    }

    // -------------------------------------------------------------------------
    // STATE: CHASE DISC
    // -------------------------------------------------------------------------
    private void ExecuteChaseDisc()
    {
        if (!disc) return;

        // Move toward the disc
        SetAgentDestination(disc.transform.position);

        // Auto-catch when close enough
        float distToDisc = GetDistanceToDisc();
        if (distToDisc <= botData.catchRadius)
        {
            CatchDisc();
        }
    }

    // -------------------------------------------------------------------------
    // STATE: HOLD AND PASS
    // -------------------------------------------------------------------------
    private void ExecuteHoldAndPass()
    {
        if (!_isHoldingDisc)
        {
            // Support — move to open space near disc
            if (disc)
            {
                Vector3 supportPos = GetSupportPosition();
                SetAgentDestination(supportPos);
            }
            return;
        }

        // Has disc — wait for holdDecisionTime then pass
        if (_holdTimer >= botData.holdDecisionTime)
        {
            AttemptPass();
        }
        else
        {
            // Stop and wait while deciding
            _agent.ResetPath();
        }
    }

    // -------------------------------------------------------------------------
    // STATE: SHOOT AT GOAL
    // -------------------------------------------------------------------------
    private void ExecuteShootAtGoal()
    {
        if (!_isHoldingDisc)
        {
            TransitionTo(BotState.ChaseDisc);
            return;
        }

        if (!opposingGoal) return;

        Vector3 goalPosition = opposingGoal.transform.position;
        float distToGoal = Vector3.Distance(transform.position, goalPosition);

        if (distToGoal <= botData.shootRange)
        {
            // In range — shoot now
            ShootAtGoal();
        }
        else
        {
            // Move closer to the goal
            SetAgentDestination(goalPosition);
        }
    }

    // -------------------------------------------------------------------------
    // STATE: DEFEND
    // -------------------------------------------------------------------------
    private void ExecuteDefend()
    {
        if (!ownGoal) return;

        // Find the enemy carrier position
        Transform enemyCarrier = GetEnemyDiscCarrier();

        Vector3 defendTarget;

        if (enemyCarrier)
        {
            // Position between own goal and enemy carrier
            Vector3 goalPos = ownGoal.transform.position;
            Vector3 enemyPos = enemyCarrier.position;
            defendTarget = Vector3.Lerp(goalPos, enemyPos, 0.4f);
        }
        else
        {
            // No specific threat — stand in front of own goal
            defendTarget = ownGoal.transform.position +
                           ownGoal.transform.forward * botData.defendRadius;
        }

        SetAgentDestination(defendTarget);
    }

    // -------------------------------------------------------------------------
    // STATE: INTERCEPT
    // -------------------------------------------------------------------------
    private void ExecuteIntercept()
    {
        Transform enemyCarrier = GetEnemyDiscCarrier();

        if (!enemyCarrier)
        {
            // Lost target — re-evaluate
            TransitionTo(BotState.Defend);
            return;
        }

        float distToEnemy = Vector3.Distance(transform.position, enemyCarrier.position);

        // Move toward enemy carrier
        SetAgentDestination(enemyCarrier.position);

        // Attempt dash when close enough
        if (distToEnemy <= botData.dashTriggerRange && !_dashOnCooldown)
        {
            AttemptDash(enemyCarrier);
        }
    }

    // -------------------------------------------------------------------------
    // DISC INTERACTION
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bot catches the disc — transitions it to Held state.
    /// </summary>
    private void CatchDisc()
    {
        if (_isHoldingDisc) return;
        if (!disc) return;
        if (disc.currentState == DiscController.DiscState.Held) return;

        _isHoldingDisc = true;
        _holdTimer = 0f;

        disc.SetHeld(transform);
        onBotCaughtDisc.Invoke();

        Debug.Log($"[BotAIController] {gameObject.name} caught the disc.");
    }

    /// <summary>
    /// Bot throws the disc toward a target position.
    /// </summary>
    private void ThrowDiscAt(Vector3 targetPosition, float speedOverride = -1f)
    {
        if (!_isHoldingDisc || !disc) return;

        Vector3 direction = (targetPosition - disc.transform.position).normalized;

        // Add slight inaccuracy based on botData.aimInaccuracy
        direction = AddAimInaccuracy(direction);

        _isHoldingDisc = false;
        _holdTimer = 0f;

        disc.SetPassed(direction, speedOverride);
        onBotThrewDisc.Invoke();

        Debug.Log($"[BotAIController] {gameObject.name} threw the disc toward {targetPosition}.");
    }

    /// <summary>
    /// Bot attempts to pass to its teammate.
    /// Falls back to shooting if no valid teammate found.
    /// </summary>
    private void AttemptPass()
    {
        if (!_isHoldingDisc) return;

        // Find a valid teammate to pass to
        Transform passTarget = GetBestPassTarget();

        if (passTarget)
        {
            ThrowDiscAt(passTarget.position);
            Debug.Log($"[BotAIController] {gameObject.name} passed to {passTarget.name}.");
        }
        else
        {
            // No teammate available — shoot instead
            if (ShouldShoot())
            {
                ShootAtGoal();
            }
        }
    }

    /// <summary>
    /// Bot shoots at the opposing goal with aim inaccuracy applied.
    /// </summary>
    private void ShootAtGoal()
    {
        if (!_isHoldingDisc || !disc) return;
        if (!opposingGoal) return;

        Vector3 goalTarget = opposingGoal.transform.position;
        ThrowDiscAt(goalTarget);

        Debug.Log($"[BotAIController] {gameObject.name} shot at goal!");
    }

    // -------------------------------------------------------------------------
    // DASH / INTERCEPT
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bot dashes toward the enemy carrier.
    /// Uses PlayerCombat if available, otherwise applies direct force.
    /// </summary>
    private void AttemptDash(Transform target)
    {
        if (_dashOnCooldown) return;

        _dashOnCooldown = true;
        _dashCooldownTimer = botData.dashCooldown;

        // Check if enemy is holding disc (stagger condition)
        PlayerDiscHandler enemyDiscHandler = target.GetComponent<PlayerDiscHandler>();
        BotAIController enemyBot = target.GetComponent<BotAIController>();
        bool targetHasDisc = (enemyDiscHandler && enemyDiscHandler.IsHoldingDisc())
                          || (enemyBot && enemyBot.IsHoldingDisc());

        if (!targetHasDisc) return;

        IStaggerable staggerable = target.GetComponent<IStaggerable>();
        if (staggerable == null || staggerable.IsStaggered()) return;

        Vector3 knockBackDirection = (target.position - transform.position).normalized;

        // Force enemy to drop disc
        if (enemyDiscHandler) enemyDiscHandler.OnDiscLost();
        if (enemyBot) enemyBot.OnDiscLost();

        // Release disc into open space
        Vector3 discKnockaway = (knockBackDirection + Vector3.up * 3f).normalized;
        disc.SetFree(discKnockaway * 6f);

        // Apply stagger to enemy
        staggerable.ApplyStagger(knockBackDirection, 3f);

        Debug.Log($"[BotAIController] {gameObject.name} dashed into {target.name} — stagger applied!");
    }

    // -------------------------------------------------------------------------
    // DECISION HELPERS
    // -------------------------------------------------------------------------

    private bool ShouldShoot()
    {
        if (!_isHoldingDisc) return false;
        if (!opposingGoal) return false;
        if (!opposingGoal.IsGoalActive()) return false;

        float distToGoal = Vector3.Distance(transform.position, opposingGoal.transform.position);
        return distToGoal <= botData.shootRange;
    }

    private bool ShouldIntercept()
    {
        Transform enemyCarrier = GetEnemyDiscCarrier();
        if (!enemyCarrier) return false;

        float dist = Vector3.Distance(transform.position, enemyCarrier.position);
        return dist <= botData.interceptTriggerRange;
    }

    private bool IsDiscFree()
    {
        if (!disc) return false;
        return disc.currentState == DiscController.DiscState.Free;
    }

    private bool EnemyTeamHasDisc()
    {
        if (!disc) return false;
        if (disc.currentState != DiscController.DiscState.Held) return false;

        return GetEnemyDiscCarrier();
    }

    private bool TeammateHasDisc()
    {
        if (!disc) return false;
        if (disc.currentState != DiscController.DiscState.Held) return false;

        if (!teammate) return false;

        PlayerDiscHandler teammateHandler = teammate.GetComponent<PlayerDiscHandler>();
        BotAIController teammateBot = teammate.GetComponent<BotAIController>();

        bool teammateHolding = (teammateHandler && teammateHandler.IsHoldingDisc())
                            || (teammateBot && teammateBot.IsHoldingDisc());

        return teammateHolding;
    }

    /// <summary>
    /// Finds the enemy character currently holding the disc.
    /// Checks both PlayerDiscHandler and BotAIController.
    /// </summary>
    private Transform GetEnemyDiscCarrier()
    {
        if (!disc) return null;
        if (disc.currentState != DiscController.DiscState.Held) return null;

        // The disc's parent is the current holder when SetHeld() was using parenting.
        // Since we fixed SetHeld() to NOT parent, we use PossessionManager instead.
        if (!possessionManager) return null;

        // If enemy team is possessing, find their carrier in the scene
        TeamType enemyTeam = GetEnemyTeam();
        if (possessionManager.PossessingTeam != enemyTeam) return null;

        // Scan for any character holding the disc that is on the enemy team
        PlayerDiscHandler[] allHandlers = FindObjectsByType<PlayerDiscHandler>(FindObjectsSortMode.None);
        for (int i = 0; i < allHandlers.Length; i++)
        {
            if (allHandlers[i].IsHoldingDisc())
            {
                TeamType handlerTeam = GetTeamFromGameObject(allHandlers[i].gameObject);
                if (handlerTeam == enemyTeam)
                {
                    return allHandlers[i].transform;
                }
            }
        }

        BotAIController[] allBots = FindObjectsByType<BotAIController>(FindObjectsSortMode.None);
        for (int i = 0; i < allBots.Length; i++)
        {
            if (allBots[i] != this && allBots[i].IsHoldingDisc())
            {
                if (allBots[i].Team == enemyTeam)
                {
                    return allBots[i].transform;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the best teammate to pass to.
    /// Prefers teammates closer to the opposing goal and not covered by enemies.
    /// </summary>
    private Transform GetBestPassTarget()
    {
        if (!teammate) return null;

        float distToTeammate = Vector3.Distance(transform.position, teammate.position);

        if (distToTeammate > botData.passSearchRadius) return null;

        // Make sure teammate is not currently staggered
        IStaggerable teammateStaggerable = teammate.GetComponent<IStaggerable>();
        if (teammateStaggerable != null && teammateStaggerable.IsStaggered()) return null;

        return teammate;
    }

    /// <summary>
    /// Returns a support position offset from the disc — open space for receiving a pass.
    /// </summary>
    private Vector3 GetSupportPosition()
    {
        if (!disc) return transform.position;

        // Stand to the side of the disc carrier at a comfortable pass distance
        Vector3 offset = new Vector3(4f, 0f, 3f);
        return disc.transform.position + offset;
    }

    private TeamType GetEnemyTeam()
    {
        if (botData.team == TeamType.TeamA) return TeamType.TeamB;
        if (botData.team == TeamType.TeamB) return TeamType.TeamA;
        return TeamType.None;
    }

    private TeamType GetTeamFromGameObject(GameObject obj)
    {
        if (obj.CompareTag("TeamA")) return TeamType.TeamA;
        if (obj.CompareTag("TeamB")) return TeamType.TeamB;
        return TeamType.None;
    }

    // -------------------------------------------------------------------------
    // AIM INACCURACY
    // -------------------------------------------------------------------------
    private Vector3 AddAimInaccuracy(Vector3 direction)
    {
        if (botData.aimInaccuracy <= 0f) return direction;

        float spread = botData.aimInaccuracy;

        // Random offset within a cone — no Mathf.Random so we use Unity's Random
        Vector3 randomOffset = new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread * 0.5f, spread * 0.5f),
            Random.Range(-spread, spread)
        );

        return (direction + randomOffset).normalized;
    }

    // -------------------------------------------------------------------------
    // FSM TRANSITION
    // -------------------------------------------------------------------------
    private void TransitionTo(BotState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
    }

    // -------------------------------------------------------------------------
    // NAVMESH HELPER
    // -------------------------------------------------------------------------
    private void SetAgentDestination(Vector3 destination)
    {
        if (!_agent || !_agent.isOnNavMesh) return;
        if (_agent.isStopped) _agent.isStopped = false;
        _agent.SetDestination(destination);
    }

    private float GetDistanceToDisc()
    {
        if (!disc) return float.MaxValue;
        return Vector3.Distance(transform.position, disc.transform.position);
    }

    // -------------------------------------------------------------------------
    // COOLDOWN TICKING
    // -------------------------------------------------------------------------
    private void TickDashCooldown()
    {
        if (!_dashOnCooldown) return;

        _dashCooldownTimer -= Time.deltaTime;
        if (_dashCooldownTimer <= 0f)
        {
            _dashOnCooldown = false;
            _dashCooldownTimer = 0f;
        }
    }

    private void TickStagger()
    {
        if (!_isStaggered) return;

        _staggerTimer += Time.deltaTime;
        if (_staggerTimer >= _staggerDuration)
        {
            _isStaggered = false;
            _staggerTimer = 0f;
            onBotRecovered.Invoke();
            Debug.Log($"[BotAIController] {gameObject.name} recovered from stagger.");
        }
    }

    // -------------------------------------------------------------------------
    // IDISCINTERACTOR
    // -------------------------------------------------------------------------
    public Transform GetTransform()
    {
        return transform;
    }

    public void OnDiscReceived(DiscController discController)
    {
        disc = discController;
        CatchDisc();
    }

    public void OnDiscLost()
    {
        if (!_isHoldingDisc) return;

        _isHoldingDisc = false;
        _holdTimer = 0f;
    }

    public bool IsHoldingDisc()
    {
        return _isHoldingDisc;
    }

    // -------------------------------------------------------------------------
    // ISTAGGERABLE
    // -------------------------------------------------------------------------
    public void ApplyStagger(Vector3 knockbackDirection, float knockbackForce)
    {
        _isStaggered = true;
        _staggerTimer = 0f;

        // Drop disc if holding
        if (_isHoldingDisc)
        {
            OnDiscLost();
        }

        // Apply physical knockBack via NavMeshAgent warp
        Vector3 knockBackTarget = transform.position + knockbackDirection * 0.8f;
        if (_agent && _agent.isOnNavMesh)
        {
            _agent.Warp(knockBackTarget);
        }

        onBotStaggered.Invoke();
        Debug.Log($"[BotAIController] {gameObject.name} staggered!");
    }

    public bool IsStaggered()
    {
        return _isStaggered;
    }

    // -------------------------------------------------------------------------
    // IRESETTABLE
    // -------------------------------------------------------------------------
    public void ResetToSpawn(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Clear all state
        _isHoldingDisc = false;
        _holdTimer = 0f;
        _isStaggered = false;
        _staggerTimer = 0f;
        _currentState = BotState.Idle;

        // Warp NavMeshAgent to spawn position
        if (_agent && _agent.isOnNavMesh)
        {
            _agent.Warp(spawnPosition);
        }
        else
        {
            transform.position = spawnPosition;
        }

        transform.rotation = spawnRotation;

        Debug.Log($"[BotAIController] {gameObject.name} reset to spawn at {spawnPosition}.");
    }

    public void FreezePlayer()
    {
        _isFrozen = true;

        if (_agent)
        {
            _agent.ResetPath();
            _agent.isStopped = true;
        }
    }

    public void UnfreezePlayer()
    {
        _isFrozen = false;

        if (_agent)
        {
            _agent.isStopped = false;
        }
    }

    // -------------------------------------------------------------------------
    // SETUP & VALIDATION
    // -------------------------------------------------------------------------
    private void ConfigureAgent()
    {
        if (!_agent) return;

        _agent.speed = botData.moveSpeed;
        _agent.angularSpeed = botData.angularSpeed;
        _agent.acceleration = botData.acceleration;
        _agent.stoppingDistance = botData.catchRadius * 0.8f;
        _agent.autoBraking = true;
    }

    private void ValidateReferences()
    {
        if (!botData)
            Debug.LogError($"[BotAIController] {gameObject.name} — BotData SO not assigned!");
        if (!disc)
            Debug.LogError($"[BotAIController] {gameObject.name} — DiscController not assigned!");
        if (!possessionManager)
            Debug.LogWarning($"[BotAIController] {gameObject.name} — PossessionManager not assigned.");
        if (!ownGoal)
            Debug.LogWarning($"[BotAIController] {gameObject.name} — ownGoal not assigned.");
        if (!opposingGoal)
            Debug.LogWarning($"[BotAIController] {gameObject.name} — opposingGoal not assigned.");
        if (!teammate)
            Debug.LogWarning($"[BotAIController] {gameObject.name} — teammate not assigned.");
        if (!GetComponent<NavMeshAgent>())
            Debug.LogError($"[BotAIController] {gameObject.name} — NavMeshAgent component missing!");
    }
}