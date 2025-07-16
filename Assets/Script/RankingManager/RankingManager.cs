using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;

public class RankingManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    [Header("Ranking UI")]
    public Transform worldRankContent;
    public Transform friendRankContent;
    public GameObject rankItemPrefab;

    [Header("Buttons")]
    public Button worldRankButton;
    public Button friendRankButton;

    private void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    private void Start()
    {
        // Hook up button clicks
        worldRankButton.onClick.AddListener(OnWorldRankClicked);
        friendRankButton.onClick.AddListener(OnFriendRankClicked);
    }

    private void OnWorldRankClicked()
    {
        worldRankContent.gameObject.SetActive(true);
        friendRankContent.gameObject.SetActive(false);
        _ = LoadTop10GlobalRank();
    }

    private void OnFriendRankClicked()
    {
        friendRankContent.gameObject.SetActive(true);
        worldRankContent.gameObject.SetActive(false);
        _ = LoadTop10FriendRank();
    }

    public async Task LoadTop10GlobalRank()
    {
        ClearUI(worldRankContent);

        QuerySnapshot snapshot = await db.Collection("users")
            .OrderByDescending("wins")
            .Limit(10)
            .GetSnapshotAsync();

        int rank = 1;
        foreach (var doc in snapshot.Documents)
        {
            string username = doc.GetValue<string>("username");
            int wins = doc.GetValue<int>("wins");

            CreateRankItem(worldRankContent, rank, username, wins);
            rank++;
        }
    }

    public async Task LoadTop10FriendRank()
    {
        ClearUI(friendRankContent);

        string myUID = auth.CurrentUser.UserId;

        QuerySnapshot friendSnapshot = await db.Collection("friends")
            .Document(myUID)
            .Collection("list")
            .GetSnapshotAsync();

        List<Task<DocumentSnapshot>> friendTasks = new List<Task<DocumentSnapshot>>();

        foreach (var doc in friendSnapshot.Documents)
        {
            string friendUID = doc.Id;
            friendTasks.Add(db.Collection("users").Document(friendUID).GetSnapshotAsync());
        }

        DocumentSnapshot[] friendDocs = await Task.WhenAll(friendTasks);

        var sortedFriends = friendDocs
            .Where(doc => doc.Exists)
            .Select(doc => new
            {
                Username = doc.GetValue<string>("username"),
                Wins = doc.GetValue<int>("wins")
            })
            .OrderByDescending(f => f.Wins)
            .Take(10)
            .ToList();

        int rank = 1;
        foreach (var friend in sortedFriends)
        {
            CreateRankItem(friendRankContent, rank, friend.Username, friend.Wins);
            rank++;
        }
    }

    private void CreateRankItem(Transform parent, int rank, string username, int wins)
    {
        GameObject item = Instantiate(rankItemPrefab, parent);
        item.transform.Find("UsernameText").GetComponent<TMP_Text>().text = $"{rank}. {username}";
        item.transform.Find("WinsText").GetComponent<TMP_Text>().text = $"{wins} Wins";
    }

    private void ClearUI(Transform container)
    {
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }
}
