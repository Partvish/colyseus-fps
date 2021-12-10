using TMPro;
using UnityEngine;

public class ScoreboardEntry : MonoBehaviour
{
    public int currentScore;

    [HideInInspector]
    public ArenaNetworkedEntity entityRef;

    [SerializeField]
    private TextMeshProUGUI playerName = null;

    [SerializeField]
    private TextMeshProUGUI playerScore = null;

    public void Init(ArenaNetworkedEntity entity)
    {
        entityRef = entity;
        PlayerController playerRef = ArenaGameSceneManager.Instance.GetPlayerView(entity.id);
        if (playerRef != null)
        {
            playerName.text = playerRef.userName;
        }
        else
        {
            playerName.text = entityRef.id;
        }

        playerScore.text = "0";
    }

    public void UpdateScore(int score)
    {
        playerScore.text = score.ToString("N0");
        currentScore = score;
    }
}