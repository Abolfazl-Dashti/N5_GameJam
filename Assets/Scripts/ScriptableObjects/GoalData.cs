using UnityEngine;

[CreateAssetMenu(fileName = "GoalData", menuName = "Match/Goal Data", order = 1)]
public class GoalData : ScriptableObject
{
    [Header("Scoring")]
    [Tooltip("Base points awarded for a goal before multiplier is applied")]
    public int baseGoalPoints = 100;

    [Tooltip("Seconds to pause the match after a goal before resetting")]
    public float postGoalPauseDuration = 4f;

    [Header("Visual Feedback")]
    [Tooltip("Color of the goal trigger when inActive")]
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);

    [Tooltip("Color of the goal trigger when Active & ready to receive goal from teams")]
    public Color activeColor;

    [Tooltip("Color flash when a goal is scored.")]
    public Color scoredColor;
}
