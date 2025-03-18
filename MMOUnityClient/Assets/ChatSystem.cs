using UnityEngine;

public class ChatSystem : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text text;

    private void Start()
    {
        Startup.AssignHandler(PacketType.GetChatMessage, GetChatMessage);
    }

    private void GetChatMessage(PacketRead read)
    {
        string chatMessage = read.ReadString();
        text.text = chatMessage;
    }
}
