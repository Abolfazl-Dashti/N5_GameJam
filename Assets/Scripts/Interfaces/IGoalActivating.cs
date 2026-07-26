using UnityEngine;

// Called in 'PossessionManager' Script
public interface IGoalActivating
{
    // Activate the goal
    void ActivateGoal();

    // Deactivate the goal
    void DeactivateGoal();

    // Returns true if this goal is currently active
    bool IsGoalActive();

    // Which team defends this goal?
    TeamType GetDefendingTeam();
}
