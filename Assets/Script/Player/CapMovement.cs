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
    public TextMeshProUGUI targetBoxText;

    [Header("Around the World")]
    public int currentBox = 1; // Tracks the current box (1 to 13, then 1 for win)
    private readonly int maxBox = 13; // Maximum box number
    private bool hasWon = false; // Tracks if the player has won
    private int currentBoxEntered = 0; // Tracks the box the cap is currently in
    private bool touchedBoxLine = false; // Tracks if cap touched a "BoxLine" during movement
    private bool hasResetThisFlick = false; // Prevents multiple resets in one flick

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
            touchedBoxLine = false; // Reset line collision flag on new flick
            hasResetThisFlick = false; // Reset flag on new flick

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
            Debug.Log($"CheckAllCapsStopped: currentBox={currentBox}, currentBoxEntered={currentBoxEntered}, hasWon={hasWon}, touchedBoxLine={touchedBoxLine}, position={transform.position}, capCount={caps.Length}");
            // Check if this cap is in its target box and stopped
            if (!hasWon && currentBoxEntered == currentBox)
            {
                int boxesToAdvance = touchedBoxLine ? 1 : 3; // Advance 3 boxes if no line was touched, else 1
                Debug.Log($"Advancing {boxesToAdvance} box(es) for player {PhotonNetwork.LocalPlayer.ActorNumber} from box {currentBox}");
                photonView.RPC("RPC_AdvanceBox", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, boxesToAdvance);
                currentBoxEntered = 0; // Reset after advancing
            }
            else if (!hasWon && currentBoxEntered != 0)
            {
                // Landed in wrong box, lose next turn
                Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} landed in wrong box {currentBoxEntered} (target: {currentBox}), losing next turn");
                photonView.RPC("RPC_LoseTurn", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
                currentBoxEntered = 0; // Reset after checking
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
        // Track if cap touches a line
        else if (other.CompareTag("BoxLine"))
        {
            touchedBoxLine = true;
            Debug.Log($"Cap touched BoxLine at position: {transform.position}");
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

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!photonView.IsMine || hasWon || hasResetThisFlick || !TurnManager.Instance.IsMyTurn()) return;

        // Check for collision with another cap
        if (collision.gameObject.CompareTag("BottleCap") && currentBox == 1)
        {
            Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} hit another cap while on box 1 during their turn, resetting position and progress");
            photonView.RPC("RPC_ResetProgress", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
            hasResetThisFlick = true; // Prevent multiple resets
        }
    }

    [PunRPC]
    private void RPC_AdvanceBox(int actorNumber, int boxesToAdvance)
    {
        if (photonView.Owner.ActorNumber != actorNumber) return;

        if (!hasWon)
        {
            currentBox += boxesToAdvance;
            if (currentBox > maxBox)
            {
                // If advancing past 13, wrap around to 1 and check for win
                currentBox = currentBox % maxBox;
                if (currentBox == 1)
                {
                    hasWon = true;
                    photonView.RPC("RPC_AnnounceWinner", RpcTarget.All, actorNumber);
                }
            }
            UpdateTargetBoxText();
            Debug.Log($"Player {actorNumber} advanced to box {currentBox} (advanced {boxesToAdvance} boxes)");
        }
    }

    [PunRPC]
    private void RPC_LoseTurn(int actorNumber)
    {
        if (photonView.Owner.ActorNumber != actorNumber) return;
        TurnManager.Instance.MarkPlayerLostTurn(actorNumber);
        Debug.Log($"Player {actorNumber} marked as lost turn");
    }

    [PunRPC]
    private void RPC_ResetProgress(int actorNumber)
    {
        if (photonView.Owner.ActorNumber != actorNumber) return;

        // Reset box and win state
        currentBox = 1;
        hasWon = false;
        UpdateTargetBoxText();

        // Reset position to spawn point
        int spawnIndex = GetSpawnIndex();
        if (GameManager.Instance == null || GameManager.Instance.spawnPoints == null || GameManager.Instance.spawnPoints.Length == 0)
        {
            Debug.LogError($"Cannot reset position for player {actorNumber}: GameManager or spawnPoints is null or empty");
            return;
        }
        if (spawnIndex >= 0 && spawnIndex < GameManager.Instance.spawnPoints.Length && GameManager.Instance.spawnPoints[spawnIndex] != null)
        {
            transform.position = GameManager.Instance.spawnPoints[spawnIndex].position;
            rb.velocity = Vector2.zero;
            Debug.Log($"Player {actorNumber} reset to spawn point {spawnIndex} at position {transform.position}");
        }
        else
        {
            Debug.LogError($"Invalid spawn point index {spawnIndex} for player {actorNumber}. Available spawn points: {GameManager.Instance.spawnPoints.Length}");
            // Fallback: Move to origin or first valid spawn point
            transform.position = GameManager.Instance.spawnPoints.Length > 0 ? GameManager.Instance.spawnPoints[0].position : Vector3.zero;
            rb.velocity = Vector2.zero;
            Debug.LogWarning($"Player {actorNumber} moved to fallback position {transform.position}");
        }
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

    private bool IsPointerOverThisCap()
    {
        Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        return hit.collider != null && hit.collider.gameObject == gameObject;
    }

    private int GetSpawnIndex()
    {
        if (PhotonNetwork.LocalPlayer == null)
        {
            Debug.LogError("LocalPlayer is null in GetSpawnIndex");
            return 0;
        }

        int playerCount = PhotonNetwork.PlayerList.Length;
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int spawnPointCount = (GameManager.Instance != null && GameManager.Instance.spawnPoints != null) ? GameManager.Instance.spawnPoints.Length : 0;

        if (spawnPointCount == 0)
        {
            Debug.LogError("No spawn points available in GameManager");
            return 0;
        }

        int spawnIndex;
        if (MultiplayerManager.Instance.selectedMode == GameMode.OneVsOne)
        {
            spawnIndex = actorNumber % 2;
        }
        else if (MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
        {
            int team = TurnManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            spawnIndex = (team - 1) * 2 + (actorNumber % 2);
        }
        else
        {
            spawnIndex = actorNumber % spawnPointCount;
        }

        // Ensure spawnIndex is within bounds
        if (spawnIndex >= spawnPointCount)
        {
            Debug.LogWarning($"Spawn index {spawnIndex} out of bounds for player {actorNumber}. Using modulo: {spawnIndex % spawnPointCount}");
            spawnIndex = spawnIndex % spawnPointCount;
        }
        else if (spawnIndex < 0)
        {
            Debug.LogWarning($"Negative spawn index {spawnIndex} for player {actorNumber}. Using 0");
            spawnIndex = 0;
        }

        Debug.Log($"GetSpawnIndex for player {actorNumber}: mode={MultiplayerManager.Instance.selectedMode}, team={(MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo ? TurnManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer) : 0)}, spawnIndex={spawnIndex}, spawnPointCount={spawnPointCount}");
        return spawnIndex;
    }
}