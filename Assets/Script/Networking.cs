using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class NetWorking : MonoBehaviour
{
    private const string SERVER_IP = "127.0.0.1"; // 서버 IP 주소
    private const int SERVER_PORT = 9000;         // 서버 포트 번호

    private TcpClient client;
    private NetworkStream stream;

    private bool islogin = false;
    private bool isSend = false;
    public class Packet
    {
        public string ID;
        public string PW;
        public int p = 1;

        // Serialize 함수: 패킷을 바이트 배열로 직렬화하는 메서드
        public byte[] Serialize()
        {
            byte[] IDBytes = Encoding.UTF8.GetBytes(ID);
            byte[] PWBytes = Encoding.UTF8.GetBytes(PW);
            byte[] IDLengthBytes = BitConverter.GetBytes(IDBytes.Length);
            byte[] PWLengthBytes = BitConverter.GetBytes(PWBytes.Length);

            int intSize = sizeof(int);

            byte[] data = new byte[IDBytes.Length + PWBytes.Length + (2 * intSize)];

            Buffer.BlockCopy(IDLengthBytes, 0, data, 0, intSize);
            Buffer.BlockCopy(IDBytes, 0, data, intSize, IDBytes.Length);
            Buffer.BlockCopy(PWLengthBytes, 0, data, intSize + IDBytes.Length, intSize);
            Buffer.BlockCopy(PWBytes, 0, data, intSize + IDBytes.Length + intSize, PWBytes.Length);
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
            byte[] data = packet.Serialize();
            stream.Write(data, 0, data.Length);

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
        byte[] buffer = new byte[100]; // 적절한 크기로 수정하세요
        int bytesRead;


        try
        {
            bytesRead = stream.Read(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log("Received message: " + message);
            isSend = false;
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
        islogin = true;
    }
}
