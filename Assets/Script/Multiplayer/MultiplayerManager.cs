using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public enum GameMode { OneVsOne, FreeForAll, TeamTwoVsTwo }

public class MultiplayerManager : MonoBehaviourPunCallbacks
{
    public static MultiplayerManager Instance;
    public GameMode selectedMode;

    [Header("UI References")]
    public GameObject loadingPanel;
    public GameObject menuPanel;
    public Text connectionStatusText;

    private bool isConnecting;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ConnectToPhoton();
    }

    public void ConnectToPhoton()
    {
        connectionStatusText.text = "Connecting to server...";
        isConnecting = true;

        if (PhotonNetwork.IsConnected)
        {
            OnConnectedToMaster();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        connectionStatusText.text = "Connected to server";
        isConnecting = false;
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        connectionStatusText.text = "Ready to play";
        menuPanel.SetActive(true);
    }

    public void SelectOneVsOne()
    {
        selectedMode = GameMode.OneVsOne;
        StartMatchmaking();
    }

    public void SelectFreeForAll()
    {
        selectedMode = GameMode.FreeForAll;
        StartMatchmaking();
    }

    public void SelectTeamTwoVsTwo()
    {
        selectedMode = GameMode.TeamTwoVsTwo;
        StartMatchmaking();
    }

    private void StartMatchmaking()
    {
        if (!PhotonNetwork.IsConnected)
        {
            connectionStatusText.text = "Not connected to server";
            ConnectToPhoton();
            return;
        }

        menuPanel.SetActive(false);
        loadingPanel.SetActive(true);
        connectionStatusText.text = "Finding match...";

        string modeString = selectedMode switch
        {
            GameMode.OneVsOne => "1v1",
            GameMode.TeamTwoVsTwo => "2v2",
            _ => "ffa"
        };

        var expectedProperties = new Hashtable { { "mode", modeString } };
        PhotonNetwork.JoinRandomRoom(expectedProperties, 0);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        connectionStatusText.text = "Creating new room...";
        CreateRoom();
    }

    void CreateRoom()
    {
        string modeString = selectedMode switch
        {
            GameMode.OneVsOne => "1v1",
            GameMode.TeamTwoVsTwo => "2v2",
            _ => "ffa"
        };

        byte maxPlayers = selectedMode switch
        {
            GameMode.OneVsOne => (byte)2,
            GameMode.TeamTwoVsTwo => (byte)4,
            GameMode.FreeForAll => (byte)3,
            _ => (byte)3
        };

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            CustomRoomProperties = new Hashtable { { "mode", modeString } },
            CustomRoomPropertiesForLobby = new string[] { "mode" }
        };

        string roomName = "Room_" + Random.Range(1000, 9999);
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public override void OnJoinedRoom()
    {
        connectionStatusText.text = "Loading game...";
        SceneManager.LoadScene("Game");
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("MenuScene");
    }

    public void LeaveGame()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        connectionStatusText.text = $"Disconnected: {cause}";
        menuPanel.SetActive(true);
        loadingPanel.SetActive(false);
    }
}