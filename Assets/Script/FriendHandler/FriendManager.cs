using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Firebase.Auth;
using Firebase.Firestore;

public class FriendManager : MonoBehaviour
{
    public static FriendManager Instance;

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    [Header("Friend List UI")]
    public Transform friendListContent;
    public GameObject friendItemPrefab;

    [Header("Friend Requests UI")]
    public Transform requestListContent;
    public GameObject requestItemPrefab;

    [Header("Send Friend Request")]
    public TMP_InputField searchInput;
    public Button sendRequestButton;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    private void Start()
    {
        sendRequestButton.onClick.AddListener(() => _ = OnSendFriendRequest());
        _ = RefreshFriendList();
        _ = RefreshFriendRequests();
    }

    private async Task OnSendFriendRequest()
    {
        string username = searchInput.text.Trim();
        if (!string.IsNullOrEmpty(username))
        {
            await SearchAndSendFriendRequest(username);
        }
    }

    public async Task RefreshFriendList()
    {
        foreach (Transform child in friendListContent)
            Destroy(child.gameObject);

        string myUID = auth.CurrentUser.UserId;

        var friendListSnapshot = await db.Collection("friends").Document(myUID).Collection("list").GetSnapshotAsync();

        foreach (var doc in friendListSnapshot.Documents)
        {
            string friendUID = doc.Id;

            var userSnapshot = await db.Collection("users").Document(friendUID).GetSnapshotAsync();
            string username = userSnapshot.Exists ? userSnapshot.GetValue<string>("username") : friendUID;

            GameObject item = Instantiate(friendItemPrefab, friendListContent);
            item.GetComponentInChildren<TMP_Text>().text = username;

            Button removeBtn = item.transform.Find("RemoveButton").GetComponent<Button>();
            removeBtn.onClick.AddListener(() =>
            {
                _ = RemoveFriend(friendUID);
                Destroy(item);
            });
        }
    }

    public async Task RefreshFriendRequests()
    {
        foreach (Transform child in requestListContent)
            Destroy(child.gameObject);

        string myUID = auth.CurrentUser.UserId;

        var requestSnapshot = await db.Collection("friend_requests").WhereEqualTo("to", myUID).GetSnapshotAsync();

        foreach (var doc in requestSnapshot.Documents)
        {
            string fromUID = doc.GetValue<string>("from");

            GameObject item = Instantiate(requestItemPrefab, requestListContent);
            TMP_Text usernameText = item.transform.Find("UsernameText").GetComponent<TMP_Text>();
            Button acceptBtn = item.transform.Find("AcceptButton").GetComponent<Button>();
            Button declineBtn = item.transform.Find("DeclineButton").GetComponent<Button>();

            var userSnapshot = await db.Collection("users").Document(fromUID).GetSnapshotAsync();
            usernameText.text = userSnapshot.Exists ? userSnapshot.GetValue<string>("username") : fromUID;

            acceptBtn.onClick.AddListener(() =>
            {
                _ = AcceptRequest(fromUID);
                Destroy(item);
                _ = RefreshFriendList();
            });

            declineBtn.onClick.AddListener(() =>
            {
                _ = DeclineRequest(fromUID);
                Destroy(item);
            });
        }
    }

    public async Task AcceptRequest(string fromUID)
    {
        string myUID = auth.CurrentUser.UserId;

        WriteBatch batch = db.StartBatch();

        batch.Set(db.Collection("friends").Document(myUID).Collection("list").Document(fromUID),
            new Dictionary<string, object> { { "timestamp", Timestamp.GetCurrentTimestamp() } });

        batch.Set(db.Collection("friends").Document(fromUID).Collection("list").Document(myUID),
            new Dictionary<string, object> { { "timestamp", Timestamp.GetCurrentTimestamp() } });

        batch.Delete(db.Collection("friend_requests").Document($"{fromUID}_{myUID}"));

        await batch.CommitAsync();
        Debug.Log($"Accepted friend: {fromUID}");
    }

    public async Task DeclineRequest(string fromUID)
    {
        string myUID = auth.CurrentUser.UserId;

        await db.Collection("friend_requests").Document($"{fromUID}_{myUID}").DeleteAsync();
        Debug.Log($"Declined friend request from: {fromUID}");
    }

    public async Task RemoveFriend(string friendUID)
    {
        string myUID = auth.CurrentUser.UserId;

        WriteBatch batch = db.StartBatch();
        batch.Delete(db.Collection("friends").Document(myUID).Collection("list").Document(friendUID));
        batch.Delete(db.Collection("friends").Document(friendUID).Collection("list").Document(myUID));

        await batch.CommitAsync();
        Debug.Log($"Removed friend: {friendUID}");
    }

    public async Task SearchAndSendFriendRequest(string username)
    {
        string myUID = auth.CurrentUser.UserId;

        var querySnapshot = await db.Collection("users").WhereEqualTo("username", username).GetSnapshotAsync();
        var doc = querySnapshot.Documents.FirstOrDefault();

        if (doc == null)
        {
            Debug.LogWarning("User not found.");
            return;
        }

        string targetUID = doc.Id;

        if (targetUID == myUID)
        {
            Debug.LogWarning("Cannot add yourself.");
            return;
        }

        var friendCheck = await db.Collection("friends").Document(myUID).Collection("list").Document(targetUID).GetSnapshotAsync();
        if (friendCheck.Exists)
        {
            Debug.LogWarning("Already friends.");
            return;
        }

        string requestDocId = $"{myUID}_{targetUID}";
        var reqCheck = await db.Collection("friend_requests").Document(requestDocId).GetSnapshotAsync();
        if (reqCheck.Exists)
        {
            Debug.LogWarning("Request already sent.");
            return;
        }

        var requestData = new Dictionary<string, object>
        {
            { "from", myUID },
            { "to", targetUID },
            { "timestamp", Timestamp.GetCurrentTimestamp() }
        };

        await db.Collection("friend_requests").Document(requestDocId).SetAsync(requestData);
        Debug.Log("Friend request sent!");
    }
}
