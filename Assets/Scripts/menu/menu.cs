using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class menu : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.mKey.wasPressedThisFrame)
        {
            home();
        }
    }

    public void play()//تابع دکمه شروع بازی
    {
        print("playyyy");
        SceneManager.LoadScene("Prototype-bakup 2");
        //کد لود کردن یک سین . باید اسم سین یا همون لول رو بنویسیم
    }
    public void exit()//تابع خروج از بازی
    {
        print("exittttttttt");
        Application.Quit();
        //دستور خارج شدن از برنامه و بسته شدن برنامه
    }
    public void home()//تابع دکمه هوم
    {
        SceneManager.LoadScene("menu");
        //کد لود کردن یک سین . باید اسم سین یا همون لول رو بنویسیم
    }
}
