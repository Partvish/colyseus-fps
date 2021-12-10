using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LucidSightTools;

public class ModellController : MonoBehaviour
{
    public virtual void OnHit(string entityID)
    {
        
        if (entityID.Equals(ArenaManager.Instance.CurrentNetworkedEntity.id))
        {
            string id = gameObject.GetComponentInParent<PlayerController>().Id;
            LSLog.Log("Shot to: " + id + " From: " + entityID);
            ArenaGameSceneManager.Instance.RegisterPlayerHit(entityID, id);
        }
    }
}
