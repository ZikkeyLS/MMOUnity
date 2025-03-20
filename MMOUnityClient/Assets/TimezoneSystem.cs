using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class Timezone
{
    public int year;
    public int month;
    public int day;
    public int hour;
    public int minute;
    public int seconds;
    public int milliSeconds;
    public string dateTime;
    public string date;
    public string time;
    public string timeZone;
    public string dayOfWeek;
    public bool dstActive;
}

public class TimezoneSystem : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_InputField _input;
    [SerializeField] private TMPro.TMP_Text _output;
    [SerializeField] private Button _submit;

    private void Start()
    {
        Startup.Instance.AssignHandler(PacketType.GetTimezone, GetTimezoneMessage);

        _submit.onClick.AddListener(SendTimezoneRequest);
    }

    public void SendTimezoneRequest()
    {
        PacketWrite packetWrite = new PacketWrite(PacketType.GetTimezone);
        packetWrite.WriteString(_input.text);

        Startup.Instance.SendData(packetWrite);
    }

    private void GetTimezoneMessage(PacketRead read)
    {
        string chatMessage = read.ReadString();

        try
        {
            Timezone timezone = JsonUtility.FromJson<Timezone>(chatMessage);
            _output.text = timezone.date + " " + timezone.time + "\n" + timezone.timeZone;
        }
        catch
        {
            _output.text = "Not found";
        }
    }
}
