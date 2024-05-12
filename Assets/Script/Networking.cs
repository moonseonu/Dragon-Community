using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class NetWorking : MonoBehaviour
{
    private const string SERVER_IP = "127.0.0.1"; // 서버 IP 주소
    private const int SERVER_PORT = 9000;         // 서버 포트 번호
    private const int UDPPORT = 12345;
    private TcpClient Tclient;
    private UdpClient Uclient;
    private NetworkStream stream;
    private bool isSend = false;

    private string clientIp;
    private int clientPort;
    private bool islogin;
    private bool ismatching;
    public bool Islogin { get { return islogin; } }
    public bool Ismatching {  get { return ismatching; } }
    public class LoginPacket
    {
        public string ID;
        public string PW;
        public string IP;
        public int Port;
        public int p = 1;

        // Serialize 함수: 패킷을 바이트 배열로 직렬화하는 메서드
        public byte[] Packing()
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
    public LoginPacket login = new LoginPacket();
    
    public class PlayerInfo
    {
        public string ID;
        public string IP;
    }

    public List<PlayerInfo> PlayerInfos = new List<PlayerInfo>();

    void Start()
    {
        ConnectToServer();
    }

    void Update()
    {

    }

    void ConnectToServer()
    {
        try
        {
            Tclient = new TcpClient(SERVER_IP, SERVER_PORT);

            clientIp = ((System.Net.IPEndPoint)Tclient.Client.LocalEndPoint).Address.ToString();
            clientPort = ((System.Net.IPEndPoint)Tclient.Client.LocalEndPoint).Port;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
        }
    }

    void ConnectToClient()
    {
        try
        {
            Uclient = new UdpClient(SERVER_IP, SERVER_PORT);
            IPAddress serverAddress = IPAddress.Parse(SERVER_IP);
            IPEndPoint endPoint = new IPEndPoint(serverAddress, SERVER_PORT);
            Debug.Log("FDAfd");
            // 연결된 서버로 메시지 보내기 (예: "Hello, server!")
            string message = "Hello, server!";
            byte[] data = Encoding.UTF8.GetBytes(message);
            Uclient.Send(data, data.Length, endPoint);

            byte[] receivedData = Uclient.Receive(ref endPoint);
            string response = Encoding.UTF8.GetString(receivedData);
            Debug.Log("Received from server: " + response);

        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to client: {e.Message}");
        }
    }

    void SendPacket(LoginPacket packet)
    {
        try
        {
            Tclient = new TcpClient(SERVER_IP, SERVER_PORT);
            stream = Tclient.GetStream();
            Debug.Log("Connected to server");

            clientIp = ((System.Net.IPEndPoint)Tclient.Client.LocalEndPoint).Address.ToString();
            clientPort = ((System.Net.IPEndPoint)Tclient.Client.LocalEndPoint).Port;

            packet.IP = clientIp;
            packet.Port = clientPort;
            byte[] data = packet.Packing();
            stream.Write(data, 0, data.Length);
            Debug.Log("Packet sent to server");
            RecvMessage();
            Tclient.Close();

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
            if (!islogin)
            {
                byte[] data = new byte[sizeof(bool)];
                int dataLength = stream.Read(data, 0, data.Length);
                Debug.Log(dataLength);
                if (dataLength == sizeof(bool))
                {
                    islogin = BitConverter.ToBoolean(data, 0);
                    Debug.Log(islogin);
                    if (islogin)
                    {
                        GameManager.instance.isLogin = true;
                    }
                }
            }

            else
            {
                byte[] data = new byte[512];
                int dataLength = stream.Read(data, 0, data.Length);
                Debug.Log(dataLength);

                PlayerInfo info = new PlayerInfo();

                int offset = 0;

                // IDSize 언패킹
                int idSize = BitConverter.ToInt32(data, offset);
                offset += sizeof(int);

                // ID 언패킹
                info.ID = Encoding.UTF8.GetString(data, offset, idSize);
                offset += idSize;

                // IPSize 언패킹
                int ipSize = BitConverter.ToInt32(data, offset);
                offset += sizeof(int);

                // IP 언패킹
                info.IP = Encoding.UTF8.GetString(data, offset, ipSize);
                offset += ipSize;

                PlayerInfos.Add(info);
                ismatching = true;
                ConnectToClient();
            }

        }
        catch (Exception ex)
        {
            Debug.LogError("Exception: " + ex.Message);
        }

    }

    void OnDestroy()
    {
        if (Tclient != null)
        {
            Tclient.Close();
            Debug.Log("Disconnected from server");
        }

        if(Uclient != null)
        {
            Uclient.Close();
            Debug.Log("Disconnected from client");
        }
    }

    public void IsLogin(string ID, string PW)
    {
        login.ID = ID;
        login.PW = PW;
        SendPacket(login);   
    }

    public void isMatching()
    {
        SendPacket(login);
    }
}
