public class Teams { }

// باید Tag کاراکترها دقیقا "TeamA" یا "TeamB" باشد تا با PossessionData.cs هماهنگ باشد
public enum TeamType
{
    None,  // دیسک هنوز متعلق به تیمی نیست و در حالت آزاد است
    TeamA,  // بازیکن انسانی + هم‌تیمی Bot
    TeamB  // دو Bot حریف
}
