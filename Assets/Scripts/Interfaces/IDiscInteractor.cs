using UnityEngine;

/// Implement by 'PlayerDiscHandler' and 'BotAIController'
public interface IDiscInteractor
{
    // موقعیت پلیری که دیسک را دارد برمیگرداند
    Transform GetTransform();

    // این متد زمانی صدا زده میشود، وقتی دیسک تحت مالکیت یک بازیکن درمیاد
    void OnDiscReceived(DiscController disc);

    // وقتی دیسک از مالکیت یک بازیکن خارج میشه، این متد صدا زده میشود
    void OnDiscLost();

    // اگر بازیکنی در حال حاضر دیسک را داشته باشد مقدار true برگردانده میشود
    bool IsHoldingDisc();
}
