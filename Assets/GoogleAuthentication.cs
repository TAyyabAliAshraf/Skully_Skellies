using Google;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.Networking;

public class GoogleAuthentication : MonoBehaviour
{
    public string imageURL;
    public TMP_Text userNameTxt, userEmailTxt;
    public Image profilePic;
    public GameObject loginPanel, profilePanel;
    //  public TMP_Text statusTxt;
    private GoogleSignInConfiguration configuration;
    public string webClientId = "689631902804-vn3q1n4nkrh2i7u7spachs7l17prseq4.apps.googleusercontent.com"; // create your own google webclientId



    void Awake()
    {
        configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestIdToken = true,
            UseGameSignIn = false,
            RequestEmail = true
        };
    }

    public void OnSignIn()
    {
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(
            OnAuthenticationFinished, TaskScheduler.Default);
    }

    internal void OnAuthenticationFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted)
        {
            using (IEnumerator<System.Exception> enumerator =
                task.Exception.InnerExceptions.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    GoogleSignIn.SignInException error =
                        (GoogleSignIn.SignInException)enumerator.Current;
                    Debug.LogError("Got Error: " + error.Status + " " + error.Message);
                }
                else
                {
                    Debug.LogError("Got unexpected exception?!?" + task.Exception);
                }
            }
        }
        else if (task.IsCanceled)
        {
            Debug.LogError("Cancelled");
        }
        else
        {
            GoogleSignInUser user = task.Result;
            //statusTxt.text= user.Email;
            StartCoroutine(UpdateUI(task.Result));
        }
    }

    IEnumerator UpdateUI(GoogleSignInUser user)
    {
        Debug.Log("Welcome: " + user.DisplayName + "!");

        userNameTxt.text = user.DisplayName;
        userEmailTxt.text = user.Email;
        imageURL = user.ImageUrl.ToString();

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageURL);
        yield return request.SendWebRequest();
        Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);
        Rect rect = new Rect(0, 0, downloadedTexture.width, downloadedTexture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        profilePic.sprite = Sprite.Create(downloadedTexture, rect, pivot);

        loginPanel.SetActive(false);
        profilePanel.SetActive(true);
    }

    public void OnSignOut()
    {
        userNameTxt.text = "";
        userEmailTxt.text = "";

        imageURL = "";
        loginPanel.SetActive(true);
        profilePanel.SetActive(false);
        Debug.Log("Calling SignOut");
        GoogleSignIn.DefaultInstance.SignOut();
    }

}
