using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class Settings
{
    public int port = 5555;
    public int maxPlayers = 100;
}

public enum PacketType : ushort
{
    // Client packet
    GetTimezone = 0,
    // Server packet
    ResponseRegister = 10000,
    ResponseLogin = 10001,
}

public class PacketWrite
{
    public List<byte> data = new List<byte>();

    public PacketType type;

    public PacketWrite(PacketType packetType)
    {
        type = packetType;
    }

    public void WriteString(string message)
    {
        byte[] messageData = Encoding.UTF8.GetBytes(message);

        data.AddRange(BitConverter.GetBytes(messageData.Length));
        data.AddRange(messageData);
    }

    public byte[] ToArray()
    {
        List<byte> result = new List<byte>();
        result.AddRange(BitConverter.GetBytes((ushort)type));
        result.AddRange(data);
        return result.ToArray();
    }

    public int Length()
    {
        return data.Count + sizeof(ushort);
    }
}

public class PacketRead
{
    public byte[] byteArray;
    public PacketType type;
    public int currentByte = 0;

    public string ReadString()
    {
        int size = BitConverter.ToInt32(byteArray, currentByte);
        currentByte += 4;
        string result = Encoding.UTF8.GetString(byteArray, currentByte, size);
        currentByte += size;
        return result;
    }

    public PacketRead(byte[] bytes)
    {
        if (bytes.Length < 2)
        {
            return;
        }

        byteArray = bytes;

        type = (PacketType)BitConverter.ToUInt16(bytes, 0);
        currentByte += sizeof(ushort);
    }
}

public class Client
{
    public const int dataBufferSize = 4096;

    public TcpClient socket;

    private int id;
    private NetworkStream stream;
    private byte[] receiveBuffer;

    public void Connect(TcpClient socket, int id)
    {
        this.id = id;
        this.socket = socket;
        this.socket.ReceiveBufferSize = dataBufferSize;
        this.socket.SendBufferSize = dataBufferSize;

        stream = socket.GetStream();

        receiveBuffer = new byte[dataBufferSize];

        stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
    }

    public bool Connected => socket != null;

    public void SendData(PacketWrite packet)
    {
        try
        {
            if (socket != null && stream != null)
            {
                stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Error sending data to player {id} via TCP: {ex}");
        }
    }

    private void ReceiveCallback(IAsyncResult result)
    {
        try
        {
            int byteLength = stream.EndRead(result);
            if (byteLength <= 0)
            {
                Disconnect("0 byte length");
                return;
            }

            byte[] data = new byte[byteLength];
            Array.Copy(receiveBuffer, data, byteLength);

            HandleData(data);

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }
        catch (Exception _ex)
        {
            Debug.Log($"Error receiving TCP data: {_ex}");
            Disconnect("error receiving data");
        }
    }

    private void HandleData(byte[] data)
    {
        if (data.Length < 2)
        {
            return;
        }

        PacketRead packetRead = new PacketRead(data);

        lock (Startup.locker)
        {
            Startup.packets.Add(new KeyValuePair<int, PacketRead>(id, packetRead));
        }
    }

    public void Disconnect(string reason = "")
    {
        Debug.Log("CLIENT DISCONNECTED: " + reason);

        if (socket != null)
        {
            socket.Close();
        }
        stream = null;
        receiveBuffer = null;
        socket = null;
    }
}

public class Startup : MonoBehaviour
{
    private Settings settings = new Settings();

    private TcpListener listener = null;
    private Dictionary<int, Client> clients = new Dictionary<int, Client>();

    public static List<KeyValuePair<int, PacketRead>> packets = new List<KeyValuePair<int, PacketRead>>();
    public static object locker = new object();

    private Dictionary<string, string> cityNamesUrls = new Dictionary<string, string>();

    private Dictionary<PacketType, UnityEvent<PacketRead, int>> NetworkHandlers = new Dictionary<PacketType, UnityEvent<PacketRead, int>>();
    public void AssignHandler(PacketType type, UnityAction<PacketRead, int> eventMessage)
    {
        NetworkHandlers.TryGetValue(type, out var unityEvent);

        if (unityEvent == null)
        {
            unityEvent = new UnityEvent<PacketRead, int>();
            NetworkHandlers.Add(type, unityEvent);
        }

        if (unityEvent != null)
        {
            unityEvent.AddListener(eventMessage);
        }
    }

    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 30;

