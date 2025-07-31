using UnityEngine;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using Google;
using System.Threading.Tasks;

public class UserManager : MonoBehaviour
{
    public static UserManager Instance;

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    public FirebaseUser firebaseUser;

    [Header("User Data")]
    public string Username;
    public int Wins, Losses, Level, XP, Coins, SelectedDP;
    public List<bool> Caps = new List<bool>();

    private GoogleSignInConfiguration configuration;
    private string webClientId = "532350159171-n6uhc27bcn414q224o9m5ihl3qobi7oq.apps.googleusercontent.com"; // Replace with your WebClientId

    private void Awake()
    {
        // Singleton pattern
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

        // Google config
        configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestIdToken = true,
            UseGameSignIn = false,
            RequestEmail = true
        };

        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.DefaultInstance;

                Debug.Log("✅ Firebase initialized.");

                GoogleSignIn.Configuration = configuration;

                // Try silent sign-in
                GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleSignIn);
            }
            else
            {
                Debug.LogError("❌ Firebase dependency error: " + task.Result);
            }
        });
    }

    private void OnGoogleSignIn(Task<GoogleSignInUser> task)
    {
        if (task.IsCanceled || task.IsFaulted)
        {
            Debug.LogWarning(" Silent sign-in failed: " + task.Exception);
            Debug.Log("Trying interactive login...");

            GoogleSignIn.DefaultInstance.SignOut(); // Force reset
            GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleSignInFinished);
            return;
        }

        HandleGoogleUser(task.Result);
    }


    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsCanceled || task.IsFaulted)
        {
            Debug.LogError(" Google Sign-In failed: " + task.Exception?.Flatten().InnerException?.Message);
            return;
        }

        HandleGoogleUser(task.Result);
    }

    private void HandleGoogleUser(GoogleSignInUser googleUser)
    {
        if (googleUser == null)
        {
            Debug.LogError(" GoogleSignInUser is null.");
            return;
        }

        Debug.Log(" Google Sign-In success: " + googleUser.DisplayName);

        Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
            if (authTask.IsCompleted && !authTask.IsFaulted)
            {
                firebaseUser = authTask.Result;
                Debug.Log(" Firebase Sign-In success: " + firebaseUser.UserId);
                LoadOrCreateUserData();
            }
            else
            {
                Debug.LogError(" Firebase Auth failed: " + authTask.Exception?.Flatten().Message);
            }
        });
    }

    private void LoadOrCreateUserData()
    {
        var docRef = db.Collection("users").Document(firebaseUser.UserId);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    // Load user data
                    Username = snapshot.GetValue<string>("username");
                    Wins = snapshot.GetValue<int>("wins");
                    Losses = snapshot.GetValue<int>("losses");
                    Level = snapshot.GetValue<int>("level");
                    XP = snapshot.GetValue<int>("xp");
                    Coins = snapshot.GetValue<int>("coins");
                    SelectedDP = snapshot.GetValue<int>("selectedDP");
                    Caps = snapshot.GetValue<List<bool>>("caps");

                    Debug.Log("✅ User data loaded from Firestore.");
                }
                else
                {
                    // Create default user data
                    List<bool> capsList = new List<bool>();
                    for (int i = 0; i < 10; i++) capsList.Add(false);

                    var newUser = new Dictionary<string, object>
                    {
                        { "uid", firebaseUser.UserId },
                        { "username", firebaseUser.DisplayName ?? "Player" },
                        { "wins", 0 },
                        { "losses", 0 },
                        { "level", 1 },
                        { "xp", 0 },
                        { "coins", 0 },
                        { "caps", capsList },
                        { "selectedDP", 0 }
                    };

                    docRef.SetAsync(newUser).ContinueWithOnMainThread(setTask =>
                    {
                        if (setTask.IsCompleted)
                        {
                            Debug.Log("🆕 New user created in Firestore.");

                            Username = (string)newUser["username"];
                            Wins = 0;
                            Losses = 0;
                            Level = 1;
                            XP = 0;
                            Coins = 0;
                            SelectedDP = 0;
                            Caps = capsList;
                        }
                        else
                        {
                            Debug.LogError(" Failed to create user document: " + setTask.Exception?.Flatten().Message);
                        }
                    });
                }
            }
            else
            {
                Debug.LogError(" Failed to retrieve user document: " + task.Exception?.Flatten().Message);
            }
        });
    }

    public void UpdateField(string key, object value)
    {
        if (firebaseUser == null) return;

        db.Collection("users").Document(firebaseUser.UserId).UpdateAsync(new Dictionary<string, object>
        {
            { key, value }
        }).ContinueWithOnMainThread(t =>
        {
            if (t.IsCompleted)
            {
                Debug.Log($" Field '{key}' updated.");
            }
            else
            {
                Debug.LogError($" Failed to update field '{key}': " + t.Exception?.Flatten().Message);
            }
        });
    }

    public void SignOut()
    {
        GoogleSignIn.DefaultInstance.SignOut();
        auth.SignOut();
        firebaseUser = null;
        Debug.Log(" Signed out.");
    }

    // Optional: manual trigger for UI button
    public void ManualSignIn()
    {
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleSignInFinished);
    }
}
