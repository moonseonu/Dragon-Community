using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Start is called before the first frame update
    public static GameManager instance;
    private NetWorking networking;
    private enum GameState
    {
        login, main, start
    }
    private GameState state;
    public bool isLogin = false;
    public bool isMatching = false;
    private void Awake()
    {
        if (instance != null) Destroy(gameObject);
        instance = this;
        DontDestroyOnLoad(gameObject);

        init();
    }

    private void init()
    {
        networking = GetComponent<NetWorking>();
        state = GameState.login;   
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        switch (state)
        {
            case GameState.login:
                break;
            case GameState.main:
                break;
            case GameState.start:
                break;
        }
    }

    public void IsLoginData(string id, string pw)
    {
        networking.IsLogin(id, pw);
    }
}
