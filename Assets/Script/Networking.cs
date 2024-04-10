using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class Client : MonoBehaviour
{
    private const string SERVER_IP = "127.0.0.1"; // ���� IP �ּ�
    private const int SERVER_PORT = 9000;         // ���� ��Ʈ ��ȣ

    private TcpClient client;
    private NetworkStream stream;
    public class Packet
    {
        public string playerName = "moon";
        public int playerID = 1234;

        // Serialize �Լ�: ��Ŷ�� ����Ʈ �迭�� ����ȭ�ϴ� �޼���
        public byte[] Serialize()
        {
            byte[] playerNameBytes = Encoding.UTF8.GetBytes(playerName);
            byte[] playerIDBytes = BitConverter.GetBytes(playerID);

            // int ������ ũ�⸦ ���� �����մϴ�.
            int intSize = sizeof(int);

            byte[] data = new byte[32 + intSize]; // intSize�� �����մϴ�.
            Buffer.BlockCopy(playerNameBytes, 0, data, 0, playerNameBytes.Length);
            Buffer.BlockCopy(playerIDBytes, 0, data, 32, intSize); // intSize�� �����մϴ�.

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
        if (GameManager.instance.isMatching)
        {
            GameManager.instance.isMatching = false;
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
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send packet to server: {e.Message}");
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
}
