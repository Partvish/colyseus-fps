using Colyseus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI clientCount = null;

    [SerializeField]
    private Button joinButton = null;

    private RoomSelectionMenu menuRef;

    [SerializeField]
    private TextMeshProUGUI roomName = null;

    private ColyseusRoomAvailable roomRef;

    public void Initialize(ColyseusRoomAvailable roomReference, RoomSelectionMenu menu)
    {
        menuRef = menu;
        roomRef = roomReference;
        roomName.text = roomReference.roomId;
        string maxClients = roomReference.maxClients > 0 ? roomReference.maxClients.ToString() : "--";
        clientCount.text = $"{roomReference.clients} / {maxClients}";

        if (roomReference.maxClients > 0 && roomReference.clients >= roomReference.maxClients)
        {
            joinButton.interactable = false;
        }
        else
        {
            joinButton.interactable = true;
        }
    }

    public void TryJoin()
    {
        menuRef.JoinRoom(roomRef.roomId);
    }
}