using UnityEngine;
using UnityEngine.Events;

// Must be attached to Each goal GameObjects of TeamA & TeamB
public class GoalController : MonoBehaviour, IGoalActivating
{
    [Header("Data")]
    [SerializeField] private GoalData goalData;

    [Header("Identity")]
    [Tooltip("Which team DEFENDS this goal. TeamA defends the goal TeamB shoots at, and vice versa.")]
    [SerializeField] private TeamType defendingTeam;

    [Header("References")]
    [Tooltip("The PossessionManager in the scene.")]
    [SerializeField] private PossessionManager possessionManager;

    [Tooltip("The ScoreManager in the scene.")]
    [SerializeField] private ScoreManager scoreManager;

    [Tooltip("The Renderer on the goal visuals used to show active/inactive color. " +
             "Can be a MeshRenderer on a goal frame or trigger plane.")]
    [SerializeField] private Renderer goalRenderer;

    [Tooltip("The trigger Collider that detects the disc entering the goal mouth. " +
             "Must have IsTrigger = true.")]
    [SerializeField] private Collider goalTriggerCollider;

    [Header("Events")]
    [Tooltip("what happen when this goal becomes active & ready to receive the disc")]
    public UnityEvent onGoalActivated;

    [Tooltip("what happen when this goal becomes inactive")]
    public UnityEvent onGoalDeactivated;

    [Tooltip("Run when a valid goal is scored")]
    public UnityEvent<TeamType, int> onGoalScored;

    // don't need any attachment in Inspector(handle with MatchManager.cs)
    public UnityEvent onPostGoalReset;
    
    private bool _isActive;
    private bool _isProcessingGoal;  // prevents double-trigger during replay pause
    private float _postGoalTimer;
    
    private void Awake()
    {
        ValidateReferences();
        SetGoalVisual(goalData.inactiveColor);
    }

    private void Update()
    {
        if (_isProcessingGoal)
        {
            TickPostGoalTimer();
        }
    }

    /// <summary>
    /// Called by Unity when the disc enters the goal trigger volume.
    /// OnTriggerEnter works because the disc's collider becomes a trigger
    /// while held — but we only care about it when it is NOT held (Free or Passed).
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Ignore if already processing a goal this frame
        if (_isProcessingGoal) return;

        // Ignore if goal is inactive — shots don't count
        if (!_isActive) return;

        // Check the entering object is the disc
        DiscController disc = other.GetComponent<DiscController>();
        if (!disc) return;

        // Disc must be Free or Passed — not Held by someone standing in the goal
        if (disc.currentState == DiscController.DiscState.Held) return;

        // Determine which team scored
        // The ATTACKING team is the opposite of this goal's defending team
        TeamType scoringTeam = GetAttackingTeam();

        if (scoringTeam == TeamType.None)
        {
            Debug.LogWarning("GoalController Could not determine scoring team");
            return;
        }

