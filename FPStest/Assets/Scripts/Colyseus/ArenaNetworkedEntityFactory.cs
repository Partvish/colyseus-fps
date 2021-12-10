using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using GameDevWare.Serialization;
using LucidSightTools;

public class ArenaNetworkedEntityFactory
{
    private readonly Dictionary<string, Action<ArenaNetworkedEntity>> _creationCallbacks;
    private readonly IndexedDictionary<string, ArenaNetworkedEntity> _entities;
    private readonly IndexedDictionary<string, ArenaNetworkedEntityView> _entityViews;

    public ArenaNetworkedEntityFactory(Dictionary<string, Action<ArenaNetworkedEntity>> creationCallbacks, IndexedDictionary<string, ArenaNetworkedEntity> entities, IndexedDictionary<string, ArenaNetworkedEntityView> entityViews)
    {
        _creationCallbacks = creationCallbacks;
        _entities = entities;
        _entityViews = entityViews;
    }

    public void InstantiateNetworkedEntity(ColyseusRoom<ArenaRoomState> room, string prefab, Vector3 position, Quaternion rotation,
        Dictionary<string, object> attributes = null)
    {
        if (string.IsNullOrEmpty(prefab))
        {
            LSLog.LogError("No Prefab Declared");
            return;
        }

        if (attributes != null)
        {
            attributes.Add("creationPos", new object[3] { position.x, position.y, position.z });
            attributes.Add("creationRot", new object[4] { rotation.x, rotation.y, rotation.z, rotation.w });
        }
        else
        {
            attributes = new Dictionary<string, object>()
            {
                ["creationPos"] = new object[3] { position.x, position.y, position.z },
                ["creationRot"] = new object[4] { rotation.x, rotation.y, rotation.z, rotation.w }
            };
        }

        CreateNetworkedEntity(room, prefab, attributes);
    }

    public void CreateNetworkedEntity(ColyseusRoom<ArenaRoomState> room, string prefab, Dictionary<string, object> attributes = null, ColyseusNetworkedEntityView viewToAssign = null, Action<ArenaNetworkedEntity> callback = null)
    {
        Dictionary<string, object> updatedAttributes = (attributes != null)
            ? new Dictionary<string, object>(attributes)
            : new Dictionary<string, object>();
        updatedAttributes.Add("prefab", prefab);
        CreateNetworkedEntity(room, updatedAttributes, viewToAssign, callback);
    }

    public void CreateNetworkedEntity(ColyseusRoom<ArenaRoomState> room, Dictionary<string, object> attributes = null, ColyseusNetworkedEntityView viewToAssign = null, Action<ArenaNetworkedEntity> callback = null)
    {
        try
        {
            string creationId = null;

            if (viewToAssign != null || callback != null)
            {
                creationId = Guid.NewGuid().ToString();
                if (callback != null)
                {
                    if (viewToAssign != null)
                    {
                        _creationCallbacks.Add(creationId, (newEntity) =>
                        {
                            RegisterNetworkedEntityView(newEntity, viewToAssign);
                            callback.Invoke(newEntity);
                        });
                    }
                    else
                    {
                        _creationCallbacks.Add(creationId, callback);
                    }
                }
                else
                {
                    _creationCallbacks.Add(creationId,
                        (newEntity) => { RegisterNetworkedEntityView(newEntity, viewToAssign); });
                }
            }

            _ = room.Send("createEntity",
                new EntityCreationMessage() { creationId = creationId, attributes = attributes });
        }
        catch (System.Exception err)
        {
            LSLog.LogError(err.Message + err.StackTrace);
        }

    }

    public void CreateNetworkedEntityWithTransform(ColyseusRoom<ArenaRoomState> room, Vector3 position, Quaternion rotation,
        Dictionary<string, object> attributes = null, ColyseusNetworkedEntityView viewToAssign = null,
        Action<ArenaNetworkedEntity> callback = null)
    {
        if (attributes != null)
        {
            attributes.Add("creationPos", new object[3] { position.x, position.y, position.z });
            attributes.Add("creationRot", new object[4] { rotation.x, rotation.y, rotation.z, rotation.w });
        }
        else
        {
            attributes = new Dictionary<string, object>()
            {
                ["creationPos"] = new object[3] { position.x, position.y, position.z },
                ["creationRot"] = new object[4] { rotation.x, rotation.y, rotation.z, rotation.w }
            };
        }

        CreateNetworkedEntity(room, attributes, viewToAssign, callback);
    }

    public async Task CreateFromPrefab(ArenaNetworkedEntity entity)
    {
        LSLog.LogError($"Factory - Create From Prefab - {entity.id}");

        ResourceRequest asyncItem = Resources.LoadAsync<ArenaNetworkedEntityView>(entity.attributes["prefab"]);
        while (asyncItem.isDone == false)
        {
            await Task.Yield();
        }

        ArenaNetworkedEntityView view = UnityEngine.Object.Instantiate((ArenaNetworkedEntityView)asyncItem.asset);
        if (view == null)
        {
            LSLog.LogError("Instantiated Object is not of ArenaNetworkedEntityView Type");
            asyncItem = null;
            return;
        }

        RegisterNetworkedEntityView(entity, view);
    }

    public void RegisterNetworkedEntityView(ArenaNetworkedEntity model, ColyseusNetworkedEntityView view)
    {
        if (string.IsNullOrEmpty(model.id) || view == null || _entities.ContainsKey(model.id) == false)
        {
            LSLog.LogError("Cannot Find Entity in Room");
            return;
        }

        ArenaNetworkedEntityView entityView = (ArenaNetworkedEntityView)view;

        if (entityView && !entityView.HasInit)
        {
            entityView.InitiView(model);
        }

        _entityViews.Add(model.id, (ArenaNetworkedEntityView)view);
        view.SendMessage("OnEntityViewRegistered", SendMessageOptions.DontRequireReceiver);
    }

    public void UnregisterNetworkedEntityView(ArenaNetworkedEntity model)
    {
        if (string.IsNullOrEmpty(model.id) || _entities.ContainsKey(model.id) == false)
        {
            LSLog.LogError("Cannot Find Entity in Room");
            return;
        }

        ArenaNetworkedEntityView view = _entityViews[model.id];

        _entityViews.Remove(model.id);
        view.SendMessage("OnEntityViewUnregistered", SendMessageOptions.DontRequireReceiver);
    }
}