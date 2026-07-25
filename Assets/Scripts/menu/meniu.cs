using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.SceneManagement;//برای لود کردن سین و یک لول

public class meniu : MonoBehaviour
{
    public void play()//تابع دکمه شروع بازی
    {
        print("playyyy");
        SceneManager.LoadScene("Prototype");
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

    public void playagane()//تابع دکمه شروع بازی
    {
        print("playyyy");
        //SceneManager.LoadScene("Snake");
        //کد لود کردن یک سین . باید اسم سین یا همون لول رو بنویسیم
      //  Time.timeScale = 2f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


}
