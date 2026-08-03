using UnityEngine;

// تنظیمات امتیازدهی و بازخورد بصری دروازه ها که توسط GoalController.cs و MatchManager.cs استفاده می‌شود
[CreateAssetMenu(fileName = "GoalData", menuName = "Match/Goal Data", order = 1)]
public class GoalData : ScriptableObject
{
    [Header("Score Settings")]
    // امتیاز پایه هر گل پیش از اعمال ضریب پاس. هرچه بیشتر باشد امتیاز هر گل نسبت به ضریب پاسی که داشته بیشتر خواهد بود
    [Tooltip("Base points for a goal before pass multiplier is applied")]
    public int baseGoalPoints = 100;

    // مدت توقف بازی بعد از به ثمر رسیدن یک گل(افکت جشن گرفتن توی این مدت زمان میتونه اجرا بشه)
    [Tooltip("Seconds to pause the match after a goal before resetting")]
    public float postGoalPauseDuration;

    [Header("Visual Feedbacks")] 
    [Tooltip("Color of the goal trigger when inActive")]
    public Color inactiveColor;
    
    [Tooltip("Color of the goal trigger when Active & ready to receive goal from teams")]
    public Color activeColor;

    [Tooltip("Color flash when a goal is scored")]
    public Color scoredColor;
}
