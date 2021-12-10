using System;
using System.Collections;
using System.Collections.Generic;
using Colyseus;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyController : MonoBehaviour
{
    [SerializeField]
    private GameObject connectingCover = null;

    [SerializeField]
    private CreateUserMenu createUserMenu = null;

    public int minRequiredPlayers = 2;

    public string roomName = "ArenaRoom";

    [SerializeField]
    private RoomSelectionMenu selectRoomMenu = null;

    private void Awake()
    {
        createUserMenu.gameObject.SetActive(true);
        selectRoomMenu.gameObject.SetActive(false);
        connectingCover.SetActive(true);
    }

    private IEnumerator Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        while (!ArenaManager.IsReady)
        {
            yield return new WaitForEndOfFrame();
        }

        Dictionary<string, object> roomOptions = new Dictionary<string, object>
        {
            ["logic"] = "arenaRoom",
            ["minReqPlayers"] = minRequiredPlayers.ToString(),
        };

        ArenaManager.Instance.Initialize(roomName, roomOptions);
        ArenaManager.onRoomsReceived += OnRoomsReceived;
        connectingCover.SetActive(false);
    }

    private void OnDestroy()
    {
        ArenaManager.onRoomsReceived -= OnRoomsReceived;
    }

    public void CreateUser()
    {
        string desiredUserName = createUserMenu.UserName;
        PlayerPrefs.SetString("UserName", desiredUserName);
        ArenaManager.Instance.UserName = desiredUserName;

        createUserMenu.gameObject.SetActive(false);
        selectRoomMenu.gameObject.SetActive(true);
        selectRoomMenu.GetAvailableRooms();
    }

    public void CreateRoom()
    {
        connectingCover.SetActive(true);
        string desiredRoomName = selectRoomMenu.RoomCreationName;
        LoadArena(() => { ArenaManager.Instance.CreateNewRoom(desiredRoomName); });
    }

    public void JoinRoom(string id)
    {
        connectingCover.SetActive(true);
        LoadArena(() => { ArenaManager.Instance.JoinExistingRoom(id); });
    }

    public void OnConnectedToServer()
    {
        connectingCover.SetActive(false);
    }

    private void OnRoomsReceived(ColyseusRoomAvailable[] rooms)
    {
        selectRoomMenu.HandRooms(rooms);
    }

    private void LoadArena(Action onComplete)
    {
        StartCoroutine(LoadSceneAsync("ArenaScene", onComplete));
    }

    private IEnumerator LoadSceneAsync(string scene, Action onComplete)
    {
        Scene currScene = SceneManager.GetActiveScene();
        AsyncOperation op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        while (op.progress <= 0.9f)
        {
            yield return new WaitForEndOfFrame();
        }

        onComplete.Invoke();
        op.allowSceneActivation = true;
        SceneManager.UnloadSceneAsync(currScene);
    }
}