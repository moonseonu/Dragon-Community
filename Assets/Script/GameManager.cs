using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Start is called before the first frame update
    public static GameManager instance;
    private NetWorking networking;
    private UIManager ui;

    private enum GameState
    {
        login, main, playtable
    }
    private GameState state;
    public bool isLogin = false;
    public bool isMatching = false;
    private bool isButtonOnvalued = false;
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
        ui = GetComponent<UIManager>(); 
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
                if (networking.Islogin)
                {
                    SceneManager.LoadScene(1);
                    state = GameState.main;
                    break;
                }
                break;
            case GameState.main:
                GameObject MatchingButton = GameObject.Find("Matching Button").gameObject;
                if (MatchingButton != null && !isButtonOnvalued)
                {
                    ui.Matching_AddListener(MatchingButton);
                    isButtonOnvalued = true;
                }
                if (networking.Ismatching)
                {
                    SceneManager.LoadScene(2);
                    state = GameState.playtable;
                    break;
                }
                break;
            case GameState.playtable:
                ui.init(networking.login.ID);
                break;
        }

    }

    public void IsLoginData(string id, string pw)
    {
        networking.IsLogin(id, pw);
    }

    public void IsMatching()
    {
        networking.isMatching();
    }

    public void Update_UI(TMP_Text p1)
    {
        p1.text = networking.PlayerInfos[0].ID.ToString();
    }
}
