using System;
using System.Collections.Generic;
using Colyseus;
using UnityEngine;
using LucidSightTools;

public class ArenaManager : ColyseusManager<ArenaManager>
{
    public delegate void OnRoomsReceived(ColyseusRoomAvailable[] rooms);

    public static OnRoomsReceived onRoomsReceived;
    private ArenaNetworkedEntityFactory _networkedEntityFactory;

    [SerializeField]
    private ArenaRoomController _roomController;

    public bool autoJoinRoom = true;

    public ArenaNetworkedEntity CurrentNetworkedEntity;

    private bool isInitialized;

    public bool IsInRoom
    {
        get { return _roomController.Room != null; }
    }

    public double GetServerTime
    {
        get { return _roomController.GetServerTime; }
    }

    public double GetServerTimeSeconds
    {
        get { return _roomController.GetServerTimeSeconds; }
    }

    public double GetRoundtripTime
    {
        get { return _roomController.GetRoundtripTime; }
    }

    public ArenaNetworkedUser CurrentUser
    {
        get { return _roomController.CurrentNetworkedUser; }
    }

    public static bool IsReady
    {
        get
        {
            return Instance != null; 
        }
    }

    private string userName;

    public string UserName
    {
        get { return userName; }
        set { userName = value; }
    }


    protected override void Start()
    {

        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        InitializeClient();
    }

    public void Initialize(string roomName, Dictionary<string, object> roomOptions)
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;

        _roomController = new ArenaRoomController { roomName = roomName };
        _roomController.SetRoomOptions(roomOptions);
        _roomController.SetDependencies(_colyseusSettings);

        _networkedEntityFactory = new ArenaNetworkedEntityFactory(_roomController.CreationCallbacks,
            _roomController.Entities, _roomController.EntityViews);
    }


    public override void InitializeClient()
    {
        base.InitializeClient();

        _roomController.SetClient(client);
        if (autoJoinRoom)
        {
            _roomController.JoinOrCreateRoom();
        }
    }


    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        _roomController.IncrementServerTime();
    }

    public async void GetAvailableRooms()
    {
        ColyseusRoomAvailable[] rooms = await client.GetAvailableRooms(_roomController.roomName);

        onRoomsReceived?.Invoke(rooms);
    }

    public async void JoinExistingRoom(string roomID)
    {
        await _roomController.JoinRoomId(roomID);
    }

    public async void CreateNewRoom(string roomID)
    {
        await _roomController.CreateSpecificRoom(client, _roomController.roomName, roomID);
    }

    public async void LeaveAllRooms(Action onLeave)
    {
        await _roomController.LeaveAllRooms(true, onLeave);
    }

    public bool HasEntityView(string entityId)
    {
        return _roomController.HasEntityView(entityId);
    }


    public ArenaNetworkedEntityView GetEntityView(string entityId)
    {
        return _roomController.GetEntityView(entityId);
    }


    private void CleanUpOnAppQuit()
    {
        if (client == null)
        {
            return;
        }

        _roomController.CleanUp();
    }


    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        _roomController.LeaveAllRooms(true);

        CleanUpOnAppQuit();
    }

#if UNITY_EDITOR
    public void OnEditorQuit()
    {
        OnApplicationQuit();
    }
#endif

    #region Remote Function Call


    public static void RFC(ColyseusNetworkedEntityView entity, string function, object[] param,
        ArenaRFCTargets target = ArenaRFCTargets.ALL)
    {
        RFC(entity.Id, function, param, target);
    }


    public static void RFC(string entityId, string function, object[] param,
        ArenaRFCTargets target = ArenaRFCTargets.ALL)
    {
        NetSend("remoteFunctionCall",
            new ArenaRFCMessage { entityId = entityId, function = function, param = param, target = target });
    }

    public static void CustomServerMethod(string methodName, object[] param)
    {
        NetSend("customMethod", new ArenaCustomMethodMessage { method = methodName, param = param });
    }


    public static void NetSend(string action, object message = null)
    {
        if (Instance._roomController.Room == null)
        {
            LSLog.LogError($"Error: Not in room for action {action} msg {message}");
            return;
        }

        _ = message == null
            ? Instance._roomController.Room.Send(action)
            : Instance._roomController.Room.Send(action, message);
    }


    public static void NetSend(byte actionByte, object message = null)
    {
        if (Instance._roomController.Room == null)
        {
            LSLog.LogError(
                $"Error: Not in room for action bytes msg {(message != null ? message.ToString() : "No Message")}");
            return;
        }

        _ = message == null
            ? Instance._roomController.Room.Send(actionByte)
            : Instance._roomController.Room.Send(actionByte, message);
    }

    #endregion Remote Function Call

    #region Networked Entity Creation

    public void InstantiateNetworkedEntity(string prefab, Dictionary<string, object> attributes = null)
    {
        InstantiateNetworkedEntity(prefab, Vector3.zero, Quaternion.identity, attributes);
    }


    public static void InstantiateNetworkedEntity(string prefab, Vector3 position,
        Dictionary<string, object> attributes = null)
    {
        Instance._networkedEntityFactory.InstantiateNetworkedEntity(Instance._roomController.Room, prefab, position,
            Quaternion.identity, attributes);
    }

    public static void InstantiateNetworkedEntity(string prefab, Vector3 position, Quaternion rotation,
        Dictionary<string, object> attributes = null)
    {
        Instance._networkedEntityFactory.InstantiateNetworkedEntity(Instance._roomController.Room, prefab, position,
            rotation, attributes);
    }

    public static void CreateNetworkedEntityWithTransform(Vector3 position, Quaternion rotation,
        Dictionary<string, object> attributes = null, ColyseusNetworkedEntityView viewToAssign = null,
        Action<ArenaNetworkedEntity> callback = null)
    {
        Instance._networkedEntityFactory.CreateNetworkedEntityWithTransform(Instance._roomController.Room, position,
            rotation, attributes, viewToAssign, callback);
    }

    public static void CreateNetworkedEntity(string prefab, Dictionary<string, object> attributes = null,
        ColyseusNetworkedEntityView viewToAssign = null, Action<ArenaNetworkedEntity> callback = null)
    {
        Instance._networkedEntityFactory.CreateNetworkedEntity(Instance._roomController.Room, prefab, attributes,
            viewToAssign, callback);
    }

    public static void CreateNetworkedEntity(Dictionary<string, object> attributes = null,
        ColyseusNetworkedEntityView viewToAssign = null, Action<ArenaNetworkedEntity> callback = null)
    {
        Instance._networkedEntityFactory.CreateNetworkedEntity(Instance._roomController.Room, attributes, viewToAssign,
            callback);
    }

    public void RegisterNetworkedEntityView(ArenaNetworkedEntity model, ColyseusNetworkedEntityView view)
    {
        _networkedEntityFactory.RegisterNetworkedEntityView(model, view);
    }


    private static async void CreateFromPrefab(ArenaNetworkedEntity entity)
    {
        await Instance._networkedEntityFactory.CreateFromPrefab(entity);
    }

    #endregion Networked Entity Creation
}