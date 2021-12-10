using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
using LucidSightTools;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ArenaGameSceneManager : MonoBehaviour
{
    private string _countDownString = "";

    private bool _showCountdown;

    public PlayerController prefab;

    public NoticeController noticeController;
    public Scoreboard scoreboard;

    public GameUIController uiController;
    private string userReadyState = "";

    public static ArenaGameSceneManager Instance { get; private set; }

    public enum eGameState
    {
        NONE,
        WAITING,
        WAITINGFOROTHERS,
        SENDTARGETS,
        BEGINROUND,
        SIMULATEROUND,
        ENDROUND
    }

    private eGameState currentGameState;
    private eGameState lastGameState;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private IEnumerator Start()
    {
        while (ArenaManager.Instance.IsInRoom == false)
        {
            yield return 0;
        }
    }

    private void OnEnable()
    {
        ArenaRoomController.onAddNetworkEntity += OnNetworkAdd;
        ArenaRoomController.onRemoveNetworkEntity += OnNetworkRemove;
        ArenaRoomController.onScoreUpdate += OnScoreUpdate;

        ArenaRoomController.onRoomStateChanged += OnRoomStateChanged;
        ArenaRoomController.onBeginRoundCountDown += OnBeginRoundCountDown;
        ArenaRoomController.onBeginRound += OnBeginRound;
        ArenaRoomController.onRoundEnd += OnRoundEnd;

        ArenaRoomController.OnCurrentUserStateChanged += OnUserStateChanged;

        scoreboard.ResetScores();
        noticeController.SetMessage(GetPlayerReadyState());
        uiController.AllowExit(true);
    }

    private void OnDisable()
    {
        ArenaRoomController.onAddNetworkEntity -= OnNetworkAdd;
        ArenaRoomController.onRemoveNetworkEntity -= OnNetworkRemove;
        ArenaRoomController.onScoreUpdate -= OnScoreUpdate;

        ArenaRoomController.onRoomStateChanged -= OnRoomStateChanged;
        ArenaRoomController.onBeginRoundCountDown -= OnBeginRoundCountDown;
        ArenaRoomController.onBeginRound -= OnBeginRound;
        ArenaRoomController.onRoundEnd -= OnRoundEnd;

        ArenaRoomController.OnCurrentUserStateChanged -= OnUserStateChanged;
    }

    private void OnBeginRoundCountDown()
    {
        _showCountdown = true;
        scoreboard.ResetScores();
        uiController.AllowExit(false);
        uiController.UpdatePlayerReadiness(false);
    }

    private void OnBeginRound()
    {
        StartCoroutine(DelayedRoundBegin());
    }

    private IEnumerator DelayedRoundBegin()
    {
        yield return new WaitForSeconds(1);
        _countDownString = "";
        _showCountdown = false;
        uiController.UpdatePlayerReadiness(false);
      // scoreboardController.BeginGame();
    }

    private void OnRoundEnd(Winner winner)
    {
        PlayerController player = GetPlayerView(ArenaManager.Instance.CurrentNetworkedEntity.id);
        if (player != null)
        {
            player.UpdateReadyState(false);
        }
        string winnerMessage = GetWinningMessage(winner);
        noticeController.SetMessage(winnerMessage);
        StartCoroutine(DelayedRoundEnd());
    }

    private IEnumerator DelayedRoundEnd()
    {
        yield return new WaitForSeconds(5);
        if ((currentGameState == eGameState.WAITING || currentGameState == eGameState.WAITINGFOROTHERS) && lastGameState == eGameState.ENDROUND)
        {
            noticeController.SetMessage(GetPlayerReadyState());
            uiController.UpdatePlayerReadiness(AwaitingPlayerReady());
            uiController.AllowExit(true);
        }
    }

    private void Update()
    {
        if (AwaitingPlayerReady() && Input.GetKeyDown(KeyCode.Return))
        {
            PlayerReadyToPlay();
        }
    }

    private string GetWinningMessage(Winner winner)
    {
        string winnerMessage = "";

        if (winner.tie)
        {
            winnerMessage = $"TIE!\nThese players tied with a top score of {winner.score}:\n";
            for (int i = 0; i < winner.tied.Length; i++)
            {
                PlayerController p = GetPlayerView(winner.tied[i]);
                if (p != null)
                {
                    winnerMessage += $"{(p ? p.userName : winner.tied[i])}\n";
                }
            }
        }
        else
        {
            PlayerController p = GetPlayerView(winner.id);
            if (p != null)
            {
                winnerMessage = $"Round Over!\n{(p ? p.userName : winner.id)} wins!";
            }
        }

        return winnerMessage;
    }

    private eGameState TranslateGameState(string gameState)
    {
        switch (gameState)
        {
            case "Waiting":
                {
                    PlayerController player = GetPlayerView(ArenaManager.Instance.CurrentNetworkedEntity.id);
                    if (player != null)
                    {
                        return player.isReady ? eGameState.WAITINGFOROTHERS : eGameState.WAITING;
                    }

                    return eGameState.WAITING;
                }
            case "SendTargets":
                {
                    return eGameState.SENDTARGETS;
                }
            case "BeginRound":
                {
                    return eGameState.BEGINROUND;
                }
            case "SimulateRound":
                {
                    return eGameState.SIMULATEROUND;
                }
            case "EndRound":
                {
                    return eGameState.ENDROUND;
                }
            default:
                return eGameState.NONE;
        }
    }

    private void OnRoomStateChanged(MapSchema<string> attributes)
    {
        if (_showCountdown && attributes.ContainsKey("countDown"))
        {
            _countDownString = attributes["countDown"];
            noticeController.SetMessage(_countDownString);
        }

        if (attributes.ContainsKey("currentGameState"))
        {
            eGameState nextState = TranslateGameState(attributes["currentGameState"]);
            if (IsSafeStateTransition(currentGameState, nextState))
            {
                currentGameState = nextState;
            }
            else
            {
                LSLog.LogError($"CurrentGameState: Failed to transition from {currentGameState} to {nextState}");
            }
        }

        if (attributes.ContainsKey("lastGameState"))
        {
            eGameState nextState = TranslateGameState(attributes["lastGameState"]);
            if (IsSafeStateTransition(lastGameState, nextState))
            {
                lastGameState = nextState;
            }
            else
            {
                LSLog.LogError($"LastGameState: Failed to transition from {lastGameState} to {nextState}");
            }
        }
    }

    private bool IsSafeStateTransition(eGameState fromState, eGameState nextState)
    {
        if (fromState == nextState)
            return true;

        switch (fromState)
        {
            case eGameState.WAITING:
                {
                    return nextState == eGameState.WAITINGFOROTHERS || nextState == eGameState.BEGINROUND;
                }
            case eGameState.WAITINGFOROTHERS:
                {
                    return nextState == eGameState.WAITING || nextState == eGameState.BEGINROUND || nextState == eGameState.SIMULATEROUND;
                }
            case eGameState.BEGINROUND:
                {
                    return nextState == eGameState.SIMULATEROUND || nextState == eGameState.ENDROUND;
                }
            case eGameState.SIMULATEROUND:
                {
                    return nextState == eGameState.ENDROUND || nextState == eGameState.WAITING || nextState == eGameState.WAITINGFOROTHERS;
                }
            case eGameState.ENDROUND:
                {
                    return nextState == eGameState.WAITING || nextState == eGameState.WAITINGFOROTHERS;
                }
            default:
                {
                    return true;
                }
        }
    }

    private void OnUserStateChanged(MapSchema<string> attributeChanges)
    {
        if (attributeChanges.TryGetValue("readyState", out string readyState))
        {
            userReadyState = readyState;

            if (AwaitingAnyPlayerReady())
            {
                noticeController.SetMessage(GetPlayerReadyState());
                uiController.UpdatePlayerReadiness(AwaitingPlayerReady());
            }
        }
    }

    private string GetPlayerReadyState()
    {
        string readyState = "Waiting for you to ready up!";

        PlayerController player = GetPlayerView(ArenaManager.Instance.CurrentNetworkedEntity.id);
        if (player != null)
        {
            readyState = player.isReady ? "Waiting on other players..." : "Waiting for you to ready up!";
        }

        return readyState;
    }

    public bool AwaitingPlayerReady()
    {
        if (currentGameState == eGameState.WAITING)
        {
            return true;
        }

        return false;
    }

    private bool AwaitingAnyPlayerReady()
    {
        return currentGameState == eGameState.WAITING || currentGameState == eGameState.WAITINGFOROTHERS;
    }

    private void OnNetworkAdd(ArenaNetworkedEntity entity)
    {
        if (ArenaManager.Instance.HasEntityView(entity.id))
        {
            LSLog.LogImportant("View found! For " + entity.id);
            scoreboard.CreateScoreEntry(entity); 
        }
        else
        {
            LSLog.LogImportant("No View found for " + entity.id);
            CreateView(entity);
        }
    }

    private void OnNetworkRemove(ArenaNetworkedEntity entity, ColyseusNetworkedEntityView view)
    {
        RemoveView(view);
        scoreboard.RemoveView(entity);
    }

    private void CreateView(ArenaNetworkedEntity entity)
    {
        StartCoroutine(WaitingEntityAdd(entity));
    }

    IEnumerator WaitingEntityAdd(ArenaNetworkedEntity entity)
    {
        PlayerController newView = Instantiate(prefab);
        ArenaManager.Instance.RegisterNetworkedEntityView(entity, newView);
        newView.gameObject.SetActive(true);
        float seconds = 0;
        float delayAmt = 1.0f;
        while (string.IsNullOrEmpty(newView.userName))
        {
            yield return new WaitForSeconds(delayAmt);
            seconds += delayAmt;
            if (seconds >= 30) 
            {
                newView.userName = "GUEST";
            }
        }

        scoreboard.CreateScoreEntry(entity);
    }

    private void RemoveView(ColyseusNetworkedEntityView view)
    {
        view.SendMessage("OnEntityRemoved", SendMessageOptions.DontRequireReceiver);
    }


    private void OnScoreUpdate(ArenaScoreUpdateMessage update)
    {
        PlayerController pc = GetPlayerView(update.entityID);
        if (pc != null)
        {
            pc.ShowTargetHit();
        }
        scoreboard.UpdateScore(update);
    }

    public void RegisterTargetKill(string entityID, string targetID)
    {
        ArenaManager.CustomServerMethod("scoreTarget", new object[] { entityID, targetID });
    }
    public void RegisterPlayerHit(string entityID, string targetID)
    {
        ArenaManager.CustomServerMethod("hitPlayer", new object[] { entityID, targetID });
    }

    public void PlayerReadyToPlay()
    {
        uiController.UpdatePlayerReadiness(false);
        ArenaManager.NetSend("setAttribute",
            new ArenaAttributeUpdateMessage
            {
                userId = ArenaManager.Instance.CurrentUser.id,
                attributesToSet = new Dictionary<string, string> { { "readyState", "ready" } }
            });

        PlayerController player = GetPlayerView(ArenaManager.Instance.CurrentNetworkedEntity.id);
        if (player != null)
        {
            player.SetPause(false);
            player.UpdateReadyState(true);
        }
    }

    public PlayerController GetPlayerView(string entityID)
    {
        if (ArenaManager.Instance.HasEntityView(entityID))
        {
            return ArenaManager.Instance.GetEntityView(entityID) as PlayerController;
        }

        return null;
    }

    public void OnQuitGame()
    {
        if (ArenaManager.Instance.IsInRoom)
        {
            PlayerController pc = GetPlayerView(ArenaManager.Instance.CurrentNetworkedEntity.id);
            if (pc != null)
            {
                pc.enabled = false; 
            }

            ArenaManager.Instance.LeaveAllRooms(() => { SceneManager.LoadScene("Lobby"); });
        }
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        ArenaManager.Instance.OnEditorQuit();
    }
#endif
}