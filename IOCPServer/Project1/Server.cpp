#define _CRT_SECURE_NO_WARNINGS  
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#pragma comment(lib, "ws2_32")
using namespace std;
#include <winsock2.h>
#include <stdlib.h>
#include <stdio.h>
#include <vector>
#include <mysql.h>
#include <string>
#include <iostream>
#include <conio.h>

#pragma pack(push, 1)
struct Packet {
	int IDSize;
	char* ID;

	int PWSize;
	char* PW;

	int IPSize;
	char* IP;
	int Port;
};
#pragma pack(pop)
#pragma pack(push, 1)
struct MatchPacket {
	int IDSize;
	char* ID;

	int IPSize;
	char* IP;
};
#pragma pack(pop)

#define SERVERPORT 9000
#define BUFSIZE    512

vector<MatchPacket> Packets;

// 소켓 정보 저장을 위한 구조체
struct SOCKETINFO
{
	OVERLAPPED overlapped;
	SOCKET sock;
	char buf[BUFSIZE + 1];
	int recvbytes;
	int sendbytes;
	WSABUF wsabuf;
};

// 작업자 스레드 함수
DWORD WINAPI WorkerThread(LPVOID arg);
// 오류 출력 함수
void err_quit(char* msg);
void err_display(char* msg);

int main(int argc, char* argv[])
{
	MYSQL* conn;
	MYSQL_RES* res;
	MYSQL_ROW row;

	char* server = "localhost";
	char* user = "root";
	char* password = "12341234";
	char* database = "dc";

	conn = mysql_init(NULL);

	if (!mysql_real_connect(conn, server, user, password, database, 0, NULL, 0))
	{
		exit(1);
	}
	if (mysql_query(conn, "show tables"))
	{
		exit(1);
	}

	res = mysql_use_result(conn);
	printf("MYSQL Tables in mysql database : \n");
	while ((row = mysql_fetch_row(res)) != NULL)
		printf("%s \n", row[0]);


	if (mysql_query(conn, "SELECT * FROM login"))
	{
		return 1;
	}

	res = mysql_use_result(conn);

	printf("Returning List of Names : \n");
	while ((row = mysql_fetch_row(res)) != NULL)
	{
		printf("%s %s %s %s\n", row[0], row[1], row[2], row[3]);
		if (row[3] != NULL && strcmp(row[3], "0") == 0) {
			printf("Fdsa");
		}

		printf("\n");
	}

	int retval;

	// 윈속 초기화
	WSADATA wsa;
	if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) return 1;

	// 입출력 완료 포트 생성
	HANDLE hcp = CreateIoCompletionPort(INVALID_HANDLE_VALUE, NULL, 0, 0);
	if (hcp == NULL) return 1;

	// CPU 개수 확인
	SYSTEM_INFO si;
	GetSystemInfo(&si);

	// (CPU 개수 * 2)개의 작업자 스레드 생성
	HANDLE hThread;
	for (int i = 0; i < (int)si.dwNumberOfProcessors * 2; i++) {
		hThread = CreateThread(NULL, 0, WorkerThread, hcp, 0, NULL);
		if (hThread == NULL) return 1;
		CloseHandle(hThread);
	}

	// socket()
	SOCKET listen_sock = socket(AF_INET, SOCK_STREAM, 0);
	if (listen_sock == INVALID_SOCKET) err_quit("socket()");

	// bind()
	SOCKADDR_IN serveraddr;
	ZeroMemory(&serveraddr, sizeof(serveraddr));
	serveraddr.sin_family = AF_INET;
	serveraddr.sin_addr.s_addr = htonl(INADDR_ANY);
	serveraddr.sin_port = htons(SERVERPORT);
	retval = bind(listen_sock, (SOCKADDR*)&serveraddr, sizeof(serveraddr));
	if (retval == SOCKET_ERROR) err_quit("bind()");

	// listen()
	retval = listen(listen_sock, SOMAXCONN);
	if (retval == SOCKET_ERROR) err_quit("listen()");

	// 데이터 통신에 사용할 변수
	SOCKET client_sock;
	SOCKADDR_IN clientaddr;
	int addrlen;
	DWORD recvbytes, flags;

	while (1) {
		// accept()
		addrlen = sizeof(clientaddr);
		client_sock = accept(listen_sock, (SOCKADDR*)&clientaddr, &addrlen);
		if (client_sock == INVALID_SOCKET) {
			err_display("accept()");
			break;
		}
		printf("[TCP 서버] 클라이언트 접속: IP 주소=%s, 포트 번호=%d\n",
			inet_ntoa(clientaddr.sin_addr), ntohs(clientaddr.sin_port));

		// 소켓과 입출력 완료 포트 연결
		CreateIoCompletionPort((HANDLE)client_sock, hcp, client_sock, 0);

		// 소켓 정보 구조체 할당
		SOCKETINFO* ptr = new SOCKETINFO;
		if (ptr == NULL) break;
		ZeroMemory(&ptr->overlapped, sizeof(ptr->overlapped));
		ptr->sock = client_sock;
		ptr->recvbytes = ptr->sendbytes = 0;
		ptr->wsabuf.buf = ptr->buf;
		ptr->wsabuf.len = BUFSIZE;

		// 비동기 입출력 시작
		flags = 0;
		retval = WSARecv(client_sock, &ptr->wsabuf, 1, &recvbytes,
			&flags, &ptr->overlapped, NULL);
		if (retval == SOCKET_ERROR) {
			if (WSAGetLastError() != ERROR_IO_PENDING) {
				err_display("WSARecv()");
			}
			continue;
		}
	}
	mysql_free_result(res);
	mysql_close(conn);
	// 윈속 종료
	WSACleanup();
	return 0;
}

