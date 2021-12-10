using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
using GameDevWare.Serialization;
using LucidSightTools;
using NativeWebSocket;
using UnityEngine;

[Serializable]
public class ArenaRoomController
{
    public delegate void OnBeginRound();

    public delegate void OnBeginRoundCountDown();

    public delegate void OnNetworkEntityAdd(ArenaNetworkedEntity entity);

    public delegate void OnNetworkEntityRemoved(ArenaNetworkedEntity entity, ColyseusNetworkedEntityView view);

    public delegate void OnRoomStateChanged(MapSchema<string> attributes);

    public delegate void OnRoundEnd(Winner winner);

    public delegate void OnScoreUpdate(ArenaScoreUpdateMessage update);

    public delegate void OnUserStateChanged(MapSchema<string> changes);

    public static OnNetworkEntityAdd onAddNetworkEntity;

    public static OnNetworkEntityRemoved onRemoveNetworkEntity;


    [SerializeField]
    private static ArenaNetworkedUser _currentNetworkedUser;

    private ColyseusClient _client;

    private ColyseusSettings _colyseusSettings;


    private Dictionary<string, Action<ArenaNetworkedEntity>> _creationCallbacks =
        new Dictionary<string, Action<ArenaNetworkedEntity>>();

    private IndexedDictionary<string, ArenaNetworkedEntity> _entities =
        new IndexedDictionary<string, ArenaNetworkedEntity>();

    private IndexedDictionary<string, ArenaNetworkedEntityView> _entityViews =
        new IndexedDictionary<string, ArenaNetworkedEntityView>();

    private ArenaNetworkedEntityFactory _factory;

    private double _lastPing;

    private double _lastPong;

    private string _lastRoomId;

    private Thread _pingThread;

    private ColyseusRoom<ArenaRoomState> _room;

    private double _serverTime = -1;

    private IndexedDictionary<string, ArenaNetworkedUser> _users =
        new IndexedDictionary<string, ArenaNetworkedUser>();

    private bool _waitForPong;

    public string roomName = "NO_ROOM_NAME_PROVIDED";

    private Dictionary<string, object> roomOptionsDictionary = new Dictionary<string, object>();

    public List<IColyseusRoom> rooms = new List<IColyseusRoom>();

    public double GetServerTime
    {
        get { return _serverTime; }
    }

    public double GetServerTimeSeconds
    {
        get { return _serverTime / 1000; }
    }

    public double GetRoundtripTime
    {
        get { return _lastPong - _lastPing; }
    }

    public ColyseusRoom<ArenaRoomState> Room
    {
        get { return _room; }
    }

    public string LastRoomID
    {
        get { return _lastRoomId; }
    }

    public IndexedDictionary<string, ArenaNetworkedEntity> Entities
    {
        get { return _entities; }
    }

    public IndexedDictionary<string, ArenaNetworkedEntityView> EntityViews
    {
        get { return _entityViews; }
    }

    public Dictionary<string, Action<ArenaNetworkedEntity>> CreationCallbacks
    {
        get { return _creationCallbacks; }
    }

    public ArenaNetworkedUser CurrentNetworkedUser
    {
        get { return _currentNetworkedUser; }
    }

    public bool HasEntityView(string entityId)
    {
        return EntityViews.ContainsKey(entityId);
    }

    public ArenaNetworkedEntityView GetEntityView(string entityId)
    {
        if (EntityViews.ContainsKey(entityId))
        {
            return EntityViews[entityId];
        }

        return null;
    }

    public static event OnRoomStateChanged onRoomStateChanged;
    public static event OnScoreUpdate onScoreUpdate;
    public static event OnBeginRoundCountDown onBeginRoundCountDown;
    public static event OnBeginRound onBeginRound;
    public static event OnRoundEnd onRoundEnd;
    public static event OnUserStateChanged OnCurrentUserStateChanged;

    public void SetDependencies(ColyseusSettings settings)
    {
        _colyseusSettings = settings;

        ColyseusClient.onAddRoom += AddRoom;
    }

    public void SetRoomOptions(Dictionary<string, object> options)
    {
        roomOptionsDictionary = options;
    }

    public void SetNetworkedEntityFactory(ArenaNetworkedEntityFactory factory)
    {
        _factory = factory;
    }

    public void SetClient(ColyseusClient client)
    {
        _client = client;
    }

    public void AddRoom(IColyseusRoom roomToAdd)
    {
        roomToAdd.OnLeave += code => { rooms.Remove(roomToAdd); };
        rooms.Add(roomToAdd);
    }

    public async Task CreateSpecificRoom(ColyseusClient client, string roomName, string roomId)
    {
        LSLog.LogImportant($"Creating Room {roomId}");

        try
        {
            Dictionary<string, object> options = new Dictionary<string, object> { ["roomId"] = roomId };
            foreach (KeyValuePair<string, object> option in roomOptionsDictionary)
            {
                options.Add(option.Key, option.Value);
            }

            _room = await client.Create<ArenaRoomState>(roomName, options);
        }
        catch (Exception ex)
        {
            LSLog.LogError($"Failed to create room {roomId} : {ex.Message}");
            return;
        }

        LSLog.LogImportant($"Created Room: {_room.Id}");
        _lastRoomId = roomId;
        RegisterRoomHandlers();
    }

