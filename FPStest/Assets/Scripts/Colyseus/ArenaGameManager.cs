using Colyseus;
using UnityEngine;
using LucidSightTools;

public class ArenaGameManager : MonoBehaviour
{
    public ColyseusNetworkedEntityView prefab;

    private void OnEnable()
    {
        ArenaRoomController.onAddNetworkEntity += OnNetworkAdd;
        ArenaRoomController.onRemoveNetworkEntity += OnNetworkRemove;
    }

    private void OnNetworkAdd(ArenaNetworkedEntity entity)
    {
        if (ArenaManager.Instance.HasEntityView(entity.id))
        {
            LSLog.LogImportant("View found! For " + entity.id);
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
    }

    private void CreateView(ArenaNetworkedEntity entity)
    {
        LSLog.LogImportant("print: " + JsonUtility.ToJson(entity));
        ColyseusNetworkedEntityView newView = Instantiate(prefab);
        ArenaManager.Instance.RegisterNetworkedEntityView(entity, newView);
        newView.gameObject.SetActive(true);
    }

    private void RemoveView(ColyseusNetworkedEntityView view)
    {
        view.SendMessage("OnEntityRemoved", SendMessageOptions.DontRequireReceiver);
    }
}