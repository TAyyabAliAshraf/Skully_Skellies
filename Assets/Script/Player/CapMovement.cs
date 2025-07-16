using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BottleCap : MonoBehaviour
{
    [Header("Physics Settings")]
    public float flickPower = 5f;
    public float maxDragDistance = 3f;
    public float minVelocity = 1f;

    [Header("Arrow Settings")]
    public Transform arrowContainer;            // Rotates around cap
    public SpriteRenderer powerArrowSprite;     // Optional: shows drag power
    public float arrowDistance = 1f;

    private Rigidbody2D rb;
    private bool isDragging = false;
    private bool isTouchingThisCap = false;
    private Vector2 dragStartPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // no gravity for top-down movement
        rb.drag = 1f; // to slow down naturally

        if (powerArrowSprite != null)
            powerArrowSprite.gameObject.SetActive(false);
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        Vector2 worldMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // Start drag
        if (Input.GetMouseButtonDown(0) && IsPointerOverThisCap())
        {
            Debug.Log("Start dragging this cap.");
            isTouchingThisCap = true;
            dragStartPos = worldMousePos;
            isDragging = true;
            rb.velocity = Vector2.zero; // stop current motion

            if (powerArrowSprite != null)
                powerArrowSprite.gameObject.SetActive(true);
        }

        // While dragging
        if (Input.GetMouseButton(0) && isDragging && isTouchingThisCap)
        {
            Vector2 direction = dragStartPos - worldMousePos;
            float distance = Mathf.Clamp(direction.magnitude, 0f, maxDragDistance);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            Debug.Log($"Dragging: direction={direction}, distance={distance}");

            if (arrowContainer != null)
            {
                arrowContainer.rotation = Quaternion.Euler(0, 0, angle);
                arrowContainer.localPosition = direction.normalized * arrowDistance;
            }

            if (powerArrowSprite != null)
                powerArrowSprite.size = new Vector2(distance / maxDragDistance, powerArrowSprite.size.y);
        }

        // Release
        if (Input.GetMouseButtonUp(0) && isDragging && isTouchingThisCap)
        {
            Vector2 direction = dragStartPos - worldMousePos;
            float distance = Mathf.Min(direction.magnitude, maxDragDistance);
            Vector2 flick = direction.normalized * distance * flickPower;

            if (flick.magnitude < minVelocity)
            {
                flick = direction.normalized * minVelocity * 1.2f;
                Debug.Log("Flick too weak, applying minimum velocity.");
            }

            rb.velocity = flick;

            Debug.Log($"Released: Applied velocity = {flick}");

            isDragging = false;
            isTouchingThisCap = false;

            if (powerArrowSprite != null)
                powerArrowSprite.gameObject.SetActive(false);
        }
    }

    private bool IsPointerOverThisCap()
{
    Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    RaycastHit2D[] hits = Physics2D.RaycastAll(worldPoint, Vector2.zero);
    foreach (var hit in hits)
    {
        Debug.Log("Raycast hit: " + hit.collider.name);
        if (hit.collider.gameObject == gameObject)
            return true;
    }
    Debug.Log("Raycast hit nothing or missed this cap.");
    return false;
}

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Trigger hit: " + other.gameObject.name);
    }
}
