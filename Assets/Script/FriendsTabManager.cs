using UnityEngine;
using UnityEngine.UI;

public class FriendsTabManager : MonoBehaviour
{
    [Header("Scroll Views")]
    public GameObject friendsScrollView;
    public GameObject requestsScrollView;
    public GameObject findScrollView;

    [Header("Tab Buttons")]
    public Button friendsTabBtn;
    public Button requestsTabBtn;
    public Button findTabBtn;

    [Header("Colors")]
    public Color activeTabColor = Color.white;
    public Color inactiveTabColor = Color.gray;

    void Start()
    {
        ShowFriendsList(); // Default tab on start

        // Optional: Add listeners if not set in Inspector
        friendsTabBtn.onClick.AddListener(ShowFriendsList);
        requestsTabBtn.onClick.AddListener(ShowRequests);
        findTabBtn.onClick.AddListener(ShowFindFriends);
    }

    public void ShowFriendsList()
    {
        friendsScrollView.SetActive(true);
        requestsScrollView.SetActive(false);
        findScrollView.SetActive(false);

        SetTabColors(friendsTabBtn);
    }

    public void ShowRequests()
    {
        friendsScrollView.SetActive(false);
        requestsScrollView.SetActive(true);
        findScrollView.SetActive(false);

        SetTabColors(requestsTabBtn);
    }

    public void ShowFindFriends()
    {
        friendsScrollView.SetActive(false);
        requestsScrollView.SetActive(false);
        findScrollView.SetActive(true);

        SetTabColors(findTabBtn);
    }

    private void SetTabColors(Button activeButton)
    {
        friendsTabBtn.GetComponent<Image>().color = (activeButton == friendsTabBtn) ? activeTabColor : inactiveTabColor;
        requestsTabBtn.GetComponent<Image>().color = (activeButton == requestsTabBtn) ? activeTabColor : inactiveTabColor;
        findTabBtn.GetComponent<Image>().color = (activeButton == findTabBtn) ? activeTabColor : inactiveTabColor;
    }
}
