using UnityEngine;

public class HUD : MonoBehaviour
{
    private void Start()
    {
        GameManager.instance.GameStatusAction += ChangeMenu;
    }

    // We can define what should be done for menus in different situations
    private void ChangeMenu(GameManager.GameStats gameStats)
    {
        switch (gameStats)
        {
            case GameManager.GameStats.InGame:
                break;
            case GameManager.GameStats.Win:
                break;
            case GameManager.GameStats.Lose:
                break;
            case GameManager.GameStats.Pause:
                break;
        }
    }

    // Attach to pause btn
    public void PauseGame()
    {
        GameManager.instance.GameStatusAction.Invoke(GameManager.GameStats.Pause);
    }

    // Attach to Resume btn
    public void ResumeGame()
    {
        GameManager.instance.GameStatusAction.Invoke(GameManager.GameStats.InGame);
    }
}
