using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PhotonView))]
public class BottleCap : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    public float flickPower = 5f;
    public float maxDragDistance = 3f;
    public float minVelocity = 1f;

    [Header("Visuals")]
    public Transform arrowContainer;
    public SpriteRenderer powerArrowSprite;
    public float arrowDistance = 1f;
    public Color[] teamColors;

    private Rigidbody2D rb;
    private bool isDragging = false;
    private Vector2 dragStartPos;
    private PhotonView photonView;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        photonView = GetComponent<PhotonView>();
        rb.gravityScale = 0f;

        if (powerArrowSprite != null)
            powerArrowSprite.gameObject.SetActive(false);
    }

    void Start()
    {
        if (MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
        {
            int team = TurnManager.Instance.GetPlayerTeam(photonView.Owner);
            if (team > 0 && team <= teamColors.Length)
            {
                GetComponent<SpriteRenderer>().color = teamColors[team - 1];
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine || !TurnManager.Instance.IsMyTurn()) return;

        HandleInput();
    }

    private void HandleInput()
    {
        Vector2 worldMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0) && IsPointerOverThisCap())
        {
            dragStartPos = worldMousePos;
            isDragging = true;
            rb.velocity = Vector2.zero;

            if (powerArrowSprite != null)
                powerArrowSprite.gameObject.SetActive(true);
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 direction = dragStartPos - worldMousePos;
            float distance = Mathf.Clamp(direction.magnitude, 0f, maxDragDistance);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (arrowContainer != null)
            {
                arrowContainer.rotation = Quaternion.Euler(0, 0, angle);
                arrowContainer.localPosition = direction.normalized * arrowDistance;
            }

            if (powerArrowSprite != null)
                powerArrowSprite.size = new Vector2(distance / maxDragDistance, powerArrowSprite.size.y);
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Vector2 direction = dragStartPos - worldMousePos;
            float distance = Mathf.Min(direction.magnitude, maxDragDistance);
            Vector2 flick = direction.normalized * distance * flickPower;

            if (flick.magnitude < minVelocity)
                flick = direction.normalized * minVelocity * 1.2f;

            rb.velocity = flick;
            isDragging = false;

            if (powerArrowSprite != null)
                powerArrowSprite.gameObject.SetActive(false);

            // End the turn only for the local player
            Invoke("EndTurn", 0.5f);
        }
    }


    void EndTurn()
    {
        GameManager.Instance.photonView.RPC("RPC_EndTurn", RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);

    }

    private bool IsPointerOverThisCap()
    {
        Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        return hit.collider != null && hit.collider.gameObject == gameObject;
    }

    [PunRPC]
    private void EndTurnRPC()
    {
        if (!photonView.IsMine) return;
        Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} ending turn via RPC");
        TurnManager.Instance.EndTurn();
    }
}