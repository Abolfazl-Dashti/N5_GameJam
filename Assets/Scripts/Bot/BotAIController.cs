using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class BotAIController : MonoBehaviour, IDiscInteractor, IStaggerable, IResettable
{
    public enum BotState
    {
        Idle,
        ChaseDisc,
        HoldAndPass,
        ShootAtGoal,
        Defend,
        Intercept
    }

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
    
    private Vector3 _debugLastChaseTarget;

    private float _catchBuffer;
    private const float CatchBufferDuration = 0.5f;

    [Header("Events")]
    public UnityEvent onBotCaughtDisc;
    public UnityEvent onBotThrewDisc;
    public UnityEvent onBotStaggered;
    public UnityEvent onBotRecovered;

    private BotState _currentState = BotState.Idle;
    private NavMeshAgent _agent;

    private bool _isHoldingDisc;
    private float _holdTimer;

    private bool _isStaggered;
    private float _staggerTimer;
    private float _staggerDuration = 1.5f;

    private bool _isFrozen;

    private bool _dashOnCooldown;
    private float _dashCooldownTimer;

    private float _stateEvalTimer;

    private Rigidbody _discRigidbody;

    public BotState CurrentState => _currentState;
    public TeamType Team => botData ? botData.team : TeamType.None;

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
        if (_isFrozen)
        {
            _agent.ResetPath();
            return;
        }

        TickStagger();
        TickDashCooldown();
        TickCatchBuffer();

        if (_isStaggered)
        {
            _agent.ResetPath();
            return;
        }

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
        else
        {
            AttemptProximityCatch();
        }
    }

    private void AttemptProximityCatch()
    {
        if (_isHoldingDisc) return;
        if (_catchBuffer > 0f) return;
        if (!IsDiscChaseable()) return;

        float horizontalDist = GetHorizontalDistanceToDisc();
        if (horizontalDist > botData.catchRadius) return;

        float verticalDist = Mathf.Abs(transform.position.y - disc.transform.position.y);
        if (verticalDist > botData.catchVerticalTolerance) return;

        CatchDisc();
    }

    private void TickCatchBuffer()
    {
        if (_catchBuffer > 0f)
        {
            _catchBuffer -= Time.deltaTime;
        }
    }

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
        if (_isHoldingDisc && (lastHolder == null || lastHolder == transform))
        {
            _isHoldingDisc = false;
            _holdTimer = 0f;
        }
    }

    private void OnDiscHeld(Transform newHolder)
    {
        if (newHolder != transform)
        {
            // Another character has the disc — FSM will react on next evaluation.
        }
    }

    // -------------------------------------------------------------------------
    // FSM — STATE EVALUATION
    // -------------------------------------------------------------------------
    private void EvaluateState()
    {
        if (_isFrozen)
        {
            TransitionTo(BotState.Idle);
            return;
        }

        if (_isHoldingDisc)
        {
            if (CanAttackGoal())
            {
                TransitionTo(BotState.ShootAtGoal);
            }
            else
            {
                TransitionTo(BotState.HoldAndPass);
            }
            return;
        }

        if (ShouldIntercept())
        {
            TransitionTo(BotState.Intercept);
            return;
        }

        if (IsDiscChaseable())
        {
            TransitionTo(BotState.ChaseDisc);
            return;
        }

        if (EnemyTeamHasDisc())
        {
            TransitionTo(BotState.Defend);
            return;
        }

        if (TeammateHasDisc())
        {
            TransitionTo(BotState.HoldAndPass);
            return;
        }

        TransitionTo(BotState.Idle);
    }
    
    private bool CanAttackGoal()
    {
        if (!_isHoldingDisc) return false;
        if (!opposingGoal) return false;
        return opposingGoal.IsGoalActive();
    }

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

    private void ExecuteIdle()
    {
        _agent.ResetPath();
    }

    private void ExecuteChaseDisc()
    {
        if (!disc) return;

        Vector3 chaseTarget = GetDiscChaseTarget();
        _debugLastChaseTarget = chaseTarget;  // for Gizmo visualization
        SetAgentDestination(chaseTarget);
    }

    private void ExecuteHoldAndPass()
    {
        if (!_isHoldingDisc)
        {
            if (disc)
            {
                SetAgentDestination(GetSupportPosition());
            }
            return;
        }

        if (IsUnderPressure())
        {
            AttemptPass();
            return;
        }

        if (_holdTimer >= botData.holdDecisionTime)
        {
            AttemptPass();
        }
        else
        {
            if (_agent && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }
    }

    private bool IsUnderPressure()
    {
        float pressureRange = botData.dashTriggerRange * 1.5f;
        TeamType enemyTeam = GetEnemyTeam();

        PlayerDiscHandler[] allHandlers =
            FindObjectsByType<PlayerDiscHandler>(FindObjectsSortMode.None);

        for (int i = 0; i < allHandlers.Length; i++)
        {
            TeamType handlerTeam = GetTeamFromGameObject(allHandlers[i].gameObject);
            if (handlerTeam != enemyTeam) continue;

            float dist = Vector3.Distance(transform.position, allHandlers[i].transform.position);
            if (dist <= pressureRange) return true;
        }

        BotAIController[] allBots =
            FindObjectsByType<BotAIController>(FindObjectsSortMode.None);

        for (int i = 0; i < allBots.Length; i++)
        {
            if (allBots[i] == this) continue;
            if (allBots[i].Team != enemyTeam) continue;

            float dist = Vector3.Distance(transform.position, allBots[i].transform.position);
            if (dist <= pressureRange) return true;
        }

        return false;
    }

    private void ExecuteShootAtGoal()
    {
        if (!_isHoldingDisc)
        {
            TransitionTo(BotState.ChaseDisc);
            return;
        }

        // NEW: even mid-approach to shoot, bail into a pass if pressured —
        // closes the gap where a bot would tunnel-vision toward the goal
        // and get dashed/staggered without ever considering a safer pass.
        if (IsUnderPressure())
        {
            AttemptPass();
            return;
        }

        if (!opposingGoal) return;

        Vector3 goalPosition = opposingGoal.transform.position;
        float distToGoal = Vector3.Distance(transform.position, goalPosition);

        if (distToGoal <= botData.shootRange)
        {
            ShootAtGoal();
        }
        else
        {
            SetAgentDestination(goalPosition);
        }
    }

    private void ExecuteDefend()
    {
        if (!ownGoal) return;

        Transform enemyCarrier = GetEnemyDiscCarrier();

        Vector3 defendTarget;

        if (enemyCarrier)
        {
            Vector3 goalPos = ownGoal.transform.position;
            Vector3 enemyPos = enemyCarrier.position;
            defendTarget = Vector3.Lerp(goalPos, enemyPos, 0.4f);
        }
        else
        {
            defendTarget = ownGoal.transform.position +
                           ownGoal.transform.forward * botData.defendRadius;
        }

        SetAgentDestination(defendTarget);
    }

    private void ExecuteIntercept()
    {
        Transform enemyCarrier = GetEnemyDiscCarrier();

        if (!enemyCarrier)
        {
            TransitionTo(BotState.Defend);
            return;
        }

        float distToEnemy = Vector3.Distance(transform.position, enemyCarrier.position);

        SetAgentDestination(enemyCarrier.position);

        if (distToEnemy <= botData.dashTriggerRange && !_dashOnCooldown)
        {
            AttemptDash(enemyCarrier);
        }
    }

    // -------------------------------------------------------------------------
    // DISC INTERACTION
    // -------------------------------------------------------------------------
    private void CatchDisc()
    {
        if (_isHoldingDisc) return;
        if (!disc) return;
        if (disc.currentState == DiscController.DiscState.Held) return;

        _isHoldingDisc = true;
        _holdTimer = 0f;

        // Smooth magnetic-pull catch for visual consistency with the player.
        disc.RequestCatch(transform);
        onBotCaughtDisc.Invoke();

        Debug.Log($"[BotAIController] {gameObject.name} caught the disc.");
    }

    private void ThrowDiscAt(Vector3 targetPosition, float speedOverride = -1f)
    {
        if (!_isHoldingDisc || !disc) return;

        Vector3 direction = (targetPosition - disc.transform.position).normalized;
        direction = AddAimInaccuracy(direction);

        _isHoldingDisc = false;
        _holdTimer = 0f;
        _catchBuffer = CatchBufferDuration;

        disc.SetPassed(direction, speedOverride);
        onBotThrewDisc.Invoke();

        Debug.Log($"[BotAIController] {gameObject.name} threw the disc toward {targetPosition}.");
    }

    private void AttemptPass()
    {
        if (!_isHoldingDisc) return;

        Transform passTarget = GetBestPassTarget();

        if (passTarget)
        {
            ThrowDiscAt(passTarget.position);
            Debug.Log($"[BotAIController] {gameObject.name} passed to {passTarget.name}.");
            return;
        }

        if (ShouldShoot())
        {
            ShootAtGoal();
            return;
        }

        if (IsUnderPressure())
        {
            Vector3 panicTarget = transform.position + transform.forward * 10f;
            ThrowDiscAt(panicTarget);
            Debug.Log($"[BotAIController] {gameObject.name} panic-threw the disc under pressure!");
            return;
        }

        // SAFETY NET (fixes the freeze): no valid pass, can't shoot (goal not yet
        // active), and no enemy pressure detected. Without this, the bot would hold
        // the disc and do nothing until the 30s attack timer forcibly resets
        // possession — a visible, game-breaking freeze from the player's perspective.
        if (_holdTimer >= botData.forcedReleaseTimeout)
        {
            Vector3 safeTarget = transform.position + transform.forward * 8f;
            ThrowDiscAt(safeTarget);

            Debug.LogWarning($"[BotAIController] {gameObject.name} forced a safety release " +
                             $"after {_holdTimer:F1}s with no valid pass/shot/pressure option " +
                             "Check teammate assignment and passSearchRadius in the Inspector");
        }
    }

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
    private void AttemptDash(Transform target)
    {
        if (_dashOnCooldown) return;

        PlayerDiscHandler enemyDiscHandler = target.GetComponent<PlayerDiscHandler>();
        BotAIController enemyBot = target.GetComponent<BotAIController>();
        bool targetHasDisc = (enemyDiscHandler && enemyDiscHandler.IsHoldingDisc())
                             || (enemyBot && enemyBot.IsHoldingDisc());

        if (!targetHasDisc) return;

        IStaggerable staggerable = target.GetComponent<IStaggerable>();
        if (staggerable == null || staggerable.IsStaggered()) return;

        // CHANGED: cooldown only starts once we know the dash will actually happen.
        _dashOnCooldown = true;
        _dashCooldownTimer = botData.dashCooldown;

        Vector3 knockBackDirection = (target.position - transform.position).normalized;

        if (enemyDiscHandler) enemyDiscHandler.OnDiscLost();
        if (enemyBot) enemyBot.OnDiscLost();

        Vector3 discKnockaway = (knockBackDirection + Vector3.up * 3f).normalized;
        disc.SetFree(discKnockaway * 6f);

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

    private bool IsDiscChaseable()
    {
        if (!disc) return false;
        return disc.currentState == DiscController.DiscState.Free ||
               disc.currentState == DiscController.DiscState.Passed;
    }

    private bool EnemyTeamHasDisc()
    {
        if (!disc) return false;
        if (disc.currentState != DiscController.DiscState.Held) return false;

        return GetEnemyDiscCarrier() != null;
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

    private Transform GetEnemyDiscCarrier()
    {
        if (!disc) return null;
        if (disc.currentState != DiscController.DiscState.Held) return null;
        if (!possessionManager) return null;

        TeamType enemyTeam = GetEnemyTeam();
        if (possessionManager.PossessingTeam != enemyTeam) return null;

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

    private Transform GetBestPassTarget()
    {
        if (!teammate) return null;

        // Defensive check: confirm the assigned teammate Transform is actually on
        // OUR team. Catches Inspector mis-wiring (e.g., accidentally dragging an
        // enemy bot into the teammate slot) that would otherwise cause the bot to
        // "pass" the disc straight to an opponent.
        TeamType teammateTeam = GetTeamFromGameObject(teammate.gameObject);
        if (teammateTeam != Team)
        {
            Debug.LogError($"[BotAIController] {gameObject.name} — teammate field is " +
                           $"assigned to {teammate.name} which is on team {teammateTeam}, " +
                           $"not {Team}! Fix the Inspector reference");
            return null;
        }

        float distToTeammate = Vector3.Distance(transform.position, teammate.position);
        if (distToTeammate > botData.passSearchRadius) return null;

        IStaggerable teammateStaggerable = teammate.GetComponent<IStaggerable>();
        if (teammateStaggerable != null && teammateStaggerable.IsStaggered()) return null;

        return teammate;
    }

    private Vector3 GetSupportPosition()
    {
        if (!disc) return transform.position;
        if (!opposingGoal) return disc.transform.position;

        Vector3 towardGoal = (opposingGoal.transform.position - disc.transform.position).normalized;
        Vector3 lateralOffset = Vector3.Cross(towardGoal, Vector3.up) * 4f;

        return disc.transform.position + towardGoal * 5f + lateralOffset;
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

    private Vector3 AddAimInaccuracy(Vector3 direction)
    {
        if (botData.aimInaccuracy <= 0f) return direction;

        float spread = botData.aimInaccuracy;

        Vector3 randomOffset = new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread * 0.5f, spread * 0.5f),
            Random.Range(-spread, spread)
        );

        return (direction + randomOffset).normalized;
    }

    private void TransitionTo(BotState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;
    }
    
    // NAVMESH HELPERS
    private void SetAgentDestination(Vector3 destination)
    {
        if (!_agent) return;

        if (!_agent.isOnNavMesh)
        {
            TryRecoverAgentOntoNavMesh();
            if (!_agent.isOnNavMesh) return;
        }

        if (_agent.isStopped) _agent.isStopped = false;

        bool success = _agent.SetDestination(destination);
        if (!success)
        {
            Debug.LogWarning($"[BotAIController] {gameObject.name} failed to path to {destination}.");
        }
    }
    
    // NEW: last-resort recovery if the agent is ever detected off the NavMesh.
    // Prevents a permanent silent freeze.
    private void TryRecoverAgentOntoNavMesh()
    {
        float recoverRadius = botData ? botData.navMeshFallbackSampleDistance : 10f;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(transform.position, out navHit, recoverRadius, NavMesh.AllAreas))
        {
            _agent.Warp(navHit.position);
            Debug.LogWarning($"[BotAIController] {gameObject.name} was off the NavMesh — recovered to nearest point.");
        }
        else
        {
            Debug.LogError($"[BotAIController] {gameObject.name} is off the NavMesh and no recovery point " +
                           "was found nearby. Check NavMesh coverage near this position.");
        }
    }

    /// <summary>
    /// Predicts the disc's near-future position and clamps it onto the NavMesh.
    /// FIXES Bug 1: right after a lofted throw, the disc hangs in the air well above
    /// the floor (throwLoftAngle + reduced gravityScale). Without clamping, the raw
    /// predicted target is too far from any walkable surface, NavMeshAgent.SetDestination
    /// silently fails, and the agent freezes since it has no valid path to move along.
    /// </summary>
    private Vector3 GetDiscChaseTarget()
    {
        if (!disc) return transform.position;

        Vector3 discPosition = disc.transform.position;

        // STEP 1 — Predict horizontal (XZ) lead only. Vertical velocity is irrelevant
        // here because we project straight down onto the floor regardless of height.
        Vector3 predictedXZ = discPosition;
        if (_discRigidbody)
        {
            Vector3 flatVelocity = _discRigidbody.linearVelocity;
            flatVelocity.y = 0f;
            predictedXZ += flatVelocity * botData.chaseLeadTime;
        }

        // STEP 2 — Project straight down onto the floor from the predicted XZ point.
        Vector3 floorPoint;
        if (TryProjectOntoFloor(predictedXZ, out floorPoint))
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(floorPoint, out navHit, botData.navMeshSnapTolerance, NavMesh.AllAreas))
            {
                return navHit.position;
            }
        }

        // STEP 3 — Fallback: floor raycast from the disc's actual current position
        // (no horizontal prediction), in case the predicted point overshot past a wall/gap.
        if (TryProjectOntoFloor(discPosition, out floorPoint))
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(floorPoint, out navHit, botData.navMeshSnapTolerance, NavMesh.AllAreas))
            {
                return navHit.position;
            }
        }

        // STEP 4 — Fallback: the floor raycast itself found nothing beneath the disc
        // (e.g., disc is directly over a goal Mouth / out-of-bounds gap with no floor
        // collider). Widen the search directly around the disc's raw 3D position.
        NavMeshHit wideHit;
        if (NavMesh.SamplePosition(discPosition, out wideHit, botData.navMeshFallbackSampleDistance, NavMesh.AllAreas))
        {
            return wideHit.position;
        }

        // STEP 5 — Absolute last resort. Only reached if the entire arena floor
        // near the disc is unbaked/unwalkable. Hold current position rather than
        // sending the agent toward a location NavMesh has no path to.
        Debug.LogWarning($"[BotAIController] {gameObject.name} could not resolve any valid " +
                      $"NavMesh point near disc position {discPosition}. Holding position.");
        return transform.position;
    }

    /// <summary>
    /// Casts straight down from well above the given XZ column to find the floor.
    /// Works regardless of how high the disc currently is (floor-level, mid-throw,
    /// or bouncing off the ceiling) because raycast length is independent of the
    /// NavMesh sampling radius problem described above.
    /// </summary>
    private bool TryProjectOntoFloor(Vector3 origin, out Vector3 floorPoint)
    {
        floorPoint = origin;

        // Fixed origin — always starts above the true ceiling, independent of
        // where the disc currently is at the moment this is called.
        Vector3 rayStart = new Vector3(origin.x, botData.arenaCeilingHeight + 5f, origin.z);

        // Ray must travel the full vertical span: from above the ceiling, past the
        // ceiling itself, past the disc at any height, all the way down to the floor.
        float rayLength = botData.arenaCeilingHeight + 10f;

        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, rayLength, botData.floorLayerMask))
        {
            floorPoint = hit.point;
            return true;
        }

        return false;
    }

    private float GetHorizontalDistanceToDisc()
    {
        if (!disc) return float.MaxValue;
        Vector3 flat = disc.transform.position - transform.position;
        flat.y = 0f;
        return flat.magnitude;
    }

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
        // CHANGED: keep the cached Rigidbody in sync if the disc reference ever changes.
        if (discController && discController != disc)
        {
            disc = discController;
            _discRigidbody = disc.GetComponent<Rigidbody>();
        }

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

        if (_isHoldingDisc)
        {
            OnDiscLost();
        }

        if (_agent && _agent.isOnNavMesh)
        {
            Vector3 knockBackTarget = transform.position + knockbackDirection * 0.8f;
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(knockBackTarget, out navHit, 2f, NavMesh.AllAreas))
            {
                _agent.Warp(navHit.position);
            }
            // If no valid point is found nearby, skip the warp entirely — better to
            // leave the bot in place than knock it fully off the NavMesh.
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
        _isHoldingDisc = false;
        _holdTimer = 0f;
        _catchBuffer = 0;
        _isStaggered = false;
        _staggerTimer = 0f;
        _currentState = BotState.Idle;

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
    
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (_currentState != BotState.ChaseDisc) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_debugLastChaseTarget, 0.4f);
        Gizmos.DrawLine(transform.position, _debugLastChaseTarget);
    }
}