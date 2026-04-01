using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceGatherNode : MonoBehaviour, IMouseInteraction
{
    [Header("Common")]
    [SerializeField] private Acquisition acquisitionType;   // 채집 타입
    [SerializeField] private Transform[] interactPoints;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AudioClip gatherLoopSound;
    [SerializeField] private Sprite fallbackSprite;
    [SerializeField] private int fallbackTime = 3;
    [SerializeField] private bool destroyAfterGather = true;

    [Header("Logging Option")]
    [SerializeField] private bool useLoggingVisual = false;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private AudioClip finishSound;
    [SerializeField] private GameObject arrow;

    [Header("Bonus Drop Option")]
    [SerializeField] private bool useBonusDrop = false;
    [SerializeField] private MaterialType bonusMaterialType;
    [SerializeField, Range(0, 100)] private int bonusPercent = 0;
    [SerializeField] private int bonusAmount = 1;

    [Header("Candidate Filter")]
    [SerializeField] private bool useItemIdFilter = false;
    [SerializeField] private int[] allowedItemIds;

    [Header("Extra Gauge Option")]
    [SerializeField] private bool addRecoveryGauge = false;
    [SerializeField] private int defaultGaugeUpValue = 0;

    [Header("Piece Card Option")]
    [SerializeField] private bool usePieceCardDrop = false;
    [SerializeField] private DiabolicItemInfo[] pieceList;

    private bool canInteract = false;
    private bool isGathering = false;
    private int interactIndex;

    private Character character;
    private GameManager gameManager;
    private GamesceneManager gamesceneManager;
    private SoundManager soundManager;
    private GameSceneUI gameSceneUI;

    private EffectSound currentSfx;
    private Color outlineColor;
    private Vector3 obstacleAngle;

    // 이번 상호작용에서 확정된 드랍/시간
    private int pendingItemId = -1;
    private int pendingWaitTime = 3;

    private List<Item> candidates = new List<Item>();
    private List<DiabolicItemInfo> pieceCandidates = new List<DiabolicItemInfo>();

    private void Start()
    {
        character = Character.Instance;
        gameManager = GameManager.Instance;
        gamesceneManager = GamesceneManager.Instance;
        soundManager = SoundManager.Instance;
        gameSceneUI = GameSceneUI.Instance;

        if (spriteRenderer != null)
            outlineColor = spriteRenderer.material.GetColor("_SolidOutline");

        CacheCandidates();
    }

    private void Update()
    {
        if (gamesceneManager.isNight)
        {
            if (canInteract)
                canInteract = false;
            else if (isGathering)
                Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        isGathering = false;

        if (character != null)
        {
            character.isCanControll = true;
            character.canFlip = true;
        }

        if (currentSfx != null)
            soundManager.StopLoopSFX(currentSfx);
    }

    public void CanInteraction(bool _canInteraction)
    {
        canInteract = _canInteraction;
    }

    public bool ReturnCanInteraction()
    {
        return canInteract;
    }

    public void InteractionLeftButtonFuc(GameObject hitObject)
    {
        if (!canInteract) return;

        canInteract = false;

        if (arrow != null)
            arrow.SetActive(false);

        interactIndex = GetInteractIndex();
        obstacleAngle = GetObstacleAngle();

        // 1) 이번 드랍 미리 결정
        pendingItemId = PickItemId();
        if (pendingItemId == -1)
        {
            // fallback
            if (acquisitionType == Acquisition.Bush)
                pendingItemId = gameManager.idByMaterialType[MaterialType.Fruit];
            else if (acquisitionType == Acquisition.Logging)
                pendingItemId = gameManager.idByMaterialType[MaterialType.Wood];
        }

        // 2) 이번 시간 결정
        pendingWaitTime = GetGatherTime(pendingItemId);

        Debug.Log($"[ResourceGatherNode] acq={acquisitionType}, pendingItemId={pendingItemId}, pendingWaitTime={pendingWaitTime}");

        // 3) 이동 후 상호작용 시작
        Vector3 targetPos = interactPoints != null && interactPoints.Length > 0
            ? interactPoints[Mathf.Clamp(interactIndex, 0, interactPoints.Length - 1)].position
            : transform.position;

        // Character 쪽이 waitTime을 그대로 EndInteraction으로 넘기도록 수정돼 있어야 함
        character.MoveToInteractableObject(
            targetPos,
            gameObject,
            pendingWaitTime,
            3,
            useLoggingVisual ? 1 : -1,
            useLoggingVisual ? interactIndex : -1
        );

        if (useLoggingVisual && interactIndex == 0 && spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 0.4f;
            spriteRenderer.color = color;
        }
    }

    public IEnumerator EndInteraction(Animator anim, float waitTime)
    {
        isGathering = true;

        if (acquisitionType == Acquisition.Logging && gameManager.specialStatus[SpecialStatus.DoubleAxe])
            waitTime *= 0.5f;

        if (gatherLoopSound != null)
            currentSfx = soundManager.PlaySFXAndReturn(gatherLoopSound, true);

        yield return CoroutineCaching.WaitForSeconds(waitTime);

        if (gamesceneManager.isNight)
            yield break;

        isGathering = false;

        character.isCanControll = true;
        character.canFlip = true;

        if (currentSfx != null)
            soundManager.StopLoopSFX(currentSfx);

        if (finishSound != null)
            soundManager.PlaySFX(finishSound);

        // 메인 드랍 지급
        int amount = GetMainDropAmount();
        GiveMainDrop(amount);

        // 보너스 드랍
        HandleBonusDrop();

        // 조각 카드
        if (usePieceCardDrop)
            TryDropPieceCard();

        // 회복 게이지
        if (addRecoveryGauge)
            AddRecoveryGauge(amount);

        anim.SetBool("isLogging", false);
        character.ChangeAnimationController(0);

        HandleAfterGather();
    }

    // -----------------------------
    // 후보 캐시 / 아이템 선택
    // -----------------------------
    private void CacheCandidates()
    {
        candidates = gameManager.itemDatas.FindAll(it =>
            it != null &&
            it.AcquisitionList != null &&
            it.AcquisitionList.Contains(acquisitionType) &&
            it.takePercentByAcquisition != null &&
            it.takePercentByAcquisition.ContainsKey(acquisitionType) &&
            PassCandidateFilter(it)
        );
    }

    private bool PassCandidateFilter(Item it)
    {
        if (!useItemIdFilter) return true;
        if (allowedItemIds == null || allowedItemIds.Length == 0) return true;

        for (int i = 0; i < allowedItemIds.Length; i++)
        {
            if (it.ItemId == allowedItemIds[i])
                return true;
        }
        return false;
    }

    private int PickItemId()
    {
        if (candidates == null || candidates.Count == 0)
            CacheCandidates();

        if (candidates == null || candidates.Count == 0)
            return -1;

        int total = 0;
        foreach (var it in candidates)
            total += Mathf.Max(0, it.takePercentByAcquisition[acquisitionType]);

        if (total <= 0) return -1;

        int roll = Random.Range(1, total + 1);
        int acc = 0;

        foreach (var it in candidates)
        {
            acc += Mathf.Max(0, it.takePercentByAcquisition[acquisitionType]);
            if (roll <= acc)
                return it.ItemId;
        }

        return -1;
    }

    private int GetGatherTime(int itemId)
    {
        Item picked = gameManager.itemDatas.Find(x => x.ItemId == itemId);
        if (picked != null &&
            picked.takeTimeByAcquisition != null &&
            picked.takeTimeByAcquisition.TryGetValue(acquisitionType, out int t))
        {
            // 방어: 시간 칸에 잘못된 아이디 값 들어간 경우 무시
            if (t >= 1 && t <= 30)
                return t;
        }

        return fallbackTime;
    }

    // -----------------------------
    // 메인 드랍 / 보너스 드랍
    // -----------------------------
    private int GetMainDropAmount()
    {
        if (acquisitionType == Acquisition.Logging)
            return Random.Range(1, 6);

        if (acquisitionType == Acquisition.Bush)
            return Random.Range(1, 5);

        return 1;
    }

    private void GiveMainDrop(int amount)
    {
        if (pendingItemId == -1) return;

        gameManager.AddItemById(pendingItemId, amount);

        Sprite icon = Resources.Load<Sprite>($"Item/{pendingItemId}");
        if (icon == null) icon = fallbackSprite;

        character.getItemUI.GetComponent<GetItemUI>().SetGetItemImage(icon, amount);
        character.getItemUI.gameObject.SetActive(true);
    }

    private void HandleBonusDrop()
    {
        if (!useBonusDrop) return;
        if (bonusPercent <= 0) return;

        if (Random.Range(0, 100) < bonusPercent)
        {
            int bonusId = gameManager.idByMaterialType[bonusMaterialType];
            gameManager.AddItemById(bonusId, bonusAmount);
        }
    }

    // -----------------------------
    // 조각 카드
    // -----------------------------
    private void TryDropPieceCard()
    {
        if (pieceList == null || pieceList.Length == 0) return;

#if UNITY_EDITOR
        bool canDrop = true;
#else
        bool canDrop = Random.Range(0, 100) >= 100 - GameManager.Instance.pieceCardGetRate;
#endif

        if (!canDrop) return;

        pieceCandidates.Clear();

        for (int i = 0; i < pieceList.Length; ++i)
        {
            DiabolicItemInfo info = pieceList[i];
            int itemId = info.ItemNum;

            int curCount = 0;
            gameManager.haveItems.TryGetValue(itemId, out curCount);

            if (curCount < info.MaxCount)
                pieceCandidates.Add(info);
        }

        if (pieceCandidates.Count <= 0)
            return;

        int totalWeightValue = 0;
        for (int i = 0; i < pieceCandidates.Count; ++i)
            totalWeightValue += pieceCandidates[i].WeightValue;

        int rand = Random.Range(0, totalWeightValue);
        float total = 0;

        for (int i = 0; i < pieceCandidates.Count; i++)
        {
            total += pieceCandidates[i].WeightValue;
            if (rand < total)
            {
                gameSceneUI.ShowPieceCard(pieceCandidates[i]);
                gameManager.AddItemById(pieceCandidates[i].ItemNum, 1);
                break;
            }
        }
    }

    // -----------------------------
    // 회복 게이지
    // -----------------------------
    private void AddRecoveryGauge(int amount)
    {
        character.currentRecoveryGauge = Mathf.Clamp(
            character.currentRecoveryGauge + defaultGaugeUpValue * amount,
            0,
            character.maxRecoveryGauge
        );
    }

    // -----------------------------
    // 후처리
    // -----------------------------
    private void HandleAfterGather()
    {
        if (useLoggingVisual && obstaclePrefab != null)
        {
            GameObject ob = Instantiate(
                obstaclePrefab,
                transform.position,
                Quaternion.Euler(obstacleAngle),
                GamesceneManager.Instance.treeParent
            );

            Obstacle obstacle = ob.GetComponentInChildren<Obstacle>();
            if (obstacle != null)
                obstacle.SetObstacleImage(interactIndex);
        }

        if (destroyAfterGather)
            Destroy(gameObject);
    }

    // -----------------------------
    // 방향 / 인덱스 계산
    // -----------------------------
    private int GetInteractIndex()
    {
        if (interactPoints == null || interactPoints.Length <= 1)
            return 0;

        return (character.transform.position - transform.position).x > 0 ? 0 : 1;
    }

    private Vector3 GetObstacleAngle()
    {
        if (!useLoggingVisual)
            return Vector3.zero;

        Vector3 logDir = (Character.Instance.transform.position - transform.position).normalized;
        float angle = Mathf.Atan2(logDir.z, logDir.x) * Mathf.Rad2Deg;

        if (angle >= 0 && angle < 90)
        {
            interactIndex = 3;
            angle = 180;
        }
        else if (angle >= 90 && angle < 180)
        {
            interactIndex = 0;
            angle = -90;
        }
        else if (angle >= -90 && angle < 0)
        {
            interactIndex = 1;
            angle = 90;
        }
        else if (angle >= -180 && angle < -90)
        {
            interactIndex = 2;
            angle = 0;
        }

        return new Vector3(90, 0, angle);
    }

    public void InteractionRightButtonFuc(GameObject hitObject) { }

    // -----------------------------
    // 마우스 외곽선
    // -----------------------------
    private void OnMouseOver()
    {
        if (spriteRenderer == null) return;

        if (canInteract)
        {
            if (outlineColor.a == 1) return;
            outlineColor.a = 1;
            spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
        }
        else
        {
            if (outlineColor.a == 0) return;
            outlineColor.a = 0;
            spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
        }
    }

    private void OnMouseExit()
    {
        if (spriteRenderer == null) return;
        if (outlineColor.a == 0) return;

        outlineColor.a = 0;
        spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
    }
}