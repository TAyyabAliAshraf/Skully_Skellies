using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class FirestoreUploader : MonoBehaviour
{
    FirebaseFirestore db;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                Debug.Log("Firestore initialized");

                // Automatically send "Data sent" when ready
                UploadData();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + task.Result);
            }
        });
    }

    void UploadData()
    {
        if (db == null)
        {
            Debug.LogWarning("Firestore not initialized.");
            return;
        }

        var data = new Dictionary<string, object>
        {
            { "message", "Data sent" },
            { "timestamp", Timestamp.GetCurrentTimestamp() }
        };

        db.Collection("logs").AddAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted)
            {
                Debug.Log("Data sent to Firestore.");
            }
            else
            {
                Debug.LogError("Failed to send data: " + task.Exception);
            }
        });
    }
}
