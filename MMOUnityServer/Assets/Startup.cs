using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[Serializable]
public class Settings
{
    public int port = 5555;
    public int maxPlayers = 100;
}

public enum PacketType : ushort
{
    // Client packet
    RequestRegister = 0,
    RequestLogin = 1,
    GetChatMessage = 3,
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
        string result = Encoding.ASCII.GetString(byteArray, currentByte, size);
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

    private readonly int id;
    private NetworkStream stream;
    private byte[] receiveBuffer;

    public void Connect(TcpClient socket)
    {
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
        if (data.Length < 1)
        {
            return;
        }

        Debug.Log("handle data");
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

    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 30;

        LoadSettings();
        RunServer();
    }

    private void Update()
    {
        foreach (var client in clients)
        {
            PacketWrite packet = new PacketWrite(PacketType.GetChatMessage);
            packet.WriteString(DateTime.Now.ToString());

            client.Value.SendData(packet);
        }
    }

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
                currentClient.Connect(client);
                return;
            }
        }

        Debug.Log($"{client.Client.RemoteEndPoint} failed to connect: Server full!");
    }
}
