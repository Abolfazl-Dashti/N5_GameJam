using UnityEngine;
using UnityEngine.Events;

public class MatchManager : MonoBehaviour
{
    public enum MatchState
    {
        WaitingToStart,   // Pre-match countdown / menu
        InProgress,       // Active play — timer running
        PostGoal,         // Goal scored — timer paused, respawn pending
        MatchOver         // Timer hit zero or external end condition
    }

    // -------------------------------------------------------------------------
    // INSPECTOR REFERENCES
    // -------------------------------------------------------------------------
    [Header("Data")]
    [SerializeField] private MatchData matchData;

    [Header("Scene References — Characters")]
    [Tooltip("The human Player GameObject. Must implement IResettable.")]
    [SerializeField] private GameObject playerObject;

    [Tooltip("TeamA's Bot (Teammate). Must implement IResettable.")]
    [SerializeField] private GameObject teamABotObject;

    [Tooltip("TeamB Bot 1. Must implement IResettable.")]
    [SerializeField] private GameObject teamBBot1Object;

    [Tooltip("TeamB Bot 2. Must implement IResettable.")]
    [SerializeField] private GameObject teamBBot2Object;

    [Header("Scene References — Systems")]
    [SerializeField] private DiscController disc;
    [SerializeField] private PossessionManager possessionManager;
    [SerializeField] private ScoreManager scoreManager;

    [Tooltip("Both GoalControllers in the scene. Drag both here.")]
    [SerializeField] private GoalController[] goalControllers;

    [Header("Events")]
    [Tooltip("Fires when the match starts. Use to show HUD, hide menus.")]
    public UnityEvent onMatchStarted;

    [Tooltip("Fires every second with remaining match time in seconds.")]
    public UnityEvent<float> onMatchTimerTick;

    [Tooltip("Fires when a goal triggers the post-goal pause. " +
             "Passes scoring team and points.")]
    public UnityEvent<TeamType, int> onPostGoalPauseStarted;

    [Tooltip("Fires when post-goal reset is complete and match resumes.")]
    public UnityEvent onMatchResumed;

    [Tooltip("Fires when match time hits zero. Passes the winning TeamType " +
             "(TeamType.None = draw).")]
    public UnityEvent<TeamType> onMatchOver;

    // -------------------------------------------------------------------------
    // PRIVATE STATE
    // -------------------------------------------------------------------------
    private MatchState _currentState = MatchState.WaitingToStart;

    // Match timer
    private float _matchTimeRemaining = 0f;
    private float _timerTickAccumulator = 0f;

    // Post-goal
    private float _postGoalTimer = 0f;
    private bool _centerSpawnFreezeActive = false;
    private float _centerSpawnFreezeTimer = 0f;

    // Cached IResettable references — resolved once on Awake
    private IResettable _playerResettable;
    private IResettable _teamABotResettable;
    private IResettable _teamBBot1Resettable;
    private IResettable _teamBBot2Resettable;

    // Cached Rigidbody of the disc for respawn
    private Rigidbody _discRigidbody;

    // -------------------------------------------------------------------------
    // PUBLIC READ-ONLY ACCESSORS
    // -------------------------------------------------------------------------
    public MatchState CurrentState { get { return _currentState; } }
    public float MatchTimeRemaining { get { return _matchTimeRemaining; } }

    // -------------------------------------------------------------------------
    // UNITY LIFECYCLE
    // -------------------------------------------------------------------------
    private void Awake()
    {
        ValidateReferences();
        CacheResettables();
        CacheDiscRigidbody();
    }

    private void OnEnable()
    {
        SubscribeToGoalEvents();
        SubscribeToScoreEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromGoalEvents();
        UnsubscribeFromScoreEvents();
    }

    private void Start()
    {
        // Auto-start match on play for Game Jam simplicity.
        // Replace with StartMatch() call from a menu button if needed.
        StartMatch();
    }

    private void Update()
    {
        switch (_currentState)
        {
            case MatchState.InProgress:
                TickMatchTimer();
                break;

            case MatchState.PostGoal:
                TickPostGoalPause();
                break;

            case MatchState.WaitingToStart:
            case MatchState.MatchOver:
                break;
        }
    }

