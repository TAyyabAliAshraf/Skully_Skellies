using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PhotonView))]
public class CapMovement : MonoBehaviourPunCallbacks
{
    [Header("Settings")] public float flickPower = 5f; public float maxDragDistance = 3f; public float minVelocity = 1f;

    [Header("Visuals")]
    public Transform arrowContainer;
    public SpriteRenderer powerArrowSprite;
    public float arrowDistance = 1f;
    public Color[] teamColors;
    public TextMeshProUGUI targetBoxText;

    [Header("Around the World")]
    public int currentBox = 1;
    private readonly int maxBox = 13;
    private bool hasWon = false;
    private int currentBoxEntered = 0;
    private bool isTouchingBoxLine = false;
    private bool hasResetThisFlick = false;
    private bool collidedWithCap = false;
    private int opponentHitActor = -1;
    private bool hasAppliedPenaltyThisTurn = false;

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
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
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
            isTouchingBoxLine = false;
            collidedWithCap = false;
            opponentHitActor = -1;
            hasResetThisFlick = false;
            hasAppliedPenaltyThisTurn = false;
            Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} started flick, resetting states");
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

            InvokeRepeating("CheckAllCapsStopped", 0.5f, 0.1f);
            Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} flicked cap with velocity {flick}");
        }
    }

    private void CheckAllCapsStopped()
    {
        if (!photonView.IsMine) return;

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

            foreach (GameObject cap in caps)
            {
                CapMovement capMovement = cap.GetComponent<CapMovement>();
                if (capMovement != null)
                {
                    Debug.Log($"Cap state: owner={capMovement.photonView.Owner?.ActorNumber}, position={cap.transform.position}, currentBox={capMovement.currentBox}, currentBoxEntered={capMovement.currentBoxEntered}, isTouchingBoxLine={capMovement.isTouchingBoxLine}");
                }
            }
            Debug.Log($"CheckAllCapsStopped: currentBox={currentBox}, currentBoxEntered={currentBoxEntered}, hasWon={hasWon}, isTouchingBoxLine={isTouchingBoxLine}, collidedWithCap={collidedWithCap}, opponentHitActor={opponentHitActor}, position={transform.position}, capCount={caps.Length}");

            if (!hasWon)
            {
                bool opponentInCorrectBox = CheckOpponentCapInCorrectBox();
                if (opponentInCorrectBox)
                {
                    int currentTurnPlayer = GetCurrentTurnPlayer();
                    if (currentTurnPlayer != -1)
                    {
                        Debug.Log($"Player {currentTurnPlayer} hit opponent {opponentHitActor}'s cap into their correct box, advancing 2 boxes");
                        FindAndAdvancePlayerCap(currentTurnPlayer, 2);
                    }
                    else
                    {
                        Debug.LogError("Failed to get current turn player for opponent box rule");
                    }
                }

                // New Rule: If Player 1 hit Player 2's cap into a SkellyBox, apply penalty to Player 2
                bool opponentInSkellyBox = CheckOpponentCapInSkellyBox();
                if (opponentInSkellyBox)
                {
                    // Penalty applied in CheckOpponentCapInSkellyBox
                    Debug.Log($"Player {opponentHitActor}'s cap landed in a SkellyBox, penalty applied");
                }

                if (!opponentInCorrectBox && !opponentInSkellyBox && collidedWithCap && currentBox == 1)
                {
                    Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} hit another cap while on box 1, resetting position and progress");
                    photonView.RPC("RPC_ResetProgress", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
                }
                else if (currentBoxEntered == currentBox)
                {
                    int boxesToAdvance = isTouchingBoxLine ? 1 : 3;
                    Debug.Log($"Advancing {boxesToAdvance} box(es) for player {PhotonNetwork.LocalPlayer.ActorNumber} from box {currentBox}");
                    photonView.RPC("RPC_AdvanceBox", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, boxesToAdvance);
                    currentBoxEntered = 0;
                }
                else if (currentBoxEntered != 0 && !hasAppliedPenaltyThisTurn)
                {
                    int penaltyTurns = 1;
                    Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} landed in wrong box {currentBoxEntered} (target: {currentBox}), applying penalty of {penaltyTurns} turn");
                    photonView.RPC("RPC_ApplyTurnPenalty", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, penaltyTurns);
                    hasAppliedPenaltyThisTurn = true;
                    currentBoxEntered = 0;
                }
                else
                {
                    currentBoxEntered = 0;
                }
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

    private bool CheckOpponentCapInSkellyBox()
    {
        if (opponentHitActor == -1 || !collidedWithCap) return false;

        GameObject[] caps = GameObject.FindGameObjectsWithTag("BottleCap");
        foreach (GameObject cap in caps)
        {
            CapMovement capMovement = cap.GetComponent<CapMovement>();
            if (capMovement != null && capMovement.photonView != null && capMovement.photonView.Owner != null)
            {
                if (capMovement.photonView.Owner.ActorNumber == opponentHitActor)
                {
                    if (capMovement.currentBoxEntered != 0)
                    {
                        GameObject skellyBox = GameObject.Find($"SkellyBox{capMovement.currentBoxEntered}");
                        if (skellyBox != null && skellyBox.CompareTag("SkellyBox"))
                        {
                            string boxName = skellyBox.name;
                            if (int.TryParse(boxName.Replace("SkellyBox", ""), out int skellyBoxNumber))
                            {
                                if (!capMovement.hasAppliedPenaltyThisTurn)
                                {
                                    Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} hit opponent {opponentHitActor}'s cap into SkellyBox {skellyBoxNumber}, applying penalty of {skellyBoxNumber} turns");
                                    capMovement.photonView.RPC("RPC_ApplyTurnPenalty", RpcTarget.All, opponentHitActor, skellyBoxNumber);
                                    capMovement.hasAppliedPenaltyThisTurn = true;
                                    return true;
                                }
                                else
                                {
                                    Debug.Log($"Opponent {opponentHitActor} already has a penalty this turn, skipping SkellyBox penalty");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"Failed to parse SkellyBox number from name: {boxName}");
                            }
                        }
                    }
                    return false;
                }
            }
        }
        Debug.Log($"No cap found for opponent {opponentHitActor}");
        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasWon || !photonView.IsMine) return;

        if (other.CompareTag("BoardBox") && int.TryParse(other.gameObject.name, out int boxNumber))
        {
            currentBoxEntered = boxNumber;
            Debug.Log($"Cap entered box {boxNumber}, current target: {currentBox}, position: {transform.position}, owner: {photonView.Owner?.ActorNumber}");
        }
        else if (other.CompareTag("SkellyBox"))
        {
            string boxName = other.gameObject.name;
            string numberPart = boxName.Replace("SkellyBox", "");
            if (int.TryParse(numberPart, out int skellyBoxNumber))
            {
                if (!hasAppliedPenaltyThisTurn)
                {
                    Debug.Log($"Player {PhotonNetwork.LocalPlayer.ActorNumber} entered SkellyBox {skellyBoxNumber}, applying penalty of {skellyBoxNumber} turns");
                    photonView.RPC("RPC_ApplyTurnPenalty", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, skellyBoxNumber);
                    hasAppliedPenaltyThisTurn = true;
                }
                currentBoxEntered = skellyBoxNumber;
            }
            else
            {
                Debug.LogWarning($"Failed to parse SkellyBox number from name: {boxName}. Expected format: SkellyBoxN (e.g., SkellyBox4)");
            }
        }
        else if (other.CompareTag("BoxLine"))
        {
            isTouchingBoxLine = true;
            Debug.Log($"Cap entered BoxLine at position: {transform.position}, owner: {photonView.Owner?.ActorNumber}");
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (hasWon) return;

        if (other.CompareTag("BoardBox") && int.TryParse(other.gameObject.name, out int boxNumber))
        {
            currentBoxEntered = boxNumber;
            Debug.Log($"Cap staying in box {boxNumber}, current target: {currentBox}, position: {transform.position}, owner: {photonView.Owner?.ActorNumber}");
        }
        else if (other.CompareTag("SkellyBox"))
        {
            string boxName = other.gameObject.name;
            string numberPart = boxName.Replace("SkellyBox", "");
            if (int.TryParse(numberPart, out int skellyBoxNumber))
            {
                currentBoxEntered = skellyBoxNumber;
                Debug.Log($"Cap staying in SkellyBox {skellyBoxNumber}, current target: {currentBox}, position: {transform.position}, owner: {photonView.Owner?.ActorNumber}");
            }
            else
            {
                Debug.LogWarning($"Failed to parse SkellyBox number from name: {boxName}");
            }
        }
        else if (other.CompareTag("BoxLine"))
        {
            isTouchingBoxLine = true;
            Debug.Log($"Cap staying in BoxLine at position: {transform.position}, owner: {photonView.Owner?.ActorNumber}");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (hasWon) return;

        if (other.CompareTag("BoardBox") && int.TryParse(other.gameObject.name, out int boxNumber))
        {
            if (boxNumber == currentBoxEntered)
            {
                Debug.Log($"Cap exited box {boxNumber}, position: {transform.position}, velocity: {rb.velocity.magnitude}, owner: {photonView.Owner?.ActorNumber}");
                currentBoxEntered = 0;
            }
        }
        else if (other.CompareTag("SkellyBox"))
        {
            string boxName = other.gameObject.name;
            if (int.TryParse(boxName.Replace("SkellyBox", ""), out int skellyBoxNumber))
            {
                if (skellyBoxNumber == currentBoxEntered)
                {
                    Debug.Log($"Cap exited SkellyBox {skellyBoxNumber}, position: {transform.position}, velocity: {rb.velocity.magnitude}, owner: {photonView.Owner?.ActorNumber}");
                    currentBoxEntered = 0;
                }
            }
        }
        else if (other.CompareTag("BoxLine"))
        {
            isTouchingBoxLine = false;
            Debug.Log($"Cap exited BoxLine at position: {transform.position}, owner: {photonView.Owner?.ActorNumber}");
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!photonView.IsMine || hasWon || hasResetThisFlick || !TurnManager.Instance.IsMyTurn()) return;

        if (collision.gameObject.CompareTag("BottleCap"))
        {
            collidedWithCap = true;
            CapMovement opponentCap = collision.gameObject.GetComponent<CapMovement>();
            if (opponentCap != null && opponentCap.photonView != null && opponentCap.photonView.Owner != null)
            {
                int opponentActorNumber = opponentCap.photonView.Owner.ActorNumber;
                int currentTurnPlayer = GetCurrentTurnPlayer();
                if (opponentActorNumber != currentTurnPlayer)
                {
                    opponentHitActor = opponentActorNumber;
                    Debug.Log($"Player {currentTurnPlayer} collided with opponent {opponentHitActor}'s cap at position: {transform.position}");
                }
                else
                {
                    Debug.Log($"Player {currentTurnPlayer} collided with own cap, ignoring for opponent rule");
                }
            }
            else
            {
                Debug.LogWarning($"Opponent cap has invalid CapMovement or PhotonView: {collision.gameObject.name}");
            }
        }
    }

    private bool CheckOpponentCapInCorrectBox()
    {
        if (opponentHitActor == -1 || !collidedWithCap) return false;

        GameObject[] caps = GameObject.FindGameObjectsWithTag("BottleCap");
        foreach (GameObject cap in caps)
        {
            CapMovement capMovement = cap.GetComponent<CapMovement>();
            if (capMovement != null && capMovement.photonView != null && capMovement.photonView.Owner != null)
            {
                if (capMovement.photonView.Owner.ActorNumber == opponentHitActor)
                {
                    bool inCorrectBox = capMovement.currentBoxEntered == capMovement.currentBox && capMovement.currentBoxEntered != 0;
                    Debug.Log($"Checking opponent {opponentHitActor}'s cap: currentBox={capMovement.currentBox}, currentBoxEntered={capMovement.currentBoxEntered}, inCorrectBox={inCorrectBox}, position={cap.transform.position}, velocity={capMovement.rb.velocity.magnitude}");
                    if (inCorrectBox)
                    {
                        int boxesToAdvance = capMovement.isTouchingBoxLine ? 1 : 3;
                        capMovement.photonView.RPC("RPC_AdvanceBox", RpcTarget.All, opponentHitActor, boxesToAdvance);
                        Debug.Log($"Opponent {opponentHitActor} landed in their correct box {capMovement.currentBox}, advancing {boxesToAdvance} box(es)");
                        return true;
                    }
                    return false;
                }
            }
        }
        Debug.Log($"No cap found for opponent {opponentHitActor}");
        return false;
    }

    private int GetCurrentTurnPlayer()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogError("CurrentRoom is null, cannot get turn player");
            return -1;
        }
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TurnManager.TURN_KEY, out object turnObj))
        {
            int turnPlayer = (int)turnObj;
            Debug.Log($"Current turn player: {turnPlayer}");
            return turnPlayer;
        }
        Debug.LogError("TURN_KEY not found in room properties");
        return -1;
    }

    private void FindAndAdvancePlayerCap(int actorNumber, int boxesToAdvance)
    {
        GameObject[] caps = GameObject.FindGameObjectsWithTag("BottleCap");
        foreach (GameObject cap in caps)
        {
            CapMovement capMovement = cap.GetComponent<CapMovement>();
            if (capMovement != null && capMovement.photonView != null && capMovement.photonView.Owner != null)
            {
                if (capMovement.photonView.Owner.ActorNumber == actorNumber)
                {
                    if (!capMovement.hasWon)
                    {
                        capMovement.photonView.RPC("RPC_AdvanceBox", RpcTarget.All, actorNumber, boxesToAdvance);
                        Debug.Log($"Advancing cap for player {actorNumber} by {boxesToAdvance} boxes to box {capMovement.currentBox + boxesToAdvance}");
                        break;
                    }
                    else
                    {
                        Debug.Log($"Player {actorNumber}'s cap has already won, skipping advancement");
                    }
                }
            }
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
    private void RPC_ApplyTurnPenalty(int actorNumber, int penaltyTurns)
    {
        if (photonView.Owner.ActorNumber != actorNumber) return;
        if (GameManager.Instance != null && GameManager.Instance.photonView != null)
        {
            GameManager.Instance.photonView.RPC("RPC_ApplyTurnPenalty", RpcTarget.All, actorNumber, penaltyTurns);
            Debug.Log($"CapMovement: Forwarded penalty of {penaltyTurns} turns for player {actorNumber} to GameManager");
        }
        else
        {
            Debug.LogError("RPC_ApplyTurnPenalty: GameManager or its PhotonView is null");
        }
    }

    [PunRPC]
    private void RPC_ResetProgress(int actorNumber)
    {
        if (photonView.Owner.ActorNumber != actorNumber) return;

        currentBox = 1;
        hasWon = false;
        UpdateTargetBoxText();

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
            transform.position = GameManager.Instance.spawnPoints.Length > 0 ? GameManager.Instance.spawnPoints[0].position : Vector3.zero;
            rb.velocity = Vector2.zero;
            Debug.LogWarning($"Player {actorNumber} moved to fallback position {transform.position}");
        }
    }

    [PunRPC]
    private void RPC_AnnounceWinner(int actorNumber)
    {
        Debug.Log($"Player {actorNumber} has won by completing Around the World!");
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
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.selectedMode == GameMode.OneVsOne)
        {
            spawnIndex = actorNumber % 2;
        }
        else if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo)
        {
            int team = TurnManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            spawnIndex = (team - 1) * 2 + (actorNumber % 2);
        }
        else
        {
            spawnIndex = actorNumber % spawnPointCount;
        }

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

        Debug.Log($"GetSpawnIndex for player {actorNumber}: mode={(MultiplayerManager.Instance != null ? MultiplayerManager.Instance.selectedMode : "None")}, team={(MultiplayerManager.Instance != null && MultiplayerManager.Instance.selectedMode == GameMode.TeamTwoVsTwo ? TurnManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer) : 0)}, spawnIndex={spawnIndex}, spawnPointCount={spawnPointCount}");
        return spawnIndex;
    }

}