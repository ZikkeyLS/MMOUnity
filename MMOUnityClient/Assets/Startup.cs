using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

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

public class Startup : MonoBehaviour
{
    public const int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 5555;
        
    public TcpClient socket;
    private NetworkStream stream;
    private byte[] receiveBuffer;

    private void Start()
    {
        Connect();
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

    public void SendData(Packet packet)
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
                Disconnect();
                return;
            }

            byte[] _data = new byte[_byteLength];
            Array.Copy(receiveBuffer, _data, _byteLength);

            HandleData(_data);

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }
        catch
        {
            Disconnect();
        }
    }

    private void HandleData(byte[] data)
    {
        if (data.Length < 1)
        {
            return;
        }

        StringBuilder aa = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            aa.Append(data[i].ToString());
        }
        print(aa.ToString());
    }

    private void Disconnect()
    {
        socket.Dispose();
        stream = null;
        receiveBuffer = null;
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}