    // -------------------------------------------------------------------------
    // EVENT SUBSCRIPTIONS
    // -------------------------------------------------------------------------
    private void SubscribeToGoalEvents()
    {
        if (goalControllers == null) return;

        for (int i = 0; i < goalControllers.Length; i++)
        {
            if (goalControllers[i] != null)
            {
                goalControllers[i].onGoalScored.AddListener(OnGoalScored);
            }
        }
    }

    private void UnsubscribeFromGoalEvents()
    {
        if (goalControllers == null) return;

        for (int i = 0; i < goalControllers.Length; i++)
        {
            if (goalControllers[i] != null)
            {
                goalControllers[i].onGoalScored.RemoveListener(OnGoalScored);
            }
        }
    }

    private void SubscribeToScoreEvents()
    {
        if (scoreManager != null)
        {
            scoreManager.onGoalScored.AddListener(OnScoreManagerGoalScored);
        }
    }

    private void UnsubscribeFromScoreEvents()
    {
        if (scoreManager != null)
        {
            scoreManager.onGoalScored.RemoveListener(OnScoreManagerGoalScored);
        }
    }

    // -------------------------------------------------------------------------
    // GOAL EVENT HANDLERS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by GoalController.onGoalScored when a valid goal is detected.
    /// Pauses the match timer and begins the post-goal sequence.
    /// </summary>
    private void OnGoalScored(TeamType scoringTeam, int pointsAwarded)
    {
        if (_currentState != MatchState.InProgress) return;

        Debug.Log($"[MatchManager] Goal scored by {scoringTeam} for {pointsAwarded} points. " +
                  $"Pausing match timer.");

        BeginPostGoalPause(scoringTeam, pointsAwarded);
    }

    /// <summary>
    /// Called by ScoreManager.onGoalScored — secondary listener for logging/UI.
    /// Main logic runs in OnGoalScored above.
    /// </summary>
    private void OnScoreManagerGoalScored(TeamType scoringTeam, int pointsAwarded)
    {
        // Additional reactions to a scored goal can go here (e.g., music sting)
    }

    // -------------------------------------------------------------------------
    // MATCH STATE TRANSITIONS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Starts the match from WaitingToStart state.
    /// Resets timer, repositions everyone, fires start event.
    /// </summary>
    public void StartMatch()
    {
        if (_currentState != MatchState.WaitingToStart)
        {
            Debug.LogWarning("[MatchManager] StartMatch called but match is not in WaitingToStart state.");
            return;
        }

        _matchTimeRemaining = matchData.matchDuration;
        _timerTickAccumulator = 0f;

        // Reset scores
        if (scoreManager != null)
        {
            scoreManager.ResetScores();
        }

        // Initial center spawn
        ExecuteCenterSpawn();

        _currentState = MatchState.InProgress;

        onMatchStarted.Invoke();

        Debug.Log("[MatchManager] Match started! Timer running.");
    }

    /// <summary>
    /// Pauses the 8-minute timer and begins the post-goal pause sequence.
    /// </summary>
    private void BeginPostGoalPause(TeamType scoringTeam, int pointsAwarded)
    {
        _currentState = MatchState.PostGoal;
        _postGoalTimer = 0f;

        // Freeze all characters in place during post-goal pause
        FreezeAllCharacters();

        onPostGoalPauseStarted.Invoke(scoringTeam, pointsAwarded);
    }

    /// <summary>
    /// Called when the post-goal delay finishes.
    /// Repositions everyone and resumes the match timer.
    /// </summary>
    private void ResumeAfterGoal()
    {
        // Reset possession and chain
        if (possessionManager != null)
        {
            possessionManager.ResetForCenterSpawn();
        }

        // Reposition disc and all characters
        ExecuteCenterSpawn();

        // Begin center freeze countdown
        _centerSpawnFreezeActive = true;
        _centerSpawnFreezeTimer = 0f;

        // Match timer resumes after center freeze ends (in TickCenterSpawnFreeze)
        Debug.Log("[MatchManager] Post-goal reset complete. Center spawn freeze started.");
    }

