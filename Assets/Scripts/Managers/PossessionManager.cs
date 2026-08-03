using UnityEngine;
using UnityEngine.Events;

public class PossessionManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PossessionData possessionData;

    [Header("Scene References")]
    [SerializeField] private DiscController discControllerScript;

    [Tooltip("Goal that TeamA defends (TeamB shoots at it)")]
    [SerializeField] private MonoBehaviour teamAGoal;

    [Tooltip("Goal that TeamB defends (TeamA shoots at it)")]
    [SerializeField] private MonoBehaviour teamBGoal;

    [Header("Events — Broadcast to UI, GameManager, AudioManager etc")]

    [Tooltip("Fires when any team gains possession. Passes the team that now has the disc")]
    public UnityEvent<TeamType> onPossessionChanged;

    [Tooltip("Fires when a pass is successfully completed. Passes new multiplier value")]
    public UnityEvent<int> onPassCompleted;

    [Tooltip("Fires when the opposing goal is activated after first pass")]
    public UnityEvent<TeamType> onGoalActivated;

    [Tooltip("Fires when the chain resets (timer expired or interception). Passes the team that caused the reset")]
    public UnityEvent<TeamType> onChainReset;

    [Tooltip("Fires every second with the remaining attack time. For UI countdown display.")]
    public UnityEvent<float> onAttackTimerTick;

    [Tooltip("Fires when the attack timer expires.")]
    public UnityEvent onAttackTimerExpired;

    // Current possession
    private TeamType _possessingTeam = TeamType.None;
    private TeamType _previousPossessingTeam = TeamType.None;
    private GameObject _currentHolder;
    private GameObject _previousHolder;

    // Pass chain
    private int _currentPassMultiplier;
    private int _passChainCount;
    private bool _goalActivated;

    // Attack timer
    private bool _attackTimerRunning;
    private float _attackTimerRemaining;
    private float _timerTickAccumulator;

    // Cache the interfaces
    private IGoalActivating _teamAGoalInterface;
    private IGoalActivating _teamBGoalInterface;

    // Property
    public TeamType PossessingTeam => _possessingTeam;
    public int CurrentPassMultiplier => _currentPassMultiplier;
    public int PassChainCount => _passChainCount;
    public float AttackTimerRemaining => _attackTimerRemaining;
    public bool IsGoalActivated => _goalActivated;
    
    private void Awake()
    {
        ValidateReferences();
        CacheGoalInterfaces();
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
        if (_attackTimerRunning)
        {
            TickAttackTimer();
        }
    }
    
    private void SubscribeToDiscEvents()
    {
        if (!discControllerScript) return;

        discControllerScript.onDiscHeld.AddListener(OnDiscHeld);
        discControllerScript.onDiscReleased.AddListener(OnDiscReleased);
        discControllerScript.onDiscPassed.AddListener(OnDiscPassed);
    }

    private void UnsubscribeFromDiscEvents()
    {
        if (!discControllerScript) return;

        discControllerScript.onDiscHeld.RemoveListener(OnDiscHeld);
        discControllerScript.onDiscReleased.RemoveListener(OnDiscReleased);
        discControllerScript.onDiscPassed.RemoveListener(OnDiscPassed);
    }
    
    private void OnDiscHeld(Transform holder)
    {
        if (!holder)
        {
            Debug.LogWarning("PossessionManager OnDiscHeld received null holder.");
            return;
        }

        TeamType newTeam = GetTeamFromTransform(holder);
        bool isInterception = IsInterception(newTeam);
        bool isPass = IsSuccessfulPass(newTeam, holder.gameObject);

        _previousHolder = _currentHolder;
        _currentHolder = holder.gameObject;

        if (isInterception)
        {
            HandleInterception(newTeam);
        }
        else if (isPass)
        {
            HandleSuccessfulPass(newTeam);

            // Update current holder after pass resolved
            _previousHolder = null;
        }
        else if (_possessingTeam == TeamType.None || newTeam != _possessingTeam)
        {
            // Fresh pickup from free disc
            HandleFreshPossession(newTeam);
            _previousHolder = null;
        }
    }
    
    private void OnDiscPassed(Transform thrower)
    {
        if (!thrower) return;

        // The disc is now in the air — store the previous holder so we can
        // detect a successful pass when it is caught by a teammate
        _previousHolder = thrower.gameObject;
        _previousPossessingTeam = GetTeamFromTransform(thrower);

        // Timer keeps running — catching this throw will resolve the pass
    }
    
    private void OnDiscReleased(Transform lastHolder)
    {
        // Disc is free — timer continues running under current possessing team.
        // Possession only changes when someone CATCHES the disc (OnDiscHeld).
        // We don't reset the chain just because the disc is in the air.
    }
    
    private void HandleInterception(TeamType interceptingTeam)
    {
        Debug.Log($"[PossessionManager] INTERCEPTION by {interceptingTeam}! Chain reset.");

        TeamType previousTeam = _possessingTeam;

        // Reset the chain for the previous team
        ResetChain(previousTeam);

        // Give possession to the intercepting team
        HandleFreshPossession(interceptingTeam);

        onChainReset.Invoke(interceptingTeam);
    }
    
    private void HandleFreshPossession(TeamType team)
    {
        _possessingTeam = team;

        // Start or restart the attack timer
        StartAttackTimer();

        onPossessionChanged.Invoke(team);

        Debug.Log($"[PossessionManager] {team} gained possession. Attack timer started.");
    }
    
    private void HandleSuccessfulPass(TeamType team)
    {
        _passChainCount++;

        // First pass: set multiplier to 1 and activate the opposing goal
        if (_passChainCount == 1)
        {
            _currentPassMultiplier = possessionData.firstPassMultiplier;
            ActivateOpposingGoal(team);

            Debug.Log($"[PossessionManager] {team} completed FIRST pass! " +
                      $"Goal activated. Multiplier: x{_currentPassMultiplier}");
        }
        else
        {
            // Subsequent passes — increment multiplier up to max
            _currentPassMultiplier = Mathf.Min(_currentPassMultiplier + 1,
                                               possessionData.maxPassMultiplier);

            Debug.Log($"[PossessionManager] {team} completed pass #{_passChainCount}. " +
                      $"Multiplier: x{_currentPassMultiplier}");
        }

        // Restart the attack timer on each successful pass
        RestartAttackTimer();

        onPassCompleted.Invoke(_currentPassMultiplier);
    }

    // -------------------------------------------------------------------------
    // CHAIN & TIMER
    // -------------------------------------------------------------------------
    
    private void ResetChain(TeamType teamThatLost)
    {
        _currentPassMultiplier = 0;
        _passChainCount = 0;
        _goalActivated = false;
        _possessingTeam = TeamType.None;
        _previousPossessingTeam = TeamType.None;
        _previousHolder = null;

        StopAttackTimer();
        DeactivateAllGoals();

        Debug.Log($"[PossessionManager] Chain reset. {teamThatLost} lost possession.");
    }

    private void StartAttackTimer()
    {
        _attackTimerRunning = true;
        _attackTimerRemaining = possessionData.attackTimerDuration;
        _timerTickAccumulator = 0f;
    }

    private void RestartAttackTimer()
    {
        _attackTimerRemaining = possessionData.attackTimerDuration;
        _timerTickAccumulator = 0f;
    }

    private void StopAttackTimer()
    {
        _attackTimerRunning = false;
        _attackTimerRemaining = 0f;
        _timerTickAccumulator = 0f;
    }
    
    private void TickAttackTimer()
    {
        _attackTimerRemaining -= Time.deltaTime;
        _timerTickAccumulator += Time.deltaTime;

        // Fire tick event once per second for UI
        if (_timerTickAccumulator >= 1f)
        {
            _timerTickAccumulator -= 1f;
            onAttackTimerTick.Invoke(_attackTimerRemaining);
        }

        if (_attackTimerRemaining <= 0f)
        {
            _attackTimerRemaining = 0f;
            _attackTimerRunning = false;

            Debug.Log($"[PossessionManager] Attack timer expired! {_possessingTeam} chain reset.");

            onAttackTimerExpired.Invoke();

            TeamType teamThatRanOutOfTime = _possessingTeam;
            ResetChain(teamThatRanOutOfTime);
            onChainReset.Invoke(teamThatRanOutOfTime);
        }
    }
    
    private void ActivateOpposingGoal(TeamType attackingTeam)
    {
        _goalActivated = true;

        if (attackingTeam == TeamType.TeamA)
        {
            if (_teamBGoalInterface != null && !_teamBGoalInterface.IsGoalActive())
            {
                _teamBGoalInterface.ActivateGoal();
                onGoalActivated.Invoke(TeamType.TeamA);
            }
        }
        else if (attackingTeam == TeamType.TeamB)
        {
            if (_teamAGoalInterface != null && !_teamAGoalInterface.IsGoalActive())
            {
                _teamAGoalInterface.ActivateGoal();
                onGoalActivated.Invoke(TeamType.TeamB);
            }
        }
    }

    private void DeactivateAllGoals()
    {
        if (_teamAGoalInterface != null && _teamAGoalInterface.IsGoalActive())
        {
            _teamAGoalInterface.DeactivateGoal();
        }

        if (_teamBGoalInterface != null && _teamBGoalInterface.IsGoalActive())
        {
            _teamBGoalInterface.DeactivateGoal();
        }
    }
    
    private TeamType GetTeamFromTransform(Transform target)
    {
        if (!target) return TeamType.None;

        string targetTag = target.gameObject.tag;

        for (int i = 0; i < possessionData.teamATags.Length; i++)
        {
            if (targetTag == possessionData.teamATags[i])
            {
                return TeamType.TeamA;
            }
        }

        for (int i = 0; i < possessionData.teamBTags.Length; i++)
        {
            if (targetTag == possessionData.teamBTags[i])
            {
                return TeamType.TeamB;
            }
        }
        
        return TeamType.None;
    }
    
    private bool IsInterception(TeamType catchingTeam)
    {
        // No previous possession — can't be an interception, it's a fresh pickup
        if (_possessingTeam == TeamType.None) return false;

        // Same team caught it — not an interception
        if (catchingTeam == _possessingTeam) return false;

        // Different team caught it — interception
        return true;
    }
    
    private bool IsSuccessfulPass(TeamType catchingTeam, GameObject catcher)
    {
        // Must be same team
        if (catchingTeam != _possessingTeam) return false;

        // Disc must have been thrown (Passed state registered a thrower)
        if (!_previousHolder) return false;

        // The catcher must be different from the thrower
        // (prevents a player catching their own throw from counting as a pass)
        if (catcher == _previousHolder) return false;

        return true;
    }
    
    public void OnGoalScored()
    {
        ResetChain(_possessingTeam);
        onChainReset.Invoke(TeamType.None);
        Debug.Log("PossessionManager Goal scored — possession and chain fully reset");
    }
    
    public void ResetForCenterSpawn()
    {
        ResetChain(_possessingTeam);
        Debug.Log("PossessionManager Center spawn reset — all possession state cleared");
    }
    
    public string GetMultiplierDisplayString()
    {
        if (_currentPassMultiplier <= 0) return "x0";
        return "x" + _currentPassMultiplier;
    }
    
    private void CacheGoalInterfaces()
    {
        if (teamAGoal)
        {
            _teamAGoalInterface = teamAGoal as IGoalActivating;

            if (_teamAGoalInterface == null)
                Debug.LogError("[PossessionManager] teamAGoal does not implement IGoalActivating!");
        }

        if (teamBGoal)
        {
            _teamBGoalInterface = teamBGoal as IGoalActivating;

            if (_teamBGoalInterface == null)
                Debug.LogError("[PossessionManager] teamBGoal does not implement IGoalActivating");
        }
    }

    private void ValidateReferences()
    {
        if (!possessionData)
            Debug.LogError("[PossessionManager] PossessionData SO is not assigned!");
        if (!discControllerScript)
            Debug.LogError("[PossessionManager] DiscController is not assigned!");
        if (!teamAGoal)
            Debug.LogWarning("[PossessionManager] TeamA Goal not assigned — goal activation won't work.");
        if (!teamBGoal)
            Debug.LogWarning("[PossessionManager] TeamB Goal not assigned — goal activation won't work.");
    }

    #region Gizmos
    private void OnGUI()
    {
        // Only show in Play Mode
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 10, 280, 130));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Possessing Team: {_possessingTeam}");
        GUILayout.Label($"Pass Chain: #{_passChainCount}");
        GUILayout.Label($"Multiplier: x{_currentPassMultiplier}");
        GUILayout.Label($"Goal Active: {_goalActivated}");

        string timerDisplay = _attackTimerRunning ? _attackTimerRemaining.ToString("F1") + "s" : "Stopped";

        GUILayout.Label($"Attack Timer: {timerDisplay}");
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    #endregion
}
