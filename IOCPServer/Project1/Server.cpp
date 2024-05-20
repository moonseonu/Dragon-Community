#define _CRT_SECURE_NO_WARNINGS  
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include <iostream>
#include <winsock2.h>
#include <mswsock.h>
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <mysql.h>
#include <vector>
#pragma comment(lib, "ws2_32.lib")

#define SERVER_PORT 9000
#define BUFFER_SIZE 1024
#define MAX_CLIENTS 10

#pragma pack(push, 1)
struct Packet {
    int header;
	int IDSize;
	char* ID;

	int PWSize;
	char* PW;

	int IPSize;
	char* IP;
	int Port;

    int UnPacking(int offset, const char* buffer) {
        memcpy(&IDSize, buffer + offset, sizeof(int));
        offset += sizeof(int);

        memcpy(&IDSize, buffer + offset, sizeof(int));
        offset += sizeof(int);
        ID = new char[IDSize + 1];
        memcpy(ID, buffer + offset, IDSize);
        ID[IDSize] = '\0';
        offset += IDSize;

        memcpy(&PWSize, buffer + offset, sizeof(int));
        offset += sizeof(int);
        PW = new char[PWSize + 1];
        memcpy(PW, buffer + offset, PWSize);
        PW[PWSize] = '\0';
        offset += PWSize;

        memcpy(&IPSize, buffer + offset, sizeof(int));
        offset += sizeof(int);
        IP = new char[IPSize + 1];
        memcpy(IP, buffer + offset, IPSize);
        IP[IPSize] = '\0';
        offset += IPSize;

        memcpy(&Port, buffer + offset, sizeof(int));
        offset = 0;

        return offset;
    }

    int Packing(int offset, char* buffer) {
        int length = offset + 2 * (sizeof(int)) + IDSize + IPSize;

        memcpy(buffer + offset, &length, sizeof(int));
        offset += sizeof(int);

        memcpy(buffer + offset, &IDSize, sizeof(int));
        offset += sizeof(int);

        memcpy(buffer + offset, ID, IDSize);
        offset += IDSize;

        memcpy(buffer + offset, &IPSize, sizeof(int));
        offset += sizeof(int);

        memcpy(buffer + offset, IP, IPSize);
        offset += IPSize;

        return offset;
    }
};

struct Matching {
    int IDSize;
    char* ID;

    int IPSize;
    char* IP;

    SOCKET socket;

    void IsMatchingWait(Packet client, SOCKET clientSock) {
        IDSize = client.IDSize;
        ID = new char[IDSize + 1];
        memcpy(ID, client.ID, IDSize);

        IPSize = client.IPSize;
        IP = new char[IPSize + 1];
        memcpy(IP, client.IP, IPSize);

        socket = clientSock;
    }

    int Packing(int offset, char* buffer) {
        int length = offset + 2 * (sizeof(int)) + IDSize + IPSize;

        memcpy(buffer + offset, &length, sizeof(int));
        offset += sizeof(int);

        memcpy(buffer + offset, &IDSize, sizeof(int));
        offset += sizeof(int);

        memcpy(buffer + offset, ID, IDSize);
        offset += IDSize;

        memcpy(buffer + offset, &IPSize, sizeof(int));
        offset += sizeof(int);

        memcpy(buffer + offset, IP, IPSize);
        offset += IPSize;

        return offset; 
    }
};
#pragma pack(pop)

std::vector<Matching> MatchPackets;

typedef struct {
    OVERLAPPED overlapped;
    SOCKET socket;
    char buffer[BUFFER_SIZE];
    WSABUF wsabuf;
    int operation; // 0: receive, 1: send
} IO_DATA, * LPIO_DATA;

typedef struct {
    SOCKET socket;
    SOCKADDR_IN clientAddr;
    IO_DATA ioData;
} CLIENT, * LPCLIENT;

HANDLE hCompletionPort;
CLIENT clients[MAX_CLIENTS];

