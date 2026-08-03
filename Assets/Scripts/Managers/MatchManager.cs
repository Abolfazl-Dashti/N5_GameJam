using UnityEngine;
using UnityEngine.Events;

public class MatchManager : MonoBehaviour
{
    public enum MatchState
    {
        WaitingToStart,  // Pre-match countdown / menu
        InProgress,  // Active play — timer running
        PostGoal,  // Goal scored — timer paused, respawn pending
        MatchOver  // Timer hit zero or external end condition
    }

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

    private MatchState _currentState = MatchState.WaitingToStart;

    // Match timer
    private float _matchTimeRemaining;
    private float _timerTickAccumulator;

    // Post-goal
    private float _postGoalTimer;
    private bool _centerSpawnFreezeActive;
    private float _centerSpawnFreezeTimer;

    // Cached IResettable references, resolved once on Awake
    private IResettable _playerResettable;
    private IResettable _teamABotResettable;
    private IResettable _teamBBot1Resettable;
    private IResettable _teamBBot2Resettable;

    // Cached Rigidbody of the disc for respawn
    private Rigidbody _discRigidbody;

    // Property
    public MatchState CurrentState => _currentState;
    public float MatchTimeRemaining => _matchTimeRemaining;

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

    // EVENT SUBSCRIPTIONS
    private void SubscribeToGoalEvents()
    {
        if (goalControllers == null) return;

        for (int i = 0; i < goalControllers.Length; i++)
        {
            if (goalControllers[i])
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
            if (goalControllers[i])
            {
                goalControllers[i].onGoalScored.RemoveListener(OnGoalScored);
            }
        }
    }

    private void SubscribeToScoreEvents()
    {
        if (scoreManager)
        {
            scoreManager.onGoalScored.AddListener(OnScoreManagerGoalScored);
        }
    }

    private void UnsubscribeFromScoreEvents()
    {
        if (scoreManager)
        {
            scoreManager.onGoalScored.RemoveListener(OnScoreManagerGoalScored);
        }
    }

    // GOAL EVENT HANDLERS
    // Called by GoalController.onGoalScored when a valid goal is detected.
    // Pauses the match timer and begins the post-goal sequence.
    private void OnGoalScored(TeamType scoringTeam, int pointsAwarded)
    {
        if (_currentState != MatchState.InProgress) return;

        Debug.Log($"[MatchManager] Goal scored by {scoringTeam} for {pointsAwarded} points. " +
                  $"Pausing match timer.");

        BeginPostGoalPause(scoringTeam, pointsAwarded);
    }
    
    // Called by ScoreManager.onGoalScored — secondary listener for logging/UI.
    // Main logic runs in OnGoalScored above.
    private void OnScoreManagerGoalScored(TeamType scoringTeam, int pointsAwarded)
    {
        // Additional reactions to a scored goal can go here (e.g., music)
    }

    // MATCH STATE TRANSITIONS
    // Starts the match from WaitingToStart state
    // Resets timer, repositions everyone, fires start event
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
    
    // Pauses the 8-minute timer and begins the post-goal pause sequence
    private void BeginPostGoalPause(TeamType scoringTeam, int pointsAwarded)
    {
        _currentState = MatchState.PostGoal;
        _postGoalTimer = 0f;

        // Freeze all characters in place during post-goal pause
        FreezeAllCharacters();

        onPostGoalPauseStarted.Invoke(scoringTeam, pointsAwarded);
    }
    
    // Called when the post-goal delay finishes
    // Repositions everyone and resumes the match timer
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
    
    // Called after center spawn freeze ends — unfreezes all players and
    // officially transitions back to InProgress
    private void ResumeMatch()
    {
        _currentState = MatchState.InProgress;
        _centerSpawnFreezeActive = false;

        UnfreezeAllCharacters();

        onMatchResumed.Invoke();

        Debug.Log("[MatchManager] Match resumed. Timer running.");
    }
    
    // Called when the 8-minute timer reaches zero
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
    
    // Counts down the 8-minute match timer
    // Only runs during InProgress state — automatically paused in PostGoal
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
    
    // Counts down the post-goal pause
    // Timer is NOT running during this state — it resumes after
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
    
    // Short freeze window after center spawn before match resumes
    // Gives players a moment to orient before the disc is live
    private void TickCenterSpawnFreeze()
    {
        _centerSpawnFreezeTimer += Time.deltaTime;

        if (_centerSpawnFreezeTimer >= matchData.centerSpawnFreezeTime)
        {
            _centerSpawnFreezeTimer = 0f;
            ResumeMatch();
        }
    }
    
    // Teleports all characters and the disc to center spawn positions
    // Calls IResettable.ResetToSpawn on each character
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
    
    // Returns the disc to center spawn, fully free with zero velocity
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
        if (_discRigidbody)
        {
            _discRigidbody.linearVelocity = Vector3.zero;
            _discRigidbody.angularVelocity = Vector3.zero;
        }

        // Ensure disc is in Free state with no holder
        disc.SetFree(Vector3.zero);

        Debug.Log($"[MatchManager] Disc reset to {matchData.discSpawnPosition}.");
    }

    // Freeze and UnFreeze Characters
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
    
    // Returns remaining time as a formatted MM:SS string for UI display
    public string GetMatchTimeDisplayString()
    {
        float time = Mathf.Max(0f, _matchTimeRemaining);
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    
    // Force-ends the match early. Useful for debug(not used yet)
    public void ForceEndMatch()
    {
        if (_currentState == MatchState.MatchOver) return;
        EndMatch();
    }
    
    private void CacheResettables()
    {
        _playerResettable = CacheResettable(playerObject, "Player");
        _teamABotResettable = CacheResettable(teamABotObject, "TeamA Bot");
        _teamBBot1Resettable = CacheResettable(teamBBot1Object, "TeamB Bot 1");
        _teamBBot2Resettable = CacheResettable(teamBBot2Object, "TeamB Bot 2");
    }

    private IResettable CacheResettable(GameObject obj, string label)
    {
        if (!obj)
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
        if (disc)
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

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 150, 280, 100));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Match State: {_currentState}");
        GUILayout.Label($"Match Time: {GetMatchTimeDisplayString()}");

        if (_currentState == MatchState.PostGoal)
        {
            GUILayout.Label($"Post-Goal: {(_postGoalTimer).ToString("F1")}s " +
                            $"/ {matchData.postGoalRespawnDelay}s");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}