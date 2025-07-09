using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BottleCap : MonoBehaviour
{
    [Header("Movement Settings")]
    public float glideFactor = 0.95f;
    public float minVelocity = 1f;
    public float flickPower = 5f;
    public float maxDragDistance = 300f;
    public float bounceDamping = 0.6f;

    [Header("UI References")]
    public RectTransform canvasRect;             // Automatically assigned if null
    public RectTransform arrowContainer;         // Rotates around cap
    public Image powerArrowImage;                // Filled image
    public float arrowDistance = 80f;

    private RectTransform rectTransform;
    private Vector2 velocity;
    private bool isDragging = false;
    private bool isTouchingThisCap = false;
    private Vector2 dragStartPos;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        powerArrowImage.gameObject.SetActive(false);

        // ✅ Assign canvasRect if not assigned
        if (canvasRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
            }
        }

        Debug.Log("canvasRect assigned: " + (canvasRect != null));
    }

    void Update()
    {
        HandleInput();

        if (!isDragging && velocity.magnitude > minVelocity)
        {
            rectTransform.anchoredPosition += velocity * Time.deltaTime;
            BounceInsideCanvas();
            velocity *= glideFactor;
        }
    }

    private void HandleInput()
    {
        if (canvasRect == null) return;

        Vector2 localMousePos;

        // Start drag
        if (Input.GetMouseButtonDown(0) && IsPointerOverThisCap())
        {
            isTouchingThisCap = true;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, null, out dragStartPos
            );

            isDragging = true;
            powerArrowImage.gameObject.SetActive(true);
            powerArrowImage.fillAmount = 0f;
        }

        // While dragging
        if (Input.GetMouseButton(0) && isDragging && isTouchingThisCap)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, null, out localMousePos
            );

            Vector2 direction = dragStartPos - localMousePos;
            float distance = Mathf.Clamp(direction.magnitude, 0f, maxDragDistance);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Rotate and position arrow
            arrowContainer.localRotation = Quaternion.Euler(0, 0, angle);
            powerArrowImage.rectTransform.anchoredPosition = new Vector2(arrowDistance, 0);
            powerArrowImage.fillAmount = distance / maxDragDistance;
        }

        // Release
        if (Input.GetMouseButtonUp(0) && isDragging && isTouchingThisCap)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, null, out localMousePos
            );

            Vector2 direction = dragStartPos - localMousePos;
            float distance = Mathf.Min(direction.magnitude, maxDragDistance);
            velocity = direction.normalized * distance * flickPower;

            // ✅ Always apply minimum velocity to ensure first move
            if (velocity.magnitude < minVelocity)
            {
                velocity = direction.normalized * minVelocity * 1.2f;
            }

            isDragging = false;
            isTouchingThisCap = false;
            powerArrowImage.gameObject.SetActive(false);
        }
    }

    private void BounceInsideCanvas()
    {
        if (canvasRect == null) return;

        // Get world corners
        Vector3[] corners = new Vector3[4];
        canvasRect.GetWorldCorners(corners);
        Vector3 bottomLeft = corners[0];
        Vector3 topRight = corners[2];

        Vector3 capWorldPos = rectTransform.position;
        Vector3 capSize = rectTransform.lossyScale * rectTransform.rect.size;

        float halfW = capSize.x / 2f;
        float halfH = capSize.y / 2f;

        // Bounce X
        if (capWorldPos.x - halfW < bottomLeft.x)
        {
            capWorldPos.x = bottomLeft.x + halfW;
            velocity.x *= -bounceDamping;
        }
        else if (capWorldPos.x + halfW > topRight.x)
        {
            capWorldPos.x = topRight.x - halfW;
            velocity.x *= -bounceDamping;
        }

        // Bounce Y
        if (capWorldPos.y - halfH < bottomLeft.y)
        {
            capWorldPos.y = bottomLeft.y + halfH;
            velocity.y *= -bounceDamping;
        }
        else if (capWorldPos.y + halfH > topRight.y)
        {
            capWorldPos.y = topRight.y - halfH;
            velocity.y *= -bounceDamping;
        }

        rectTransform.position = capWorldPos;
    }

    private bool IsPointerOverThisCap()
    {
        PointerEventData pointerData = new(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
        {
            if (result.gameObject == gameObject)
                return true;
        }

        return false;
    }


    /// <summary>
    /// Sent when another object enters a trigger collider attached to this
    /// object (2D physics only).
    /// </summary>
    /// <param name="other">The other Collider2D involved in this collision.</param>
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("" + other.gameObject);
    }
}