DWORD WINAPI WorkerThread(LPVOID lpParam) {
    DWORD bytesTransferred;
    LPOVERLAPPED lpOverlapped;
    LPIO_DATA ioData;
    DWORD flags = 0;

    MYSQL* conn;
    MYSQL_RES* res;
    MYSQL_ROW row;

    char* server = "localhost";
    char* user = "root";
    char* password = "12341234";
    char* database = "dc";
    bool islogin = false;

    while (TRUE) {
        BOOL result = GetQueuedCompletionStatus(hCompletionPort, &bytesTransferred, (PULONG_PTR)&lpOverlapped, &lpOverlapped, INFINITE);
        if (!result || bytesTransferred == 0) {
            // Handle disconnect or error
            closesocket(((LPIO_DATA)lpOverlapped)->socket);
            continue;
        }

        ioData = (LPIO_DATA)lpOverlapped;
        if (ioData->operation == 0) { // Receive
            // Process received data
            Packet recvPack;
            int offset = 0;
            offset = recvPack.UnPacking(offset, ioData->buffer);
            conn = mysql_init(NULL);
            if (!mysql_real_connect(conn, server, user, password, database, 0, NULL, 0))
            {
                printf("connect error4\n");
                exit(1);
            }

            if (mysql_query(conn, "SELECT * FROM login"))
            {
                return 1;
            }
			res = mysql_use_result(conn);
            bool login = false;
            while ((row = mysql_fetch_row(res)) != NULL)
            {
                //로그인 안된경우
                if ((strcmp(row[1], recvPack.ID) == 0) && (strcmp(row[2], recvPack.PW) == 0)) {
                    login = true;
                    if (strcmp(row[3], "0") == 0) {
                        memcpy(ioData->buffer + offset, &login, sizeof(bool));
                        offset += sizeof(bool);
                        mysql_free_result(res);
                        char update_query[1000];
                        sprintf(update_query, "update login set Online = true where ID = '%s'", recvPack.ID);
                        if (mysql_query(conn, update_query)) {
                            printf("MySQL query failed: %s\n", mysql_error(conn));
                            return 1;
                        }

                        printf("[%s]님이 로그인하셨습니다.\n", recvPack.ID);
                        ioData->wsabuf.buf = ioData->buffer;
                        ioData->wsabuf.len = offset;
                        ioData->operation = 1; // Set to send operation
                    }

                    //로그인되어서 매칭하는 경우
                    else {
                        Matching match;
                        match.IsMatchingWait(recvPack, ioData->socket);
                        MatchPackets.push_back(match);
                        printf("[%s]님이 매칭을 시작했습니다.\n", recvPack.ID);

                        //매칭중인게 2개가 되었을 경우
                        if (MatchPackets.size() == 2) {
                            for (int i = 0; i < MatchPackets.size(); i++) {
                                if (strcmp(recvPack.ID, MatchPackets[i].ID) != 0) {
                                    for (int j = 0; j < MatchPackets.size(); j++) {
                                        if (j != i) {
                                            offset = MatchPackets[j].Packing(offset, ioData->buffer);
                                            ioData->wsabuf.buf = ioData->buffer;
                                            ioData->wsabuf.len = offset;
                                            WSASend(MatchPackets[i].socket, &(ioData->wsabuf), 1, NULL, 0, &(ioData->overlapped), NULL);
                                            printf("%s  %s 매칭을 마췄습니다. 게임을 시작합니다.\n", MatchPackets[i].ID, recvPack.ID);
                                            offset = 0;
                                        }
                                    }
                                }

                                else {
                                    for (int j = 0; j < MatchPackets.size(); j++) {
                                        if (j != i) {
                                            offset = MatchPackets[j].Packing(offset, ioData->buffer);
                                        }
                                    }
                                }
                            }
                            MatchPackets.clear();
                        }
                    }
                    break;
                }
            }
            if (!login) {
                memcpy(ioData->buffer + offset, &login, sizeof(bool));
                offset += sizeof(bool);
            }

            delete[] recvPack.ID;
            delete[] recvPack.IP;
            delete[] recvPack.PW;

            // For demonstration, just send back the same data
            
            //ioData->wsabuf.buf = ioData->buffer;
            //ioData->wsabuf.len = offset;
            //ioData->operation = 1; // Set to send operation

            // Send data back to client
            if (offset != 0)
            {
                ioData->wsabuf.buf = ioData->buffer;
                ioData->wsabuf.len = offset;
                ioData->operation = 1; // Set to send operation
                WSASend(ioData->socket, &(ioData->wsabuf), 1, NULL, 0, &(ioData->overlapped), NULL);
            }
        }
        else { // Send
            // After sending, prepare to receive more data
            ZeroMemory(&(ioData->overlapped), sizeof(OVERLAPPED));
            ioData->wsabuf.buf = ioData->buffer;
            ioData->wsabuf.len = BUFFER_SIZE;
            ioData->operation = 0; // Set to receive operation

            WSARecv(ioData->socket, &(ioData->wsabuf), 1, NULL, &flags, &(ioData->overlapped), NULL);
        }
    }

    return 0;
}

void InitializeIOCP() {
    hCompletionPort = CreateIoCompletionPort(INVALID_HANDLE_VALUE, NULL, 0, 0);

    SYSTEM_INFO systemInfo;
    GetSystemInfo(&systemInfo);
    for (DWORD i = 0; i < systemInfo.dwNumberOfProcessors; i++) {
        HANDLE hThread = CreateThread(NULL, 0, WorkerThread, NULL, 0, NULL);
        CloseHandle(hThread);
    }
}

void StartServer() {
    WSADATA wsaData;
    WSAStartup(MAKEWORD(2, 2), &wsaData);

    SOCKET listenSocket = WSASocket(AF_INET, SOCK_STREAM, 0, NULL, 0, WSA_FLAG_OVERLAPPED);
    SOCKADDR_IN serverAddr;
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_addr.s_addr = htonl(INADDR_ANY);
    serverAddr.sin_port = htons(SERVER_PORT);

    bind(listenSocket, (SOCKADDR*)&serverAddr, sizeof(serverAddr));
    listen(listenSocket, SOMAXCONN);

    InitializeIOCP();

    while (TRUE) {
        SOCKET clientSocket = accept(listenSocket, NULL, NULL);

        for (int i = 0; i < MAX_CLIENTS; i++) {
            if (clients[i].socket == INVALID_SOCKET) {
                clients[i].socket = clientSocket;
                clients[i].ioData.socket = clientSocket;
                clients[i].ioData.wsabuf.buf = clients[i].ioData.buffer;
                clients[i].ioData.wsabuf.len = BUFFER_SIZE;
                clients[i].ioData.operation = 0; // Set to receive operation

                CreateIoCompletionPort((HANDLE)clientSocket, hCompletionPort, (ULONG_PTR) & (clients[i].ioData), 0);
                DWORD flags = 0;
                WSARecv(clientSocket, &(clients[i].ioData.wsabuf), 1, NULL, &flags, &(clients[i].ioData.overlapped), NULL);
                break;
            }
        }
    }

    closesocket(listenSocket);
    WSACleanup();
}

int main() {
    for (int i = 0; i < MAX_CLIENTS; i++) {
        clients[i].socket = INVALID_SOCKET;
    }

    StartServer();
    return 0;
}