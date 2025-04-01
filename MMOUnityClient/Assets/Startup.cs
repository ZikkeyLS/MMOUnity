using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

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

public class Startup : MonoBehaviour
{
    public static Startup Instance;

    public const int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 5555;
        
    public TcpClient socket;
    private NetworkStream stream;
    private byte[] receiveBuffer;

    private List<PacketRead> packets = new List<PacketRead>();
    private object locker = new object();

    private Dictionary<PacketType, UnityEvent<PacketRead>> NetworkHandlers = new Dictionary<PacketType, UnityEvent<PacketRead>>();
    public void AssignHandler(PacketType type, UnityAction<PacketRead> eventMessage)
    {
        NetworkHandlers.TryGetValue(type, out var unityEvent);

        if (unityEvent == null)
        {
            unityEvent = new UnityEvent<PacketRead>();
            NetworkHandlers.Add(type, unityEvent);
        }

        if (unityEvent != null)
        {
            unityEvent.AddListener(eventMessage);
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Application.runInBackground = true;

        Connect();
    }

    private void Update()
    {
        lock (locker)
        {
            foreach (var packetRead in packets)
            {
                NetworkHandlers.TryGetValue(packetRead.type, out var handler);
                if (handler != null)
                {
                    handler.Invoke(packetRead);
                }
            }

            packets.Clear();
        }
    }

    public void Connect()
    {
        socket = new TcpClient
        {
            ReceiveBufferSize = dataBufferSize,
            SendBufferSize = dataBufferSize
        };

        receiveBuffer = new byte[dataBufferSize];
        socket.BeginConnect(ip, port, ConnectCallback, socket);
    }

    public void SendData(PacketWrite packet)
    {
        try
        {
            if (socket != null)
            {
                stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null); // Send data to server
            }
        }
        catch (Exception ex)
        {
            print($"Error sending data to server via TCP: {ex}");
        }
    }

    private void ConnectCallback(IAsyncResult _result)
    {
        socket.EndConnect(_result);

        if (!socket.Connected)
        {
            return;
        }

        print("Connected");

        stream = socket.GetStream();

        stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
    }

    private void ReceiveCallback(IAsyncResult _result)
    {
        try
        {
            int _byteLength = stream.EndRead(_result);
            if (_byteLength <= 0)
            {
                Disconnect("0 byte length");
                return;
            }

            byte[] _data = new byte[_byteLength];
            Array.Copy(receiveBuffer, _data, _byteLength);

            HandleData(_data);

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }
        catch
        {
            Disconnect("error");
        }
    }

    private void HandleData(byte[] data)
    {
        if (data.Length < 2)
        {
            return;
        }

        PacketRead packetRead = new PacketRead(data);

        lock (locker)
        {
            packets.Add(packetRead);
        }
    }

    private void Disconnect(string reason = "")
    {
        print("Disconnected " + reason);

        if (socket != null)
        {
            socket.Dispose();
        }
        stream = null;
        receiveBuffer = null;
        socket = null;
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}
