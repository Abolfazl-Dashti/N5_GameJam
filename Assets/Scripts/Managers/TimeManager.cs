using UnityEngine;

public class TimeManager : MonoBehaviour
{
    private void Start()
    {
        GameManager.instance.GameStatusAction += ChangeGameTime;
    }

    private void ChangeGameTime(GameManager.GameStats gameStats)
    {
        switch (gameStats)
        {
            case GameManager.GameStats.InGame:
                Time.timeScale = 1;
                break;
            case GameManager.GameStats.Lose:
            case GameManager.GameStats.Win:
            case GameManager.GameStats.Pause:
                Time.timeScale = 0;
                break;
        }
    }
}
