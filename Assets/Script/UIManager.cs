using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField ID;
    [SerializeField] private TMP_InputField PW;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ButtonEvent(string name)
    {
        switch (name)
        {
            case "match":
                GameManager.instance.isMatching = true;
                Debug.Log("matching");
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
