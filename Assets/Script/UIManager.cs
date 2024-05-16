using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField ID;
    [SerializeField] private TMP_InputField PW;

    [SerializeField] private TMP_Text P1_ID;
    [SerializeField] private TMP_Text P2_ID;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void init(string player1, string player2)
    {
        P1_ID = GameObject.Find("UI").transform.Find("P1").transform.Find("Name").GetComponent<TMP_Text>();
        P1_ID.text = player1;

        P2_ID = GameObject.Find("UI").transform.Find("P2").transform.Find("Name").GetComponent<TMP_Text>();
        P2_ID.text = player2;
    }

    public void ButtonEvent(string name)
    {
        switch (name)
        {
            case "match":
                GameManager.instance.IsMatching();
                break;
            case "login":
                GameManager.instance.IsLoginData(ID.text, PW.text);
                break;
        }
    }

    public void Matching_AddListener(GameObject button)
    {
        Button MatchButton = button.GetComponent<Button>();
        MatchButton.onClick.AddListener(() => ButtonEvent("match"));
    }
}
