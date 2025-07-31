using UnityEngine;
using Photon.Pun;
using TMPro;
using Photon.Realtime;
using System.Linq;

public class PlayerUIManager : MonoBehaviourPunCallbacks
{
    public static PlayerUIManager Instance;

    [Header("Player UI Elements")]
    public PlayerUI[] playerUIs = new PlayerUI[4];

    [System.Serializable]
    public class PlayerUI
    {
        public TextMeshProUGUI usernameText;
        public TextMeshProUGUI targetBoxText;
        public TextMeshProUGUI skipTurnText;
        public int assignedActorNumber = -1;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Initialize all player UIs as inactive
        for (int i = 0; i < playerUIs.Length; i++)
        {
            SetPlayerUIVisibility(i, false);
        }

        // Initial update with delay to ensure everything is ready
        Invoke(nameof(UpdateAllPlayerDisplays), 0.5f);
    }

    public void UpdateAllPlayerDisplays()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList == null) return;

        // Clear previous assignments
        foreach (var ui in playerUIs)
        {
            ui.assignedActorNumber = -1;
        }

        // Get all players ordered by actor number
        Player[] players = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToArray();

        // Assign players to UI slots based on their order
        for (int i = 0; i < players.Length && i < playerUIs.Length; i++)
        {
            if (players[i] != null)
            {
                playerUIs[i].assignedActorNumber = players[i].ActorNumber;
                UpdatePlayerDisplay(i, players[i]);
            }
        }

        // Hide UI for unused player slots
        for (int i = players.Length; i < playerUIs.Length; i++)
        {
            SetPlayerUIVisibility(i, false);
        }
    }

    private void UpdatePlayerDisplay(int slotIndex, Player player)
    {
        if (player == null || slotIndex < 0 || slotIndex >= playerUIs.Length) return;

        var ui = playerUIs[slotIndex];
        if (ui.usernameText == null || ui.targetBoxText == null || ui.skipTurnText == null) return;

        // Show UI for this player
        SetPlayerUIVisibility(slotIndex, true);

        // Set username
        ui.usernameText.text = GetPlayerUsername(player);

        // Set target box - now properly synced through CapMovement RPCs
        ui.targetBoxText.text = $"Target: {GetPlayerTargetBox(player)}";

        // Set skip turns - now properly synced through TurnManager
        ui.skipTurnText.text = $"Skips: {GetPlayerSkipTurns(player)}";

        // Highlight current turn player
        ui.usernameText.color = IsPlayerCurrentTurn(player) ? Color.yellow : Color.white;
    }

    private bool IsPlayerCurrentTurn(Player player)
    {
        if (player == null || !PhotonNetwork.InRoom) return false;

        //if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TurnManager.TURN_KEY, out object turnObj))
        //{
        //    return player.ActorNumber == (int)turnObj;
        //}
        return false;
    }

    private void SetPlayerUIVisibility(int slotIndex, bool visible)
    {
        if (slotIndex < 0 || slotIndex >= playerUIs.Length) return;

        var ui = playerUIs[slotIndex];
        if (ui.usernameText != null) ui.usernameText.gameObject.SetActive(visible);
        if (ui.targetBoxText != null) ui.targetBoxText.gameObject.SetActive(visible);
        if (ui.skipTurnText != null) ui.skipTurnText.gameObject.SetActive(visible);
    }

    private string GetPlayerUsername(Player player)
    {
        if (player == null) return "Player";

        if (UserManager.Instance != null && UserManager.Instance.firebaseUser != null)
        {
            return UserManager.Instance.Username ?? $"Player {player.ActorNumber}";
        }
        return GenerateRandomUsername(player.ActorNumber);
    }

    private string GenerateRandomUsername(int actorNumber)
    {
        string[] adjectives = { "Swift", "Clever", "Mighty", "Brave", "Quick", "Lucky", "Fierce", "Smart" };
        string[] nouns = { "Flicker", "Slider", "Jumper", "Dasher", "Bouncer", "Glider", "Hopper", "Diver" };
        return $"{adjectives[Mathf.Abs(actorNumber) % adjectives.Length]} {nouns[Mathf.Abs(actorNumber) % nouns.Length]}";
    }

    private string GetPlayerTargetBox(Player player)
    {
        if (player == null) return "?";

        CapMovement cap = FindPlayerCap(player);
        if (cap != null)
        {
            // Use the synced values from CapMovement
            return false ? "Winner!" : $"Box {cap.currentBox}";
        }
        return "?";
    }

    private int GetPlayerSkipTurns(Player player)
    {
        if (player == null) return 0;

        // Get skip turns from TurnManager which should be synced
        //if (TurnManager.Instance != null && TurnManager.Instance.lostTurnPlayers != null)
        //{
        //    return TurnManager.Instance.lostTurnPlayers.Contains(player.ActorNumber) ? 1 : 0;
        //}
        return 0;
    }

    private CapMovement FindPlayerCap(Player player)
    {
        if (player == null) return null;

        GameObject[] caps = GameObject.FindGameObjectsWithTag("BottleCap");
        foreach (var cap in caps)
        {
            if (cap == null) continue;

            PhotonView pv = cap.GetComponent<PhotonView>();
            if (pv != null && pv.Owner != null && pv.Owner.ActorNumber == player.ActorNumber)
            {
                return cap.GetComponent<CapMovement>();
            }
        }
        return null;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        // Force a complete UI refresh when a player leaves
        UpdateAllPlayerDisplays();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        UpdateAllPlayerDisplays();
    }

    public void OnTurnChanged()
    {
        // This should be called via RPC from TurnManager
        UpdateAllPlayerDisplays();
    }

    // Call this via RPC when important changes occur
    public void ForceUIUpdate()
    {
        if (photonView.IsMine)
        {
            photonView.RPC("RPC_ForceUIUpdate", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_ForceUIUpdate()
    {
        UpdateAllPlayerDisplays();
    }
}