// 작업자 스레드 함수
DWORD WINAPI WorkerThread(LPVOID arg)
{
	int retval;
	HANDLE hcp = (HANDLE)arg;
	MYSQL* conn;
	MYSQL_RES* res;
	MYSQL_ROW row;

	char* server = "localhost";
	char* user = "root";
	char* password = "12341234";
	char* database = "dc";
	bool islogin = false;

	int x = 0;

	conn = mysql_init(NULL);

	if (!mysql_real_connect(conn, server, user, password, database, 0, NULL, 0))
	{
		exit(1);
	}

	while (1) {
		// 비동기 입출력 완료 기다리기
		DWORD cbTransferred;
		SOCKET client_sock;
		SOCKETINFO* ptr;
		retval = GetQueuedCompletionStatus(hcp, &cbTransferred,
			(PULONG_PTR)&client_sock, (LPOVERLAPPED*)&ptr, INFINITE);

		// 클라이언트 정보 얻기
		SOCKADDR_IN clientaddr;
		int addrlen = sizeof(clientaddr);
		getpeername(ptr->sock, (SOCKADDR*)&clientaddr, &addrlen);

		// 비동기 입출력 결과 확인
		if (retval == 0 || cbTransferred == 0) {
			if (retval == 0) {
				DWORD temp1, temp2;
				WSAGetOverlappedResult(ptr->sock, &ptr->overlapped,
					&temp1, FALSE, &temp2);
				err_display("WSAGetOverlappedResult()");
			}
			closesocket(ptr->sock);
			printf("[TCP 서버] 클라이언트 종료: IP 주소=%s, 포트 번호=%d\n",
				inet_ntoa(clientaddr.sin_addr), ntohs(clientaddr.sin_port));
			delete ptr;
			continue;
		}

		// 데이터 전송량 갱신
		if (ptr->recvbytes == 0) {
			ptr->recvbytes = cbTransferred;
			ptr->sendbytes = 0;
			ptr->buf[ptr->recvbytes] = '\0';

			Packet recvPack;
			int offset = 0;

			memcpy(&recvPack.IDSize, ptr->buf + offset, sizeof(int));
			offset += sizeof(int);
			recvPack.ID = new char[recvPack.IDSize + 1];

			memcpy(recvPack.ID, ptr->buf + offset, recvPack.IDSize);
			recvPack.ID[recvPack.IDSize] = '\0';
			offset += recvPack.IDSize;

			memcpy(&recvPack.PWSize, ptr->buf + offset, sizeof(int));
			offset += sizeof(int);
			recvPack.PW = new char[recvPack.PWSize + 1];

			memcpy(recvPack.PW, ptr->buf + offset, recvPack.PWSize);
			recvPack.PW[recvPack.PWSize] = '\0';
			offset += recvPack.PWSize;

			memcpy(&recvPack.IPSize, ptr->buf + offset, sizeof(int));
			offset += sizeof(int);
			recvPack.IP = new char[recvPack.IPSize + 1];

			memcpy(recvPack.IP, ptr->buf + offset, recvPack.IPSize);
			recvPack.IP[recvPack.IPSize] = '\0';
			offset += recvPack.IPSize;

			memcpy(&recvPack.Port, ptr->buf + offset, sizeof(int));
			offset += sizeof(int);

			memset(ptr->buf, 0, sizeof(ptr->buf));

			if (mysql_query(conn, "SELECT * FROM login"))
			{
				return 1;
			}
			res = mysql_use_result(conn);
			bool login = false;
			while ((row = mysql_fetch_row(res)) != NULL)
			{
				if ((strcmp(row[1], recvPack.ID) == 0) && (strcmp(row[2], recvPack.PW) == 0)) {
					printf("%d mooonmoonmoon\n", atoi(row[3]));
					if (strcmp(row[3], "0") == 0) {
						login = true;
						memcpy(ptr->buf, &login, sizeof(bool));
						mysql_free_result(res);
						char update_query[1000];
						sprintf(update_query, "update login set Online = true where ID = '%s'", recvPack.ID);
						if (mysql_query(conn, update_query)) {
							printf("MySQL query failed: %s\n", mysql_error(conn));
							return 1;
						}
					}

					else {
						MatchPacket mp;
						mp.IDSize = recvPack.IDSize;
						mp.ID = new char[recvPack.IDSize + 1];
						strcpy(mp.ID, recvPack.ID);

						mp.IPSize = recvPack.IPSize;
						mp.IP = new char[recvPack.IPSize + 1];
						strcpy(mp.IP, recvPack.IP);

						Packets.push_back(mp);
						if (Packets.size() == 1)
						{
							int offset = 0;
							memcpy(ptr->buf + offset, &Packets[0].IDSize, sizeof(int));
							offset += sizeof(int);
							memcpy(ptr->buf + offset, Packets[0].ID, Packets[0].IDSize);
							offset += Packets[0].IDSize;
							memcpy(ptr->buf + offset, &Packets[0].IPSize, sizeof(int));
							offset += sizeof(int);
							memcpy(ptr->buf + offset, Packets[0].IP, Packets[0].IPSize);
							offset += Packets[0].IPSize;

							printf("%d\n", offset);
							delete[] Packets[0].ID;
							delete[] Packets[0].IP;

							//for (int i = 0; i < Packets.size(); i++) {
							//	if (strcmp(Packets[i].ID, mp.ID) != 0) {
							//		printf("%s", Packets[i].ID);
							//		memcpy(ptr->buf + offset, &Packets[i].IDSize, sizeof(int));
							//		offset += sizeof(int);
							//		memcpy(ptr->buf + offset, Packets[i].ID, Packets[i].IDSize);
							//		offset += Packets[i].IDSize;
							//		memcpy(ptr->buf + offset, &Packets[i].IPSize, sizeof(int));
							//		offset += sizeof(int);
							//		memcpy(ptr->buf + offset, Packets[i].IP, Packets[i].IPSize);
							//		offset += Packets[i].IPSize;

							//		printf("%d\n", offset);
							//	}
							//	delete[] Packets[i].ID;
							//	delete[] Packets[i].IP;
							//}

							Packets.clear();
						}
					}

					break;
				}

				else {
					login = false;
					memcpy(ptr->buf, &login, sizeof(bool));
				}
			}

			delete[] recvPack.ID;
			delete[] recvPack.IP;
			delete[] recvPack.PW;
		}
		else {
			ptr->sendbytes += cbTransferred;
		}

		if (ptr->recvbytes > ptr->sendbytes) {
			// 데이터 보내기
			ZeroMemory(&ptr->overlapped, sizeof(ptr->overlapped));
			ptr->wsabuf.buf = ptr->buf + ptr->sendbytes;
			ptr->wsabuf.len = ptr->recvbytes - ptr->sendbytes;
			DWORD sendbytes;
			retval = WSASend(ptr->sock, &ptr->wsabuf, 1,
				&sendbytes, 0, &ptr->overlapped, NULL);
			if (retval == SOCKET_ERROR) {
				if (WSAGetLastError() != WSA_IO_PENDING) {
					err_display("WSASend()");
				}
				continue;
			}
		}
		else {
			ptr->recvbytes = 0;
			// 데이터 받기
			ZeroMemory(&ptr->overlapped, sizeof(ptr->overlapped));
			ptr->wsabuf.buf = ptr->buf;
			ptr->wsabuf.len = BUFSIZE;
			DWORD recvbytes;
			DWORD flags = 0;
			retval = WSARecv(ptr->sock, &ptr->wsabuf, 1,
				&recvbytes, &flags, &ptr->overlapped, NULL);
			if (retval == SOCKET_ERROR) {
				if (WSAGetLastError() != WSA_IO_PENDING) {
					err_display("WSARecv()");
				}
				continue;
			}
		}
	}
	mysql_free_result(res);
	mysql_close(conn);
	return 0;
}

// 소켓 함수 오류 출력 후 종료
void err_quit(char* msg)
{
	LPVOID lpMsgBuf;
	FormatMessage(
		FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
		NULL, WSAGetLastError(),
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
		(LPTSTR)&lpMsgBuf, 0, NULL);
	MessageBox(NULL, (LPCTSTR)lpMsgBuf, msg, MB_ICONERROR);
	LocalFree(lpMsgBuf);
	exit(1);
}

// 소켓 함수 오류 출력
void err_display(char* msg)
{
	LPVOID lpMsgBuf;
	FormatMessage(
		FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
		NULL, WSAGetLastError(),
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
		(LPTSTR)&lpMsgBuf, 0, NULL);
	printf("[%s] %s", msg, (char*)lpMsgBuf);
	LocalFree(lpMsgBuf);
}
