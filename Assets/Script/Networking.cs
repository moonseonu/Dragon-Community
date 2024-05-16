using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class NetWorking : MonoBehaviour
{
    private const string SERVER_IP = "127.0.0.1"; // 서버 IP 주소
    private const int SERVER_PORT = 9000;         // 서버 포트 번호
    private const int UDPPORT = 12345;
    private TcpClient Tclient;
    private UdpClient Uclient;
    private NetworkStream stream;

    private string clientIp;
    private int clientPort;
    private bool islogin;
    private bool ismatching;
    public bool Islogin { get { return islogin; } }
    public bool Ismatching { get { return ismatching; } }

    public class LoginPacket
    {
        public string ID;
        public string PW;
        public string IP;
        public int Port;
        public int p = 1;

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
            int offset = 0;
            byte[] data = new byte[IDBytes.Length + PWBytes.Length + IPBytes.Length + PortBytes.Length + (4 * intSize)];

            Buffer.BlockCopy(IDLengthBytes, 0, data, offset, intSize);
            offset += intSize;
            Buffer.BlockCopy(IDBytes, 0, data, offset, IDBytes.Length);
            offset += IDBytes.Length;
            Buffer.BlockCopy(PWLengthBytes, 0, data, offset, intSize);
            offset += intSize;
            Buffer.BlockCopy(PWBytes, 0, data, offset, PWBytes.Length);
            offset += PWBytes.Length;
            Buffer.BlockCopy(IPLengthBytes, 0, data, offset, intSize);
            offset += intSize;
            Buffer.BlockCopy(IPBytes, 0, data, offset, IPBytes.Length);
            offset += IPBytes.Length;
            Buffer.BlockCopy(PortLengthBytes, 0, data, offset, intSize);
            offset += intSize;
            Buffer.BlockCopy(PortBytes, 0, data, offset, PortBytes.Length);
            return data;
        }
    }
    public LoginPacket login = new LoginPacket();

    public class PlayerInfo
    {
        public int header;
        private int IDSize;
        private int IPSize;
        public string ID;
        public string IP;

        public void UnPacking(byte[] recv)
        {
            int offset = 0;
            header = BitConverter.ToInt32(recv, offset);
            offset += sizeof(int);
            // IDSize 언패킹
            IDSize = BitConverter.ToInt32(recv, offset);
            offset += sizeof(int);
            // ID 언패킹
            ID = Encoding.UTF8.GetString(recv, offset, IDSize);
            offset += IDSize;

            // IPSize 언패킹
            int ipSize = BitConverter.ToInt32(recv, offset);
            offset += sizeof(int);

            // IP 언패킹
            IP = Encoding.UTF8.GetString(recv, offset, ipSize);
        }
    }

    public List<PlayerInfo> PlayerInfos = new List<PlayerInfo>();

    void Start()
    {
        // 비동기 접속 시도
        //ConnectToServerAsync();
    }

    async void ConnectToServerAsync()
    {
        try
        {
            Tclient = new TcpClient();
            await Tclient.ConnectAsync(SERVER_IP, SERVER_PORT);

            clientIp = ((IPEndPoint)Tclient.Client.LocalEndPoint).Address.ToString();
            clientPort = ((IPEndPoint)Tclient.Client.LocalEndPoint).Port;

            stream = Tclient.GetStream();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
        }
    }

    async Task ConnectToClientAsync()
    {
        try
        {
            Uclient = new UdpClient();
            IPAddress serverAddress = IPAddress.Parse(SERVER_IP);
            IPEndPoint endPoint = new IPEndPoint(serverAddress, SERVER_PORT);

            // 연결된 서버로 메시지 보내기 (예: "Hello, server!")
            string message = "Hello, server!";
            byte[] data = Encoding.UTF8.GetBytes(message);
            await Uclient.SendAsync(data, data.Length, endPoint);

            UdpReceiveResult result = await Uclient.ReceiveAsync();
            string response = Encoding.UTF8.GetString(result.Buffer);
            Debug.Log("Received from server: " + response);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to client: {e.Message}");
        }
    }

    async void SendPacketAsync(LoginPacket packet)
    {
        try
        {
            if (Tclient == null || !Tclient.Connected)
            {
                Tclient = new TcpClient();
                await Tclient.ConnectAsync(SERVER_IP, SERVER_PORT);
                stream = Tclient.GetStream();

                clientIp = ((IPEndPoint)Tclient.Client.LocalEndPoint).Address.ToString();
                clientPort = ((IPEndPoint)Tclient.Client.LocalEndPoint).Port;
            }

            packet.IP = clientIp;
            packet.Port = clientPort;
            byte[] sdata = packet.Packing();
            await stream.WriteAsync(sdata, 0, sdata.Length);
            int st = BitConverter.ToInt32(sdata, 0);
            string ss = Encoding.UTF8.GetString(sdata, sizeof(int), 3);
            Debug.Log("Packet sent to server");

            await RecvMessageAsync();

            //Tclient.Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send packet to server: {e.Message}");
        }
    }

    async Task RecvMessageAsync()
    {
        try
        {
            if (stream == null)
            {
                Debug.Log("stream is null");
                return;
            }
                

            if (!islogin)
            {
                byte[] rdata = new byte[sizeof(bool)];
                int dataLength = await stream.ReadAsync(rdata, 0, sizeof(bool));
                Debug.Log(dataLength);
                if (dataLength == sizeof(bool))
                {
                    islogin = BitConverter.ToBoolean(rdata, 0);
                    if (islogin)
                    {
                        GameManager.instance.isLogin = true;
                    }
                }
            }
            else
            {
                byte[] header = new byte[sizeof(int)];
                int dataLength = await stream.ReadAsync(header, 0, sizeof(int));
                int Header = BitConverter.ToInt32(header, 0);
                Debug.Log(Header);
                PlayerInfo info = new PlayerInfo();

                if (info.header != 0)
                {
                    PlayerInfos.Add(info);
                    ismatching = true;
                    await ConnectToClientAsync();
                }
                else
                {
                    Debug.Log("header is " + info.header);
                }
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

        if (Uclient != null)
        {
            Uclient.Close();
            Debug.Log("Disconnected from client");
        }
    }

    public void IsLogin(string ID, string PW)
    {
        login.ID = ID;
        login.PW = PW;
        SendPacketAsync(login);
    }

    public void isMatching()
    {
        SendPacketAsync(login);
    }
}