        // Retrieve current multiplier from PossessionManager
        int currentMultiplier = possessionManager ? possessionManager.CurrentPassMultiplier : 1;
        ProcessGoal(scoringTeam, currentMultiplier);
    }
    
    // --------- For Goal Finding Bug in prototype(not stadium yet) ---------
    // Returns the goal's real-world scoring position. The GoalController's own
    // transform sits at the prefab/parent origin and does NOT reflect the goal's
    // actual placement in the arena — the goalTriggerCollider child does, since
    // it's the object that was actually moved into position for gameplay.
    // AI and gameplay code MUST use this instead of transform.position.
    public Vector3 GetGoalPosition()
    {
        if (goalTriggerCollider) return goalTriggerCollider.transform.position;

        Debug.LogWarning($"[GoalController] {gameObject.name} — goalTriggerCollider not assigned, " +
                         "falling back to parent transform.position (likely incorrect).");
        return transform.position;
    }

    /// <summary>
    /// A valid goal has been scored. Award points, notify systems, start pause.
    /// </summary>
    private void ProcessGoal(TeamType scoringTeam, int passMultiplier)
    {
        _isProcessingGoal = true;
        _postGoalTimer = 0f;

        // Deactivate goal immediately — no more shots during replay pause
        DeactivateGoal();

        // Award score
        if (scoreManager)
        {
            scoreManager.AddScore(scoringTeam, goalData.baseGoalPoints, passMultiplier);
        }

        // Flash scored color
        SetGoalVisual(goalData.scoredColor);

        // Notify PossessionManager to reset chain
        if (possessionManager)
        {
            possessionManager.OnGoalScored();
        }

        int pointsAwarded = goalData.baseGoalPoints * Mathf.Max(1, passMultiplier);
        onGoalScored.Invoke(scoringTeam, pointsAwarded);

        Debug.Log($"[GoalController] GOAL! {scoringTeam} scored {pointsAwarded} points " +
                  $"(x{passMultiplier} multiplier). Post-goal pause started.");
    }
    
    /// Counts down the post-goal pause (replay window).
    /// When done, fires reset event so MatchManager can respawn players.
    private void TickPostGoalTimer()
    {
        _postGoalTimer += Time.deltaTime;

        if (_postGoalTimer >= goalData.postGoalPauseDuration)
        {
            _isProcessingGoal = false;
            _postGoalTimer = 0f;

            // Restore inactive visual
            SetGoalVisual(goalData.inactiveColor);

            onPostGoalReset.Invoke();

            Debug.Log("[GoalController] Post-goal pause ended. Firing reset event.");
        }
    }
    
    public void ActivateGoal()
    {
        if (_isActive) return;
        if (_isProcessingGoal) return;

        _isActive = true;
        SetGoalVisual(goalData.activeColor);
        onGoalActivated.Invoke();

        Debug.Log($"[GoalController] {defendingTeam}'s goal is now ACTIVE.");
    }

    public void DeactivateGoal()
    {
        if (!_isActive) return;

        _isActive = false;

        // Only reset visual if not showing the scored flash
        if (!_isProcessingGoal)
        {
            SetGoalVisual(goalData.inactiveColor);
        }

        onGoalDeactivated.Invoke();

        Debug.Log($"[GoalController] {defendingTeam}'s goal is now INACTIVE.");
    }

    public bool IsGoalActive()
    {
        return _isActive;
    }

    public TeamType GetDefendingTeam()
    {
        return defendingTeam;
    }
    
    /// The attacking team is whoever is NOT defending this goal.
    private TeamType GetAttackingTeam()
    {
        if (defendingTeam == TeamType.TeamA) return TeamType.TeamB;
        if (defendingTeam == TeamType.TeamB) return TeamType.TeamA;
        return TeamType.None;
    }
    
    /// Sets the goal renderer's emission color to give visual feedback.
    /// Works with URP Lit shader — requires _EmissionColor property.
    private void SetGoalVisual(Color color)
    {
        if (!goalRenderer) return;

        // Use MaterialPropertyBlock to avoid creating new material instances
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        goalRenderer.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", color);
        block.SetColor("_BaseColor", color);
        goalRenderer.SetPropertyBlock(block);
    }

    private void ValidateReferences()
    {
        if (!goalData)
            Debug.LogError("[GoalController] GoalData SO is not assigned!");
        if (!possessionManager)
            Debug.LogError("[GoalController] PossessionManager is not assigned!");
        if (!scoreManager)
            Debug.LogError("[GoalController] ScoreManager is not assigned!");
        if (!goalTriggerCollider)
            Debug.LogWarning("[GoalController] goalTriggerCollider not assigned — " +
                             "disc entry detection won't work!");
        if (!goalRenderer)
            Debug.LogWarning("[GoalController] goalRenderer not assigned — " +
                             "visual feedback won't work.");
        if (defendingTeam == TeamType.None)
            Debug.LogError("[GoalController] defendingTeam is set to None! " +
                           "Set it to TeamA or TeamB in the Inspector.");
    }
}
