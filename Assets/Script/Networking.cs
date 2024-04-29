using System;
using System.Net.Sockets;
using System.Text;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetWorking : MonoBehaviour
{
    private const string SERVER_IP = "127.0.0.1"; // 서버 IP 주소
    private const int SERVER_PORT = 9000;         // 서버 포트 번호

    private TcpClient client;
    private NetworkStream stream;

    private bool islogin = false;
    private bool isSend = false;

    private string clientIp;
    private int clientPort;
    bool islog;
    public class Packet
    {
        public string ID;
        public string PW;
        public string IP;
        public int Port;
        public int p = 1;

        // Serialize 함수: 패킷을 바이트 배열로 직렬화하는 메서드
        public byte[] Serialize()
        {
            byte[] IDBytes = Encoding.UTF8.GetBytes(ID);
            byte[] PWBytes = Encoding.UTF8.GetBytes(PW);
            byte[] IPBytes = Encoding.UTF8.GetBytes(IP);
            byte[] PortBytes = BitConverter.GetBytes(Port);

            byte[] IDLengthBytes = BitConverter.GetBytes(IDBytes.Length);
            byte[] PWLengthBytes = BitConverter.GetBytes(PWBytes.Length);
            byte[] IPLengthBytes = BitConverter.GetBytes(IPBytes.Length);
            byte[] PortLengthBytes = BitConverter.GetBytes(PortBytes.Length);

            int intSize = sizeof(int);

            byte[] data = new byte[IDBytes.Length + PWBytes.Length + IPBytes.Length + PortBytes.Length + (4 * intSize)];

            Buffer.BlockCopy(IDLengthBytes, 0, data, 0, intSize);
            Buffer.BlockCopy(IDBytes, 0, data, intSize, IDBytes.Length);
            Buffer.BlockCopy(PWLengthBytes, 0, data, intSize + IDBytes.Length, intSize);
            Buffer.BlockCopy(PWBytes, 0, data, intSize + IDBytes.Length + intSize, PWBytes.Length);
            Buffer.BlockCopy(IPLengthBytes, 0, data, intSize + IDBytes.Length + intSize + PWBytes.Length, intSize);
            Buffer.BlockCopy(IPBytes, 0, data, intSize + IDBytes.Length + intSize + PWBytes.Length + intSize, IPBytes.Length);
            Buffer.BlockCopy(PortLengthBytes, 0, data, intSize + IDBytes.Length + intSize + PWBytes.Length + intSize + IPBytes.Length, intSize);
            Buffer.BlockCopy(PortBytes, 0, data, intSize + IDBytes.Length + intSize + PWBytes.Length + intSize + IPBytes.Length + intSize, PortBytes.Length);
            return data;
        }
    }
    Packet packet = new Packet();
    

    void Start()
    {
        ConnectToServer();
    }

    void Update()
    {
        if (islogin)
        {
            islogin = false;
            SendPacket(packet);
        }
    }

    void ConnectToServer()
    {
        try
        {
            client = new TcpClient(SERVER_IP, SERVER_PORT);
            stream = client.GetStream();
            Debug.Log("Connected to server");

            clientIp = ((System.Net.IPEndPoint)client.Client.LocalEndPoint).Address.ToString();
            clientPort = ((System.Net.IPEndPoint)client.Client.LocalEndPoint).Port;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
        }
    }

    void SendPacket(Packet packet)
    {
        try
        {
            client = new TcpClient(SERVER_IP, SERVER_PORT);
            stream = client.GetStream();
            Debug.Log("Connected to server");

            clientIp = ((System.Net.IPEndPoint)client.Client.LocalEndPoint).Address.ToString();
            clientPort = ((System.Net.IPEndPoint)client.Client.LocalEndPoint).Port;

            packet.IP = clientIp;
            packet.Port = clientPort;
            byte[] data = packet.Serialize();
            Debug.Log("f1143431");
            stream.Write(data, 0, data.Length);
            Debug.Log("f11189978978789");
            Debug.Log("Packet sent to server");
            RecvMessage();

        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send packet to server: {e.Message}");
        }
    }

    void RecvMessage()
    {
        try
        {
            byte[] data = new byte[sizeof(bool)];
            Debug.Log("fdafd");
            int dataLength = stream.Read(data, 0, data.Length);
            Debug.Log("f11111");
            if (dataLength == sizeof(bool))
            {
                islog = BitConverter.ToBoolean(data, 0);
                Debug.Log(islog);
            }

        }
        catch (Exception ex)
        {
            Debug.LogError("Exception: " + ex.Message);
        }

    }

    void OnDestroy()
    {
        if (client != null)
        {
            client.Close();
            Debug.Log("Disconnected from server");
        }
    }

    public void IsLogin(string ID, string PW)
    {
        packet.ID = ID;
        packet.PW = PW;
        SendPacket(packet);
        
    }
}
