using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [SerializeField]
    private Button exitButton = null;

    [SerializeField]
    private Button readyButton = null;

    public void UpdatePlayerReadiness(bool showButton)
    {
        readyButton.gameObject.SetActive(showButton);
    }

    public void AllowExit(bool allowed)
    {
        exitButton.gameObject.SetActive(allowed);
    }

    public void ButtonOnReady()
    {
        ArenaGameSceneManager.Instance.PlayerReadyToPlay();
    }

    public void ButtonOnExit()
    {
        ArenaGameSceneManager.Instance.OnQuitGame();
    }
}