        using (var webClient = new WebClient())
        {
            string data = webClient.DownloadString($"https://true-time.com/city-list/");

            string match = "<a href=\"";
            string end = "</a>";

            int currentIndex = 0;

            while (currentIndex < data.Length && currentIndex != -1)
            {
                int index = data.IndexOf(match, currentIndex);

                if (currentIndex == -1 || index == -1)
                {
                    break;
                }

                currentIndex = index + 1;

                try
                {
                    int endSuc = 0;
                    int localIndex = index;

                    StringBuilder aPointer = new StringBuilder();

                    while (endSuc != end.Length)
                    {
                        if (localIndex >= data.Length)
                        {
                            break;
                        }

                        aPointer.Append(data[localIndex]);

                        if (data[localIndex] == end[endSuc])
                        {
                            endSuc++;
                        }
                        else
                        {
                            endSuc = 0;
                        }

                        localIndex += 1;
                    }

                    string aData = aPointer.ToString();
                    if (aData.Contains("Главная"))
                    {
                        continue;
                    }

                    aData = aData.Replace("<a href=\"", "");
                    aData = aData.Replace("\">", "|");
                    aData = aData.Replace("</a>", "");

                    string[] parts = aData.Split("|");

                    int lastIndexOf = parts[1].LastIndexOf(" ");
                    string cityName = lastIndexOf > 0 ? parts[1].Substring(0, lastIndexOf) : parts[1];
                    cityNamesUrls[cityName] = parts[0];
                }
                catch
                {

                }
            }

            foreach (var item in cityNamesUrls)
            {
                File.AppendAllText("cities.txt", item.Key + " " + item.Value + "\n");
            }
        }

        LoadSettings();
        RunServer();

        AssignHandler(PacketType.GetTimezone, GetTimezoneHandler);
    }

    private void GetTimezoneHandler(PacketRead packet, int clientId)
    {
        string cityName = packet.ReadString();

        string result = "Not found";

        try
        {
            using (var webClient = new WebClient())
            {
                if (cityNamesUrls.TryGetValue(cityName, out string url))
                {
                    result = webClient.DownloadString($"https://true-time.com{url}");

                    string template = "<span class=\"hour-minutes-string\">";

                    int startIndex = result.IndexOf(template) + template.Length;

                    string rawData = result.Substring(startIndex, 100);
                    result = Regex.Replace(rawData, "[^0-9:]+", "");
                }
            }
        }
        catch
        {
            result = "Not found";
        }

        PacketWrite response = new PacketWrite(PacketType.GetTimezone);
        response.WriteString(result);

        clients.TryGetValue(clientId, out Client client);
        if (client != null)
        {
            client.SendData(response);
        }
    }

    private void Update()
    {
        lock (locker)
        {
            foreach (var packetRead in packets)
            {
                NetworkHandlers.TryGetValue(packetRead.Value.type, out var handler);
                if (handler != null)
                {
                    handler.Invoke(packetRead.Value, packetRead.Key);
                }
            }

            packets.Clear();
        }
    }

    //private void Update()
    //{
    //    foreach (var client in clients)
    //    {
    //        PacketWrite packet = new PacketWrite(PacketType.GetChatMessage);
    //        packet.WriteString(DateTime.Now.ToString());

    //        client.Value.SendData(packet);
    //    }
    //}

    private void OnApplicationQuit()
    {
        foreach (var client in clients)
        {
            client.Value.Disconnect();
        }
        clients.Clear();

        listener.Server.Close();
        listener.Server.Dispose();
        listener.Stop();
        listener = null;
    }

    private void LoadSettings()
    {
        if (File.Exists("settings.json") == false)
        {
            File.WriteAllText("settings.json", JsonUtility.ToJson(settings));
        }

        settings = JsonUtility.FromJson<Settings>(File.ReadAllText("settings.json"));
    }

    private void RunServer()
    {
        listener = new TcpListener(IPAddress.Any, settings.port);
        listener.Start();
        listener.BeginAcceptTcpClient(TCPConnectCallback, null);

        print("Server started!");
    }

    private void TCPConnectCallback(IAsyncResult result)
    {
        TcpClient client = listener.EndAcceptTcpClient(result);
        listener.BeginAcceptTcpClient(TCPConnectCallback, null);

        Client currentClient = new Client();

        bool success = false;
        for (int i = 0; i < settings.maxPlayers; i++)
        {
            if (clients.TryGetValue(i, out Client tempClient))
            {
                if (tempClient.Connected == false)
                {
                    success = true;
                    clients[i] = currentClient;
                }
            }
            else
            {
                success = true;
                clients.Add(i, currentClient);
            }

            if (success)
            {
                Debug.Log($"Incoming connection from {client.Client.RemoteEndPoint}... ID: {i}");
                currentClient.Connect(client, i);
                return;
            }
        }

        Debug.Log($"{client.Client.RemoteEndPoint} failed to connect: Server full!");
    }
}
