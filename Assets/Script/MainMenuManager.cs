using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    public TMP_InputField nameField;

    public TMP_Text usernameTxt;
    public GameObject profilePanel;

    public 

    // Start is called before the first frame update
    void Start()
    {
        nameField.text = UserManager.Instance.Username;
        usernameTxt.text = UserManager.Instance.Username;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnNameChange()
    {
        usernameTxt.text = nameField.text;
        UserManager.Instance.Username = usernameTxt.text;
    }

  public void OnFriendsPanelOpen()
    {

    }
}
