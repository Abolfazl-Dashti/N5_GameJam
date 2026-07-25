using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        GameStatusAction += GameStatus;
    }

    public enum GameStats
    {
        InGame,
        Win,
        Lose,
        Pause,
    }

    public UnityAction<GameStats> GameStatusAction;
    private GameStats _currentGameStatus;

    private void GameStatus(GameStats gameStatus)
    {
        _currentGameStatus = gameStatus;
    }
}
