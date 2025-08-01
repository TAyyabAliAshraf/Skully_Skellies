using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using ExitGames.Client.Photon;
using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviourPunCallbacks
{
    public static TurnManager Instance; 
    public const string TURN_KEY = "TurnPlayer";
    public const string TEAM_KEY = "PlayerTeam";
    public Dictionary<int, int> playerTurnPenalties = new Dictionary<int, int>(); // Tracks players and their turn penalty count

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

    public int GetPlayerTeam(Player player)
    {
        if (player == null)
        {
            Debug.LogError("GetPlayerTeam: Player is null");
            return 0;
        }

        if (player.CustomProperties.TryGetValue(TEAM_KEY, out object teamObj))
        {
            return (int)teamObj;
        }
        return player.ActorNumber % 2 == 1 ? 1 : 2;
    }

    public void StartTurnSystem()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogError("StartTurnSystem: CurrentRoom is null");
            return;
        }

        if (MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
        {
            AssignTeams();
        }

        // Initialize first turn if not set
        if (PhotonNetwork.IsMasterClient && !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(TURN_KEY))
        {
            var firstPlayer = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).FirstOrDefault();
            if (firstPlayer != null)
            {
                SetTurn(firstPlayer.ActorNumber);
                Debug.Log($"Initialized turn to player {firstPlayer.ActorNumber}");
            }
            else
            {
                Debug.LogError("No players available to start turn system!");
            }
        }
    }

    private void AssignTeams()
    {
        var players = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            int team = (players[i].ActorNumber % 2 == 1) ? 1 : 2;
            Hashtable props = new Hashtable { { TEAM_KEY, team } };
            players[i].SetCustomProperties(props);
            Debug.Log($"Assigned player {players[i].ActorNumber} to team {team}");
        }
    }

    public bool IsMyTurn()
    {
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
        {
            Debug.LogWarning("IsMyTurn: CurrentRoom or LocalPlayer is null");
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj))
        {
            return PhotonNetwork.LocalPlayer.ActorNumber == (int)turnObj;
        }
        return false;
    }

    private void SetTurn(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("SetTurn: Not master client or CurrentRoom is null");
            return;
        }

        Debug.Log($"Setting turn to player {actorNumber} (Current players: {string.Join(",", PhotonNetwork.PlayerList.Select(p => p.ActorNumber))})");

        // Verify player exists
        if (!PhotonNetwork.PlayerList.Any(p => p.ActorNumber == actorNumber))
        {
            Debug.LogError($"Player {actorNumber} not found in room! Falling back to first player.");
            var firstPlayer = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).FirstOrDefault();
            if (firstPlayer != null)
            {
                actorNumber = firstPlayer.ActorNumber;
            }
            else
            {
                Debug.LogError("No players available to set turn!");
                return;
            }
        }

        Hashtable hash = new Hashtable { { TURN_KEY, actorNumber } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(hash);
    }

    public void EndTurn()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("EndTurn: Only master client can end turn");
            return;
        }

        Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} ending turn");

        int nextTurn = GetNextPlayer();
        if (nextTurn != -1)
        {
            // Check if we're returning to the first player (end of turn cycle)
            var players = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToList();
            int currentTurn = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj) ? (int)turnObj : -1;
            int currentIndex = players.FindIndex(p => p.ActorNumber == currentTurn);
            int nextIndex = players.FindIndex(p => p.ActorNumber == nextTurn);

            if (nextIndex <= currentIndex && players.Count > 1)
            {
                GameManager.Instance.ResetSpawnedPlayers();
                Debug.Log("Full turn cycle completed, resetting spawnedPlayers.");
            }

            SetTurn(nextTurn);
        }
        else
        {
            Debug.LogError("No next player found! Game may be in an invalid state.");
        }
    }

    private int GetNextPlayer()
    {
        var players = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToList();
        if (players.Count == 0)
        {
            Debug.LogError("GetNextPlayer: No players in the room!");
            return -1;
        }

        // If no current turn is set, start with the first player
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object currentTurnObj))
        {
            Debug.Log("No current turn set, starting with first player.");
            return players[0].ActorNumber;
        }

        int currentTurn = (int)currentTurnObj;
        int currentIndex = players.FindIndex(p => p.ActorNumber == currentTurn);

        if (currentIndex == -1)
        {
            Debug.LogWarning($"Current turn player {currentTurn} not found! Falling back to first player.");
            return players[0].ActorNumber;
        }

        // Find the next player, skipping those with remaining penalties
        int nextIndex = currentIndex;
        int attempts = 0;
        do
        {
            nextIndex = (nextIndex + 1) % players.Count;
            int nextPlayer = players[nextIndex].ActorNumber;
            attempts++;

            // Check if player has a penalty
            if (!playerTurnPenalties.ContainsKey(nextPlayer) || playerTurnPenalties[nextPlayer] == 0)
            {
                Debug.Log($"GetNextPlayer: Current turn: {currentTurn}, Next turn: {nextPlayer}");
                return nextPlayer;
            }
            else
            {
                // Decrement penalty
                playerTurnPenalties[nextPlayer]--;
                Debug.Log($"Skipping player {nextPlayer} (remaining penalty: {playerTurnPenalties[nextPlayer]} turns)");
                if (playerTurnPenalties[nextPlayer] == 0)
                {
                    playerTurnPenalties.Remove(nextPlayer);
                    Debug.Log($"Player {nextPlayer} penalty cleared");
                }
            }
        } while (attempts < players.Count * 2); // Prevent infinite loops

        // If all players have penalties, reduce all penalties by 1 and try again
        Debug.LogWarning("All players have penalties. Reducing all penalties by 1 and retrying.");
        foreach (var player in playerTurnPenalties.Keys.ToList())
        {
            playerTurnPenalties[player]--;
            if (playerTurnPenalties[player] == 0)
            {
                playerTurnPenalties.Remove(player);
            }
        }
        return players[0].ActorNumber; // Fallback to first player
    }

    public void ApplyTurnPenalty(int actorNumber, int penaltyTurns)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("ApplyTurnPenalty: Only master client can apply penalties");
            return;
        }

        if (penaltyTurns <= 0)
        {
            Debug.LogWarning($"Invalid penalty turns {penaltyTurns} for player {actorNumber}. Ignoring.");
            return;
        }

        playerTurnPenalties[actorNumber] = penaltyTurns;
        Debug.Log($"ApplyTurnPenalty: Player {actorNumber} assigned penalty of {penaltyTurns} turns.");

        // Synchronize penalty across all clients
        if (GameManager.Instance != null && GameManager.Instance.photonView != null)
        {
            GameManager.Instance.photonView.RPC("RPC_ApplyTurnPenalty", RpcTarget.All, actorNumber, penaltyTurns);
        }
        else
        {
            Debug.LogError("ApplyTurnPenalty: GameManager or its PhotonView is null");
        }

        // If the penalized player is the current turn, advance to the next player
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj) && (int)turnObj == actorNumber)
        {
            Debug.Log($"Player {actorNumber} penalized during their turn. Scheduling turn advancement.");
            Invoke("EndTurn", 0.1f); // Delay to ensure penalty syncs
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient)
        {
            // Remove from playerTurnPenalties if player leaves
            playerTurnPenalties.Remove(otherPlayer.ActorNumber);
            Debug.Log($"Player {otherPlayer.ActorNumber} left, removed from penalties");
            // If the current turn player left, advance to the next player
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj) && (int)turnObj == otherPlayer.ActorNumber)
            {
                Debug.Log($"Player {otherPlayer.ActorNumber} left during their turn. Advancing turn.");
                EndTurn();
            }
        }
    }

}