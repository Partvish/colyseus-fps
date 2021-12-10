using System;
using Colyseus;
using LucidSightTools;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ArenaNetworkedEntityView : ColyseusNetworkedEntityView
{
    public string OwnerId { get; private set; }
    public int RefId { get; private set; }
    public bool IsEntityOwner { get; private set; }
    public bool IsMine { get; private set; }
    public bool HasInit { get; private set; }

    public bool autoInitEntity = false;
    public string prefabName;

    [HeaderAttribute("Position Sync")]
    public float positionLerpSpeed = 5f;
    [HeaderAttribute("Rotation Sync")]
    public float snapIfAngleIsGreater = 100f;
    public float rotationLerpSpeed = 5f;

    public float stateSyncUpdateRateMs = 100f;

    public double interpolationBackTimeMs = 200f;
    public double extrapolationLimitMs = 500f;
    public float maxSpeedDeltaSqr = 9f;

    public bool syncLocalPosition = true;
    public bool syncLocalRotation = true;
    public bool checkForSpeedHacks = false;

    [SerializeField]
    protected ArenaNetworkedEntity state;
    protected ArenaNetworkedEntity previousState;
    protected ArenaNetworkedEntity localUpdatedState;

    protected double lastStateTimestamp;
    private float counterStateSyncUpdateRate = 0f;
    private bool lerpPosition = false;
    private long lastFullSync = 0;

    [SerializeField]
    protected EntityState[] proxyStates = new EntityState[20];

    protected int proxyStateCount;

    protected Transform myTransform;

    public Vector3 LocalPositionDelta
    {
        get
        {
            return localPositionDelta;
        }
    }
    protected Vector3 localPositionDelta;

    private Vector3 prevLocalPosition;

    [System.Serializable]
    protected struct EntityState
    {
        public double timestamp;
        public Vector3 pos;
        public Vector3 vel;
        public Quaternion rot;
        public Colyseus.Schema.MapSchema<string> attributes;
    }

    protected virtual void Awake()
    {
        myTransform = transform;
    }

    protected virtual void Start()
    {
        if (autoInitEntity)
            InitiWithServer();
    }

    public void InitiWithServer()
    {
        StartCoroutine("Co_InitiWithServer");
    }

    IEnumerator Co_InitiWithServer()
    {
        while (ArenaManager.Instance.IsInRoom == false)
        {
            yield return 0;
        }

        if (autoInitEntity && HasInit == false && !string.IsNullOrEmpty(prefabName))
        {
            ArenaManager.CreateNetworkedEntity(prefabName, null, this);
        }
        else if (autoInitEntity && HasInit == false && string.IsNullOrEmpty(prefabName))
        {
            LSLog.LogError("Prefab Name / Location Must be set");
        }
    }


    public void InitiView(ArenaNetworkedEntity entity)
    {
        try
        {
            state = entity;
            IsMine = ArenaManager.Instance.CurrentUser != null && string.Equals(ArenaManager.Instance.CurrentUser.id, state.ownerId);
            state.attributes.OnChange += Attributes_OnChange;
            state.OnChange += Entity_State_OnChange;

            OwnerId = state.ownerId;
            Id = state.id;
            RefId = state.__refId;
            SetStateStartPos();
            if (myTransform == null) myTransform = transform;
            prevLocalPosition = myTransform.localPosition;

            HasInit = true;
        }
        catch (System.Exception e)
        {
            LSLog.LogError($"Error: {e.Message + e.StackTrace}");
        }
    }

    protected virtual void SetStateStartPos()
    {
        gameObject.transform.localPosition = new Vector3((float)state.xPos, (float)state.yPos, (float)state.zPos);
        gameObject.transform.localRotation = new Quaternion((float)state.xRot, (float)state.yRot, (float)state.zRot, (float)state.wRot);
        gameObject.transform.localScale = new Vector3((float)state.xScale, (float)state.yScale, (float)state.zScale);
    }

    public virtual void OnEntityRemoved()
    {
    }

    virtual protected void Entity_State_OnChange(List<Colyseus.Schema.DataChange> changes)
    {

        lastStateTimestamp = state.timestamp;
        if (!IsMine)
        {
            UpdateViewFromState();
        }
    }

    virtual protected void Attributes_OnChange(string value, string key)
    {
    }

    virtual protected void UpdateViewFromState()
    {

        Vector3 pos = new Vector3((float)state.xPos, (float)state.yPos, (float)state.zPos);
        Vector3 velocity = new Vector3((float)state.xVel, (float)state.yVel, (float)state.zVel);
        Quaternion rot = new Quaternion((float)state.xRot, (float)state.yRot, (float)state.zRot, (float)state.wRot);

        if (!syncLocalPosition)
        {
            pos = myTransform.localPosition;
            velocity = localPositionDelta;
        }

        if (!syncLocalRotation)
        {
            rot = myTransform.localRotation;
        }


        for (int i = proxyStates.Length - 1; i >= 1; i--)
        {
            proxyStates[i] = proxyStates[i - 1];
        }

        EntityState newState = new EntityState() { timestamp = state.timestamp }; 

        newState.pos = pos;
        newState.vel = velocity;
        newState.rot = rot;
        newState.attributes = state.Clone().attributes;
        proxyStates[0] = newState;

        proxyStateCount = Mathf.Min(proxyStateCount + 1, proxyStates.Length);
    }

    protected virtual void UpdateStateFromView()
    {

        previousState = state.Clone();

        state.xPos = (float)System.Math.Round((decimal)transform.localPosition.x, 4);
        state.yPos = (float)System.Math.Round((decimal)transform.localPosition.y, 4);
        state.zPos = (float)System.Math.Round((decimal)transform.localPosition.z, 4);

        state.xRot = transform.localRotation.x;
        state.yRot = transform.localRotation.y;
        state.zRot = transform.localRotation.z;
        state.wRot = transform.localRotation.w;

        state.xScale = transform.localScale.x;
        state.yScale = transform.localScale.y;
        state.zScale = transform.localScale.z;

        state.xVel = localPositionDelta.x;
        state.yVel = localPositionDelta.y;
        state.zVel = localPositionDelta.z;

        if (localUpdatedState != null)
        {
            List<ArenaPropertyCompareResult> changesLocal = Compare(localUpdatedState, state);
            if (changesLocal.Count == 0 || (changesLocal.Count == 1 && changesLocal[0].Name == "timestamp"))
            {
                return;
            }
        }

        List<ArenaPropertyCompareResult> changes = Compare(previousState, state);

        if (changes.Count > 0)
        {
            object[] changeSet = new object[(changes.Count * 2) + 1];
            changeSet[0] = state.id;
            int saveIndex = 1;
            for (int i = 0; i < changes.Count; i++)
            {
                changeSet[saveIndex] = changes[i].Name;
                changeSet[saveIndex + 1] = changes[i].NewValue;
                saveIndex += 2;
            }
            localUpdatedState = state.Clone();
            ArenaManager.NetSend("entityUpdate", changeSet);
        }

    }

    protected virtual void Update()
    {

        if (IsMine)
        {
            counterStateSyncUpdateRate += Time.deltaTime;
            if (counterStateSyncUpdateRate > stateSyncUpdateRateMs / 1000f)
            {
                counterStateSyncUpdateRate = 0f;
                UpdateStateFromView();
            }

        }
        else if (!IsMine && (syncLocalPosition || syncLocalRotation))
        {
            ProcessViewSync();
        }

        localPositionDelta = myTransform.localPosition - prevLocalPosition;
        prevLocalPosition = myTransform.localPosition;

    }

    public void RemoteFunctionCallHandler(ArenaRFCMessage _rfc)
    {
        System.Type thisType = this.GetType();

        MethodInfo theMethod = thisType.GetMethod(_rfc.function);
        if (theMethod != null)
            theMethod.Invoke(this, _rfc.param);
        else
            LSLog.LogError("Missing Fucntion: " + _rfc.function);
    }


    protected virtual void ProcessViewSync()
    {
        double interpolationTime = ArenaManager.Instance.GetServerTime - interpolationBackTimeMs;

        if (proxyStates[0].timestamp > interpolationTime)
        {
            float deltaFactor = (ArenaManager.Instance.GetServerTimeSeconds > proxyStates[0].timestamp) ?
                (float)(ArenaManager.Instance.GetServerTimeSeconds - proxyStates[0].timestamp) * 0.2f : 0f;

            if (syncLocalPosition) myTransform.localPosition = Vector3.Lerp(myTransform.localPosition, proxyStates[0].pos, Time.deltaTime * (positionLerpSpeed + deltaFactor));

            if (syncLocalRotation && Mathf.Abs(Quaternion.Angle(transform.localRotation, proxyStates[0].rot)) > snapIfAngleIsGreater) myTransform.localRotation = proxyStates[0].rot;

            if (syncLocalRotation) myTransform.localRotation = Quaternion.Slerp(myTransform.localRotation, proxyStates[0].rot, Time.deltaTime * (rotationLerpSpeed + deltaFactor));
        }
        else
        {
            EntityState latest = proxyStates[0];

            float extrapolationLength = (float)(interpolationTime - latest.timestamp);
            if (extrapolationLength < extrapolationLimitMs / 1000f)
            {
                if (syncLocalPosition) myTransform.localPosition = latest.pos + latest.vel * extrapolationLength;
                if (syncLocalRotation) myTransform.localRotation = latest.rot;
            }
        }
    }

    protected T ParseRFCObject<T>(object raw)
        where T : class, new()
    {
        IDictionary<string, object> parseFrom = new Dictionary<string, object>();
        foreach (DictionaryEntry item in (IEnumerable)raw)
        {
            parseFrom.Add(item.Key.ToString(), item.Value);
        }

        return parseFrom.ToObject<T>();
    }


    public void SetAttributes(Dictionary<string, string> attributesToSet)
    {
        ArenaManager.NetSend("setAttribute", new ArenaAttributeUpdateMessage() { entityId = state.id, attributesToSet = attributesToSet });
    }

    protected static List<ArenaPropertyCompareResult> Compare<T>(T oldObject, T newObject)
    {
        FieldInfo[] properties = typeof(T).GetFields();
        List<ArenaPropertyCompareResult> result = new List<ArenaPropertyCompareResult>();

        foreach (FieldInfo pi in properties)
        {

            object oldValue = pi.GetValue(oldObject), newValue = pi.GetValue(newObject);

            if (!object.Equals(oldValue, newValue))
            {
                result.Add(new ArenaPropertyCompareResult(pi.Name, oldValue, newValue));
            }
        }

        return result;
    }
}

namespace Colyseus
{
    public class ColyseusNetworkedEntityView : MonoBehaviour
    {
        public string Id { get; protected set; }
    }
}