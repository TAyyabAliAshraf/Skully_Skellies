using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;
    public GameObject board;
    public GameObject bottleCapPrefab;
    public Transform[] spawnPoints; // Made public for CapMovement access

    private Dictionary<int, bool> spawnedPlayers = new Dictionary<int, bool>();
    private PhotonView photonView;
    private bool gameActive = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            photonView = GetComponent<PhotonView>();
            if (photonView == null)
            {
                Debug.LogError("PhotonView component missing on GameManager!");
            }
            else
            {
                Debug.Log($"GameManager initialized with PhotonView ID: {photonView.ViewID}");
            }
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            InitializeGame();
        }
    }

    bool spawned = false;
    void Update()
    {
        if (spawned)
        {
            return;
        }
        // Only act if game is active and it's the local player's turn
        if (gameActive && TurnManager.Instance.IsMyTurn())
        {
            spawned = true;
            TrySpawnPlayerCap();
        }
    }

    private void InitializeGame()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        // Initialize turn system
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.StartTurnSystem();
        }

        CheckGameStart();
    }

    private void CheckGameStart()
    {
        if (gameActive) return;

        bool enoughPlayers = PhotonNetwork.CurrentRoom.PlayerCount >= 2;
        if (MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
        {
            enoughPlayers = PhotonNetwork.CurrentRoom.PlayerCount == 4;
        }

        if (enoughPlayers)
        {
            photonView.RPC("RPC_StartGame", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_StartGame()
    {
        gameActive = true;
        board.SetActive(true);

        // Reset spawnedPlayers to ensure fresh spawning for all players
        spawnedPlayers.Clear();
        Debug.Log("Game started, spawnedPlayers dictionary cleared.");

        // Master client ensures the first player's cap is spawned
        if (PhotonNetwork.IsMasterClient)
        {
            TrySpawnPlayerCap();
        }
    }

    [PunRPC]
    private void SpawnCapForPlayer(int actorNumber)
    {
        Debug.Log($"Spawn request for player {actorNumber} (Local actor: {PhotonNetwork.LocalPlayer?.ActorNumber})");

        // Only spawn for the matching player
        if (PhotonNetwork.LocalPlayer == null)
        {
            Debug.LogError("Local player reference is null!");
            return;
        }

        if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber)
        {
            Debug.Log($"Not spawning - I'm player {PhotonNetwork.LocalPlayer.ActorNumber}, not {actorNumber}");
            return;
        }

        // Validate spawn points
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("No spawn points configured!");
            return;
        }

        // Choose spawn point based on game mode and player
        int spawnIndex = 0;
        if (MultiplayerManager.Instance.selectedMode == GameMode.OneVsOne)
        {
            // In 1v1, use first two spawn points based on ActorNumber
            spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber % 2;
        }
        else if (MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
        {
            // In 2v2, use team-based spawn points
            int team = TurnManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            spawnIndex = (team - 1) * 2 + (PhotonNetwork.LocalPlayer.ActorNumber % 2);
        }
        else
        {
            // In FreeForAll, use ActorNumber modulo number of spawn points
            spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber % spawnPoints.Length;
        }

        // Ensure spawnIndex is valid
        if (spawnIndex >= spawnPoints.Length)
        {
            Debug.LogError($"Invalid spawn index {spawnIndex} for player {actorNumber}. Using index 0.");
            spawnIndex = 0;
        }

        // Actually spawn the cap
        GameObject newCap = PhotonNetwork.Instantiate(
            "BottleCap",
            spawnPoints[spawnIndex].position,
            Quaternion.identity,
            0,
            new object[] { actorNumber }
        );
        Debug.Log($"Successfully spawned cap for player {actorNumber} at spawn point {spawnIndex}");
    }

    public void TrySpawnPlayerCap()
    {
        if (!gameActive) return;

        if (TryGetCurrentTurn(out int currentTurn))
        {
            // Only spawn if the current turn matches the local player and they haven't spawned yet
            if (!spawnedPlayers.ContainsKey(currentTurn) && PhotonNetwork.LocalPlayer.ActorNumber == currentTurn)
            {
                spawnedPlayers[currentTurn] = true;
                photonView.RPC("SpawnCapForPlayer", RpcTarget.All, currentTurn);
                Debug.Log($"Spawning cap for player {currentTurn}");
            }
        }
    }

    private bool TryGetCurrentTurn(out int currentTurn)
    {
        currentTurn = -1;
        if (PhotonNetwork.CurrentRoom == null) return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TurnManager.TURN_KEY, out object turnObj))
        {
            currentTurn = (int)turnObj;
            return true;
        }
        return false;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            CheckGameStart();
        }
    }


    [PunRPC]
    private void RPC_ApplyTurnPenalty(int actorNumber, int penaltyTurns)
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogError("RPC_ApplyTurnPenalty: TurnManager.Instance is null");
            return;
        }
        TurnManager.Instance.playerTurnPenalties[actorNumber] = penaltyTurns;
        Debug.Log($"GameManager: Synchronized penalty of {penaltyTurns} turns for player {actorNumber}");
    }
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient)
        {
            // Clear spawnedPlayers entry for the player who left
            if (spawnedPlayers.ContainsKey(otherPlayer.ActorNumber))
            {
                spawnedPlayers.Remove(otherPlayer.ActorNumber);
                Debug.Log($"Removed player {otherPlayer.ActorNumber} from spawnedPlayers.");
            }
        }
    }

    [PunRPC]
    private void RPC_EndTurn(int actorNumber)
    {
        Debug.Log($"RPC_EndTurn called for player {actorNumber} on GameManager PhotonView ID: {photonView.ViewID}");
        if (PhotonNetwork.IsMasterClient)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TurnManager.TURN_KEY, out object turnObj) && (int)turnObj == actorNumber)
            {
                TurnManager.Instance.EndTurn();
            }
            else
            {
                Debug.LogWarning($"Player {actorNumber} tried to end turn but it's not their turn.");
            }
        }
    }

    public void ResetSpawnedPlayers()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            spawnedPlayers.Clear();
            Debug.Log("Reset spawnedPlayers for new turn cycle.");
            photonView.RPC("RPC_ResetSpawnedPlayers", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_ResetSpawnedPlayers()
    {
        spawnedPlayers.Clear();
        Debug.Log("spawnedPlayers cleared on all clients.");
    }
}