    /// <summary>
    /// Called after center spawn freeze ends — unfreezes all players and
    /// officially transitions back to InProgress.
    /// </summary>
    private void ResumeMatch()
    {
        _currentState = MatchState.InProgress;
        _centerSpawnFreezeActive = false;

        UnfreezeAllCharacters();

        onMatchResumed.Invoke();

        Debug.Log("[MatchManager] Match resumed. Timer running.");
    }

    /// <summary>
    /// Called when the 8-minute timer reaches zero.
    /// </summary>
    private void EndMatch()
    {
        _currentState = MatchState.MatchOver;

        FreezeAllCharacters();

        TeamType winner = scoreManager != null
            ? scoreManager.GetWinner()
            : TeamType.None;

        Debug.Log($"[MatchManager] Match over! Winner: {winner}. " +
                  $"Final Score — TeamA: {scoreManager.TeamAScore} | " +
                  $"TeamB: {scoreManager.TeamBScore}");

        onMatchOver.Invoke(winner);
    }

    // -------------------------------------------------------------------------
    // TIMER TICKING
    // -------------------------------------------------------------------------

    /// <summary>
    /// Counts down the 8-minute match timer.
    /// Only runs during InProgress state — automatically paused in PostGoal.
    /// </summary>
    private void TickMatchTimer()
    {
        _matchTimeRemaining -= Time.deltaTime;
        _timerTickAccumulator += Time.deltaTime;

        // Fire tick event once per second for UI
        if (_timerTickAccumulator >= 1f)
        {
            _timerTickAccumulator -= 1f;
            onMatchTimerTick.Invoke(_matchTimeRemaining);
        }

        if (_matchTimeRemaining <= 0f)
        {
            _matchTimeRemaining = 0f;
            EndMatch();
        }
    }

    /// <summary>
    /// Counts down the post-goal pause.
    /// Timer is NOT running during this state — it resumes after.
    /// </summary>
    private void TickPostGoalPause()
    {
        _postGoalTimer += Time.deltaTime;

        if (_postGoalTimer >= matchData.postGoalRespawnDelay)
        {
            _postGoalTimer = 0f;
            ResumeAfterGoal();
        }

        // If center spawn freeze is running, tick it too
        if (_centerSpawnFreezeActive)
        {
            TickCenterSpawnFreeze();
        }
    }

    /// <summary>
    /// Short freeze window after center spawn before match resumes.
    /// Gives players a moment to orient before the disc is live.
    /// </summary>
    private void TickCenterSpawnFreeze()
    {
        _centerSpawnFreezeTimer += Time.deltaTime;

        if (_centerSpawnFreezeTimer >= matchData.centerSpawnFreezeTime)
        {
            _centerSpawnFreezeTimer = 0f;
            ResumeMatch();
        }
    }

    // -------------------------------------------------------------------------
    // CENTER SPAWN
    // -------------------------------------------------------------------------

    /// <summary>
    /// Teleports all characters and the disc to center spawn positions.
    /// Calls IResettable.ResetToSpawn on each character.
    /// </summary>
    private void ExecuteCenterSpawn()
    {
        Quaternion spawnRotation = Quaternion.Euler(0f, matchData.spawnFacingYRotation, 0f);

        // Reset all characters
        ResetCharacter(_playerResettable, matchData.playerSpawnPosition, spawnRotation);
        ResetCharacter(_teamABotResettable, matchData.teamABotSpawnPosition, spawnRotation);
        ResetCharacter(_teamBBot1Resettable, matchData.teamBBot1SpawnPosition, spawnRotation);
        ResetCharacter(_teamBBot2Resettable, matchData.teamBBot2SpawnPosition, spawnRotation);

        // Reset disc
        ResetDisc();

        Debug.Log("[MatchManager] Center spawn executed.");
    }

    private void ResetCharacter(IResettable resettable, Vector3 position, Quaternion rotation)
    {
        if (resettable == null) return;
        resettable.ResetToSpawn(position, rotation);
    }

