using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

[Serializable]
public class Settings
{
    public int port = 5555;
    public int maxPlayers = 100;
}

public class Packet
{
    public List<byte> data = new List<byte>() { 1, 2, 3, 4, 5, 6, 7, 8 };

    public byte[] ToArray()
    {
        return data.ToArray();
    }

    public int Length()
    {
        return data.Count;
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

        SendData(new Packet());
    }

    public bool Connected => socket != null;

    public void SendData(Packet packet)
    {
        try
        {
            if (socket != null)
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
                Disconnect();
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
            Disconnect();
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

    public void Disconnect()
    {
        Debug.Log("CLIENT DISCONNECT");

        socket.Close();
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
        LoadSettings();
        RunServer();
    }

    private void OnApplicationQuit()
    {
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

        for (int i = 0; i < settings.maxPlayers; i++)
        {
            if (clients.TryGetValue(i, out Client tempClient))
            {
                if (tempClient.Connected == false)
                {
                    Debug.Log($"Incoming connection from {client.Client.RemoteEndPoint}... ID: {i}");
                    currentClient.Connect(client);
                    clients[i] = currentClient;
                    return;
                }
            }
            else
            {
                Debug.Log($"Incoming connection from {client.Client.RemoteEndPoint}... ID: {i}");
                currentClient.Connect(client);
                clients.Add(i, currentClient);
                return;
            }
        }

        Debug.Log($"{client.Client.RemoteEndPoint} failed to connect: Server full!");
    }
}
