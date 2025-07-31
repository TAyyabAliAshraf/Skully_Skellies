using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public SpriteRenderer boardRenderer; // Assign the board sprite
    public float borderPadding = 0.1f;   // % of screen to leave as padding

    void Update()
    {
        FitToScreen();
    }

    void FitToScreen()
    {
        if (boardRenderer == null) return;

        float screenRatio = (float)Screen.width / (float)Screen.height;
        float targetRatio = boardRenderer.bounds.size.x / boardRenderer.bounds.size.y;

        Camera cam = Camera.main;

        if (screenRatio >= targetRatio)
        {
            cam.orthographicSize = boardRenderer.bounds.size.y / 2f + borderPadding;
        }
        else
        {
            float differenceInSize = targetRatio / screenRatio;
            cam.orthographicSize = boardRenderer.bounds.size.y / 2f * differenceInSize + borderPadding;
        }
    }
}