    /// <summary>
    /// Returns the disc to center spawn — fully free with zero velocity.
    /// </summary>
    private void ResetDisc()
    {
        if (!disc) return;

        // If disc is held, force release first
        if (disc.currentState == DiscController.DiscState.Held)
        {
            disc.SetFree(Vector3.zero);
        }

        // Teleport disc to center
        disc.transform.position = matchData.discSpawnPosition;
        disc.transform.rotation = Quaternion.Euler(matchData.discSpawnRotation);

        // Kill all velocity
        if (_discRigidbody != null)
        {
            _discRigidbody.linearVelocity = Vector3.zero;
            _discRigidbody.angularVelocity = Vector3.zero;
        }

        // Ensure disc is in Free state with no holder
        disc.SetFree(Vector3.zero);

        Debug.Log($"[MatchManager] Disc reset to {matchData.discSpawnPosition}.");
    }

    // -------------------------------------------------------------------------
    // FREEZE / UNFREEZE ALL CHARACTERS
    // -------------------------------------------------------------------------
    private void FreezeAllCharacters()
    {
        FreezeCharacter(_playerResettable);
        FreezeCharacter(_teamABotResettable);
        FreezeCharacter(_teamBBot1Resettable);
        FreezeCharacter(_teamBBot2Resettable);
    }

    private void UnfreezeAllCharacters()
    {
        UnfreezeCharacter(_playerResettable);
        UnfreezeCharacter(_teamABotResettable);
        UnfreezeCharacter(_teamBBot1Resettable);
        UnfreezeCharacter(_teamBBot2Resettable);
    }

    private void FreezeCharacter(IResettable resettable)
    {
        if (resettable == null) return;
        resettable.FreezePlayer();
    }

    private void UnfreezeCharacter(IResettable resettable)
    {
        if (resettable == null) return;
        resettable.UnfreezePlayer();
    }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns remaining time as a formatted MM:SS string for UI display.
    /// </summary>
    public string GetMatchTimeDisplayString()
    {
        float time = Mathf.Max(0f, _matchTimeRemaining);
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    /// <summary>
    /// Force-ends the match early. Useful for debug or external triggers.
    /// </summary>
    public void ForceEndMatch()
    {
        if (_currentState == MatchState.MatchOver) return;
        EndMatch();
    }

    // -------------------------------------------------------------------------
    // CACHE & VALIDATION
    // -------------------------------------------------------------------------
    private void CacheResettables()
    {
        _playerResettable = CacheResettable(playerObject, "Player");
        _teamABotResettable = CacheResettable(teamABotObject, "TeamA Bot");
        _teamBBot1Resettable = CacheResettable(teamBBot1Object, "TeamB Bot 1");
        _teamBBot2Resettable = CacheResettable(teamBBot2Object, "TeamB Bot 2");
    }

    private IResettable CacheResettable(GameObject obj, string label)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[MatchManager] {label} GameObject is not assigned.");
            return null;
        }

        IResettable resettable = obj.GetComponent<IResettable>();

        if (resettable == null)
        {
            Debug.LogError($"[MatchManager] {label} does not implement IResettable! " +
                           $"Add IResettable to its controller script.");
        }

        return resettable;
    }

    private void CacheDiscRigidbody()
    {
        if (disc != null)
        {
            _discRigidbody = disc.GetComponent<Rigidbody>();
        }
    }

    private void ValidateReferences()
    {
        if (!matchData)
            Debug.LogError("[MatchManager] MatchData SO is not assigned!");
        if (!disc)
            Debug.LogError("[MatchManager] DiscController is not assigned!");
        if (!possessionManager)
            Debug.LogWarning("[MatchManager] PossessionManager is not assigned.");
        if (!scoreManager)
            Debug.LogWarning("[MatchManager] ScoreManager is not assigned.");
        if (goalControllers == null || goalControllers.Length == 0)
            Debug.LogWarning("[MatchManager] No GoalControllers assigned — " +
                             "goal events won't trigger post-goal pause.");
    }

    // -------------------------------------------------------------------------
    // DEBUG GUI
    // -------------------------------------------------------------------------
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 150, 280, 100));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Match State:   {_currentState}");
        GUILayout.Label($"Match Time:    {GetMatchTimeDisplayString()}");

        if (_currentState == MatchState.PostGoal)
        {
            GUILayout.Label($"Post-Goal:     {(_postGoalTimer).ToString("F1")}s " +
                            $"/ {matchData.postGoalRespawnDelay}s");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}