    public async void JoinOrCreateRoom()
    {
        try
        {
            LSLog.LogImportant($"Join Or Create Room - Name = {roomName}.... ");
            Dictionary<string, object> options = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> option in roomOptionsDictionary)
            {
                options.Add(option.Key, option.Value);
            }

            _room = await _client.JoinOrCreate<ArenaRoomState>(roomName, options);

            LSLog.LogImportant($"Joined / Created Room: {_room.Id}");
            _lastRoomId = _room.Id;
            RegisterRoomHandlers();
        }
        catch (Exception e)
        {
            LSLog.LogError($"Room Controller Error - {e.Message + e.StackTrace}");
        }
    }

    public async Task LeaveAllRooms(bool consented, Action onLeave = null)
    {
        if (_room != null && rooms.Contains(_room) == false)
        {
            await _room.Leave(consented);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            await rooms[i].Leave(consented);
        }

        _entities.Clear();
        _entityViews.Clear();
        _users.Clear();

        ClearRoomHandlers();

        onLeave?.Invoke();
    }

    public virtual void RegisterRoomHandlers()
    {
        LSLog.LogImportant($"sessionId: {_room.SessionId}");

        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        _pingThread = new Thread(RunPingThread);
        _pingThread.Start(_room);

        _room.OnLeave += OnLeaveRoom;

        _room.OnStateChange += OnStateChangeHandler;

        _room.OnMessage<ArenaNetworkedUser>("onJoin", currentNetworkedUser =>
        {
            Debug.Log($"Received 'ArenaNetworkedUser' after join/creation call {currentNetworkedUser.id}!");
            Debug.Log(Json.SerializeToString(currentNetworkedUser));

            _currentNetworkedUser = currentNetworkedUser;
        });

        _room.OnMessage<ArenaRFCMessage>("onRFC", _rfc =>
        {
            if (_entityViews.Keys.Contains(_rfc.entityId))
            {
                _entityViews[_rfc.entityId].RemoteFunctionCallHandler(_rfc);
            }
        });

        _room.OnMessage<ArenaPingMessage>(0, message =>
        {
            _lastPong = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _serverTime = message.serverTime;
            _waitForPong = false;
        });


        _room.OnMessage<ArenaScoreUpdateMessage>("onScoreUpdate",
            scoreUpdate => { onScoreUpdate?.Invoke(scoreUpdate); });

        _room.OnMessage<ArenaMessage>("beginRoundCountDown", msg => { onBeginRoundCountDown?.Invoke(); });

        _room.OnMessage<ArenaMessage>("beginRound", msg => { onBeginRound?.Invoke(); });

        _room.OnMessage<ArenaRoundEndMessage>("onRoundEnd", winner => { onRoundEnd?.Invoke(winner.winner); });


        Debug.Log($"Adding OnAdd/OnRemove callbacks for all {_room.State.networkedEntities.Count} entities! ***");
        _room.State.networkedEntities.OnAdd += OnEntityAdd;
        _room.State.networkedEntities.OnRemove += OnEntityRemoved;

        _room.State.networkedUsers.OnAdd += OnUserAdd;
        _room.State.networkedUsers.OnRemove += OnUserRemove;

        _room.State.TriggerAll();


        _room.colyseusConnection.OnError += Room_OnError;
        _room.colyseusConnection.OnClose += Room_OnClose;
    }

    private void OnLeaveRoom(int code)
    {
        WebSocketCloseCode closeCode = WebSocketHelpers.ParseCloseCodeEnum(code);
        LSLog.Log(string.Format("ROOM: ON LEAVE =- Reason: {0} ({1})", closeCode, code));
        _pingThread.Abort();
        _pingThread = null;
        _room = null;

        if (closeCode != WebSocketCloseCode.Normal && !string.IsNullOrEmpty(_lastRoomId))
        {
            JoinRoomId(_lastRoomId);
        }
    }

    private void ClearRoomHandlers()
    {
        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        if (_room == null)
        {
            return;
        }

        _room.State.networkedEntities.OnAdd -= OnEntityAdd;
        _room.State.networkedEntities.OnRemove -= OnEntityRemoved;
        _room.State.networkedUsers.OnAdd -= OnUserAdd;
        _room.State.networkedUsers.OnRemove -= OnUserRemove;

        _room.colyseusConnection.OnError -= Room_OnError;
        _room.colyseusConnection.OnClose -= Room_OnClose;

        _room.OnStateChange -= OnStateChangeHandler;

        _room.OnLeave -= OnLeaveRoom;

        _room = null;
        _currentNetworkedUser = null;
    }

    public async Task<ColyseusRoomAvailable[]> GetRoomListAsync()
    {
        ColyseusRoomAvailable[] allRooms = await _client.GetAvailableRooms(roomName);

        return allRooms;
    }

    public async Task JoinRoomId(string roomId)
    {
        LSLog.Log($"Joining Room ID {roomId}....");
        ClearRoomHandlers();

        try
        {
            while (_room == null || !_room.colyseusConnection.IsOpen)
            {
                _room = await _client.JoinById<ArenaRoomState>(roomId);

                if (_room == null || !_room.colyseusConnection.IsOpen)
                {
                    LSLog.LogImportant($"Failed to Connect to {roomId}.. Retrying in 5 Seconds...");
                    await Task.Delay(5000);
                }
            }
            LSLog.LogImportant($"Connected to {roomId}..");
            _lastRoomId = roomId;
            RegisterRoomHandlers();
        }
        catch (Exception ex)
        {
            LSLog.LogError(ex.Message);
            LSLog.LogError("Failed to join room");
        }
    }

    private async void OnEntityAdd(string key, ArenaNetworkedEntity entity)
    {
        LSLog.LogImportant(
            $"Entity [{entity.__refId} | {entity.id}] add: x => {entity.xPos}, y => {entity.yPos}, z => {entity.zPos}");

        _entities.Add(entity.id, entity);

        if (!string.IsNullOrEmpty(entity.creationId) && _creationCallbacks.ContainsKey(entity.creationId))
        {
            _creationCallbacks[entity.creationId].Invoke(entity);
            _creationCallbacks.Remove(entity.creationId);
        }

        onAddNetworkEntity?.Invoke(entity);

        if (_entityViews.ContainsKey(entity.id) == false && !string.IsNullOrEmpty(entity.attributes["prefab"]))
        {
            await _factory.CreateFromPrefab(entity);
        }
    }

    private void OnEntityRemoved(string key, ArenaNetworkedEntity entity)
    {
        if (_entities.ContainsKey(entity.id))
        {
            _entities.Remove(entity.id);
        }

        ColyseusNetworkedEntityView view = null;

        if (_entityViews.ContainsKey(entity.id))
        {
            view = _entityViews[entity.id];
            _entityViews.Remove(entity.id);
        }

        onRemoveNetworkEntity?.Invoke(entity, view);
    }

    private void OnUserAdd(string key, ArenaNetworkedUser user)
    {
        LSLog.LogImportant($"user [{user.__refId} | {user.id} | key {key}] Joined");

        _users.Add(key, user);

        user.OnChange += changes =>
        {
            user.updateHash = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            if (ArenaManager.Instance.CurrentUser != null &&
                string.Equals(ArenaManager.Instance.CurrentUser.sessionId, user.sessionId))
            {
                OnCurrentUserStateChanged?.Invoke(user.attributes);
            }
        };
    }

    private void OnUserRemove(string key, ArenaNetworkedUser user)
    {
        LSLog.LogImportant($"user [{user.__refId} | {user.id} | key {key}] Left");

        _users.Remove(key);
    }

    private static void Room_OnClose(int closeCode)
    {
        LSLog.LogError("Room_OnClose: " + closeCode);
    }

    private static void Room_OnError(string errorMsg)
    {
        LSLog.LogError("Room_OnError: " + errorMsg);
    }

    private static void OnStateChangeHandler(ArenaRoomState state, bool isFirstState)
    {
        onRoomStateChanged?.Invoke(state.attributes);
    }

    private void RunPingThread(object roomToPing)
    {
        ColyseusRoom<ArenaRoomState> currentRoom = (ColyseusRoom<ArenaRoomState>)roomToPing;

        const float pingInterval = 0.5f; // seconds
        const float pingTimeout = 15f; //seconds

        int timeoutMilliseconds = Mathf.FloorToInt(pingTimeout * 1000);
        int intervalMilliseconds = Mathf.FloorToInt(pingInterval * 1000);

        DateTime pingStart;
        while (currentRoom != null)
        {
            _waitForPong = true;
            pingStart = DateTime.Now;
            _lastPing = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _ = currentRoom.Send("ping");

            while (currentRoom != null && _waitForPong &&
                   DateTime.Now.Subtract(pingStart).TotalSeconds < timeoutMilliseconds)
            {
                Thread.Sleep(200);
            }

            if (_waitForPong)
            {
                LSLog.LogError("Ping Timed out");
            }

            Thread.Sleep(intervalMilliseconds);
        }
    }

    public void IncrementServerTime()
    {
        _serverTime += Time.fixedDeltaTime * 1000;
    }

    public async void CleanUp()
    {
        _pingThread?.Abort();

        List<Task> leaveRoomTasks = new List<Task>();

        foreach (IColyseusRoom roomEl in rooms)
        {
            leaveRoomTasks.Add(roomEl.Leave(false));
        }

        if (_room != null)
        {
            leaveRoomTasks.Add(_room.Leave(false));
        }

        await Task.WhenAll(leaveRoomTasks.ToArray());
    }
}