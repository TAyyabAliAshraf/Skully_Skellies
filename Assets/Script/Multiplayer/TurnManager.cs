using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using ExitGames.Client.Photon;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{
    public static TurnManager Instance;
    public const string TURN_KEY = "TurnPlayer";
    public const string TEAM_KEY = "PlayerTeam";

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
        if (player == null) return 0;

        if (player.CustomProperties.TryGetValue(TEAM_KEY, out object teamObj))
        {
            return (int)teamObj;
        }
        return player.ActorNumber % 2 == 1 ? 1 : 2;
    }

    public void StartTurnSystem()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

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
                SetTurn(firstPlayer.ActorNumber); // Set turn to first player
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
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj))
        {
            return PhotonNetwork.LocalPlayer.ActorNumber == (int)turnObj;
        }
        return false;
    }

    private void SetTurn(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;

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
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} ending turn");

        int nextTurn = GetNextPlayer();
        if (nextTurn != -1)
        {
            // Check if we're returning to the first player (end of turn cycle)
            var players = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToList();
            int currentTurn = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj) ? (int)turnObj : -1;
            int currentIndex = players.FindIndex(p => p.ActorNumber == currentTurn);
            int nextIndex = players.FindIndex(p => p.ActorNumber == nextTurn);

            if (nextIndex <= currentIndex && players.Count > 1) // End of cycle
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
            Debug.LogError("No players in the room!");
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

        // Get next index cyclically
        int nextIndex = (currentIndex + 1) % players.Count;
        Debug.Log($"Current turn: {currentTurn}, Next turn: {players[nextIndex].ActorNumber}");
        return players[nextIndex].ActorNumber;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient)
        {
            // If the current turn player left, advance to the next player
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_KEY, out object turnObj) && (int)turnObj == otherPlayer.ActorNumber)
            {
                Debug.Log($"Player {otherPlayer.ActorNumber} left during their turn. Advancing turn.");
                EndTurn();
            }
        }
    }
}