using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PhotonView))]
public class CapMovement : MonoBehaviourPunCallbacks
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
    public TextMeshProUGUI targetBoxText; // Reference to TextMeshProUGUI for displaying next target box

    [Header("Around the World")]
    public int currentBox = 1; // Tracks the current box (1 to 13, then 1 for win)
    private readonly int maxBox = 13; // Maximum box number
    private bool hasWon = false; // Tracks if the player has won
    private int currentBoxEntered = 0; // Tracks the box the cap is currently in

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

        if (targetBoxText == null)
        {
            Debug.LogError("TextMeshProUGUI component not assigned to CapMovement!");
        }
        UpdateTargetBoxText();
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
        if (!photonView.IsMine || !TurnManager.Instance.IsMyTurn() || hasWon) return;

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

            // Start checking if all caps have stopped to end the turn
            InvokeRepeating("CheckAllCapsStopped", 0.5f, 0.1f);
        }
    }

    private void CheckAllCapsStopped()
    {
        if (!photonView.IsMine) return;

        // Check if all bottle caps have stopped (velocity < 0.1f)
        GameObject[] caps = GameObject.FindGameObjectsWithTag("BottleCap");
        bool allStopped = true;
        foreach (GameObject cap in caps)
        {
            Rigidbody2D capRb = cap.GetComponent<Rigidbody2D>();
            if (capRb != null && capRb.velocity.magnitude >= 0.1f)
            {
                allStopped = false;
                break;
            }
        }

        if (allStopped)
        {
            CancelInvoke("CheckAllCapsStopped");
            // Debug the state before checking box advancement
            Debug.Log($"CheckAllCapsStopped: currentBox={currentBox}, currentBoxEntered={currentBoxEntered}, hasWon={hasWon}, position={transform.position}, capCount={caps.Length}");
            // Check if this cap is in its target box and stopped
            if (!hasWon && currentBoxEntered == currentBox)
            {
                Debug.Log($"Advancing box for player {PhotonNetwork.LocalPlayer.ActorNumber} from box {currentBox}");
                photonView.RPC("RPC_AdvanceBox", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
                currentBoxEntered = 0; // Reset after advancing
            }
            else
            {
                currentBoxEntered = 0; // Reset if not advancing
            }
            if (GameManager.Instance == null || GameManager.Instance.photonView == null)
            {
                Debug.LogError("GameManager or its PhotonView is null! Cannot send RPC_EndTurn.");
                return;
            }
            Debug.Log($"Sending RPC_EndTurn to GameManager PhotonView ID: {GameManager.Instance.photonView.ViewID} from CapMovement PhotonView ID: {photonView.ViewID}");
            GameManager.Instance.photonView.RPC("RPC_EndTurn", RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine || hasWon) return;

        // Set the current box the cap is in
        if (other.CompareTag("BoardBox") && int.TryParse(other.gameObject.name, out int boxNumber))
        {
            currentBoxEntered = boxNumber;
            Debug.Log($"Cap entered box {boxNumber}, current target: {currentBox}, position: {transform.position}, bounds: {other.bounds}");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!photonView.IsMine || hasWon) return;

        // Clear currentBoxEntered if the cap exits the box
        if (other.CompareTag("BoardBox") && int.TryParse(other.gameObject.name, out int boxNumber))
        {
            if (boxNumber == currentBoxEntered)
            {
                Debug.Log($"Cap exited box {boxNumber}, position: {transform.position}, velocity: {rb.velocity.magnitude}");
                currentBoxEntered = 0;
            }
        }
    }

    [PunRPC]
    private void RPC_AdvanceBox(int actorNumber)
    {
        if (photonView.Owner.ActorNumber != actorNumber) return;

        if (currentBox < maxBox)
        {
            currentBox++;
        }
        else if (currentBox == maxBox)
        {
            // Reached box 13, next target is box 1
            currentBox = 1;
            hasWon = true;
            photonView.RPC("RPC_AnnounceWinner", RpcTarget.All, actorNumber);
        }
        UpdateTargetBoxText();
        Debug.Log($"Player {actorNumber} advanced to box {currentBox}");
    }

    [PunRPC]
    private void RPC_AnnounceWinner(int actorNumber)
    {
        Debug.Log($"Player {actorNumber} has won by completing Around the World!");
        // Optionally, trigger game end logic here (e.g., display win screen)
    }

    private void UpdateTargetBoxText()
    {
        if (targetBoxText != null)
        {
            targetBoxText.text = hasWon ? "Winner!" : $"Box {currentBox}";
        }
    }

    private void EndTurn()
    {
        // This method is kept for compatibility but is now handled via RPC_EndTurn
    }

    [PunRPC]
    private void EndTurnRPC()
    {
        if (!photonView.IsMine) return;
        Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} ending turn via RPC");
        TurnManager.Instance.EndTurn();
    }

    private bool IsPointerOverThisCap()
    {
        Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        return hit.collider != null && hit.collider.gameObject == gameObject;
    }
}