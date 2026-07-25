using UnityEngine;
using UnityEngine.Events;

// Manager for both Teams(TeamA, TeamB)
public class ScoreManager : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("Fires whenever any team's score changes. Passes TeamA score, TeamB score")]
    public UnityEvent<int, int> onScoreChanged;

    [Tooltip("Fires when a goal is scored. Passes scoring team and points awarded")]
    public UnityEvent<TeamType, int> onGoalScored;
    
    private int _teamAScore;
    private int _teamBScore;

    // Property
    public int TeamAScore => _teamAScore;
    public int TeamBScore => _teamBScore;

    public static ScoreManager instance;
    private void Awake()
    {
        instance = this;
    }
    
    public void AddScore(TeamType scoringTeam, int basePoints, int passMultiplier)
    {
        int pointsAwarded = basePoints * Mathf.Max(1, passMultiplier);

        if (scoringTeam == TeamType.TeamA)
        {
            _teamAScore += pointsAwarded;
        }
        else if (scoringTeam == TeamType.TeamB)
        {
            _teamBScore += pointsAwarded;
        }
        else
        {
            Debug.LogWarning("ScoreManager AddScore called with TeamType.None. score not added.");
            return;
        }

        Debug.Log($"[ScoreManager] {scoringTeam} scored {pointsAwarded} points " +
                  $"({basePoints} x {passMultiplier}). " +
                  $"Total — TeamA: {_teamAScore} | TeamB: {_teamBScore}");

        onGoalScored.Invoke(scoringTeam, pointsAwarded);
        onScoreChanged.Invoke(_teamAScore, _teamBScore);
    }
    
    // Resets both scores to zero. Call at match start
    public void ResetScores()
    {
        _teamAScore = 0;
        _teamBScore = 0;
        onScoreChanged.Invoke(_teamAScore, _teamBScore);
        Debug.Log("[ScoreManager] Scores reset to zero.");
    }
    
    
    /// Returns the winning team at match end
    /// Returns TeamType.None if scores are tied
    public TeamType GetWinner()
    {
        if (_teamAScore > _teamBScore) return TeamType.TeamA;
        if (_teamBScore > _teamAScore) return TeamType.TeamB;
        return TeamType.None;
    }

    /// <summary>
    /// Convenience display string for UI. e.g. "150 — 200"
    /// </summary>
    public string GetScoreDisplayString()
    {
        return _teamAScore.ToString() + " — " + _teamBScore.ToString();
    }
}
