using UnityEngine;

// مقادیر این فایل دیتا توسط PossessionManager.cs خوانده می‌شوند تا pass-chain، multiplier و timer کنترل شود
[CreateAssetMenu(fileName = "PossessionData", menuName = "Match/Possession System", order = 0)]
public class PossessionData : ScriptableObject
{
    // مدت زمانی که تیم صاحب دیسک فرصت دارد گل بزند قبل از اینکه سیستم پاس ریست شود. هر چقدر بیشتر باشد تیم مهاجم وقت بیشتری برای حمله دارد
    [Tooltip("Seconds a team has to score after gaining possession on Disc")]
    public float attackTimerDuration = 30f;
    
    // سقف ضریب امتیاز که با هر پاس موفق یک واحد اضافه می‌شود تا به این عدد برسد. گل هایی که با ضریب بیشتر به ثمر رسیده باشند امتیاز بیشتری دارد
    [Tooltip("Maximum pass multiplier")]
    public int maxPassMultiplier = 5;

    // مقدار ضریب پاس بلافاصله بعد از اولین پاس موفق و لحظه‌ای است که دروازه حریف فعال می‌شود. همیشه باید 1 باشد
    [Tooltip("Multiplier value set after the first successful pass (Must be always 1)")]
    public int firstPassMultiplier = 1;

    [Header("Teams")]
    [Tooltip("Tags assigned to TeamA characters (Human player + Teammate bot)")]
    public string[] teamATags;
    [Tooltip("Tags assigned to TeamB characters (Enemy bots)")]
    public string[] teamBTags;
}
