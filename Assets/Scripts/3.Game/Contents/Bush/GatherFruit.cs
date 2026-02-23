using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GatherFruit : MonoBehaviour, IMouseInteraction
{
    [SerializeField] Transform[] gatherPoint;
    [SerializeField] int defaultGaugeUpValue;
    [SerializeField] DiabolicItemInfo[] bushPieceList;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Sprite fruitImage;
    [SerializeField] AudioClip gatheringSound;

    bool canGather;

    Character character;
    GameSceneUI gameSceneUI;
    GamesceneManager gamesceneManager;
    SoundManager soundManager;
    GameManager gameManager; // 추가 - haveItems를 쓰기 위해

    List<DiabolicItemInfo> itemList = new List<DiabolicItemInfo>();
    List<Item> bushFruitCandidates;

    Color outlineColor;

    EffectSound currentSfx;

    bool isGathering = false;
   
    int pendingFruitId = -1;
    int pendingGatherTime = 5; // 기본값

    private void Start()
    {
        canGather = false;
        character = Character.Instance;
        gameSceneUI = GameSceneUI.Instance;
        gamesceneManager = GamesceneManager.Instance;
        soundManager = SoundManager.Instance;
        gameManager = GameManager.Instance; // 추가
        outlineColor = spriteRenderer.material.GetColor("_SolidOutline");

    }

    private void Update()
    {
        if (isGathering && gamesceneManager.isNight)
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        isGathering = false;

        character.isCanControll = true;
        character.canFlip = true;

        if (currentSfx != null)
        {
            soundManager.StopLoopSFX(currentSfx);
        }
    }

    void GetRandomPiece()
    {
        itemList.Clear();

        // 수정 - ItemManager.getItems (DiabolicItemInfo 키) 대신
        // GameManager.haveItems (int itemId 키) 기준으로 “획득 가능 후보”를 만든다.
        for (int i = 0; i < bushPieceList.Length; ++i)
        {
            DiabolicItemInfo info = bushPieceList[i];
            int itemId = info.ItemNum;

            // beachItem과 동일한 방식: 있으면 수량 확인, 없으면 0으로 취급
            int curCount = 0;
            gameManager.haveItems.TryGetValue(itemId, out curCount);

            // MaxCount 미만이면 획득 후보에 포함
            if (curCount < info.MaxCount)
            {
                itemList.Add(info);
            }
        }

        if (itemList.Count <= 0)
        {
            return;
        }

        int totalWeightValue = 0;

        for (int i = 0; i < itemList.Count; ++i)
        {
            totalWeightValue += itemList[i].WeightValue;
        }

        int rand = Random.Range(0, totalWeightValue);

        float total = 0;

        for (int i = 0; i < itemList.Count; i++)
        {
            total += itemList[i].WeightValue;

            if (rand < total)
            {
                rand = i;
                break;
            }
        }

       
        gameSceneUI.ShowPieceCard(itemList[rand]);

        int getId = itemList[rand].ItemNum;
        gameManager.AddItemById(getId, 1);
    }

    public void CanInteraction(bool _canInteraction)
    {
        canGather = _canInteraction;
    }

    public void InteractionLeftButtonFuc(GameObject hitObject)
    {
        if (canGather)
        {
            canGather = false;

            int num = (character.transform.position - transform.position).x > 0 ? 0 : 1;

            // 1) 이번 채집 보상(열매 종류) 미리 결정
            pendingFruitId = PickBushFruitId();
            if (pendingFruitId == -1)
                pendingFruitId = gameManager.idByMaterialType[MaterialType.Fruit];

            // 2) 그 열매의 채집 시간 가져오기
            pendingGatherTime = 5; // fallback
            Item picked = gameManager.itemDatas.Find(x => x.ItemId == pendingFruitId);
            if (picked != null && picked.takeTimeByAcquisition != null &&
                picked.takeTimeByAcquisition.TryGetValue(Acquisition.Bush, out int t))
            {
                pendingGatherTime = Mathf.Max(1, t);
            }

            Debug.Log($"[GatherFruit] pickId={pendingFruitId}, pendingGatherTime={pendingGatherTime}");
            // 3) waitTime을 고정 5가 아니라 데이터 시간으로
            character.MoveToInteractableObject(gatherPoint[num].position, gameObject, pendingGatherTime, 3, -1, num);
        }
    }


    public IEnumerator EndInteraction(Animator anim, float waitTime)
    {
        Debug.Log($"[GatherFruit] EndInteraction waitTime={waitTime}");

        isGathering = true;

        currentSfx = soundManager.PlaySFXAndReturn(gatheringSound, true);

        yield return CoroutineCaching.WaitForSeconds(waitTime);
        
        if (gamesceneManager.isNight)
            yield break;

        isGathering = false;

        character.isCanControll = true;
        character.canFlip = true;

        if (currentSfx != null)
        {
            soundManager.StopLoopSFX(currentSfx);
        }

#if UNITY_EDITOR
        if (Random.Range(0, 100) >= 0)
            GetRandomPiece();
#else
        if (Random.Range(0, 100) >= 100 - GameManager.Instance.pieceCardGetRate)
            GetRandomPiece();
#endif

        RecoveryGaugeUp();

        anim.SetBool("isLogging", false);

        character.ChangeAnimationController(0);

        Destroy(gameObject);
    }

    void RecoveryGaugeUp()
    {
        
        int getFruitQuantity = Random.Range(1, 5);

        // 1) 채집 확률 테이블에서 열매 종류 1개 선택
        int fruitId = pendingFruitId;
        
        // 후보가 없으면 기존 Fruit로 fallback
        if (fruitId == -1)
        {
          fruitId = gameManager.idByMaterialType[MaterialType.Fruit];
        }
        // 2) 인벤에 지급
        gameManager.AddItemById(fruitId, getFruitQuantity);

        // 3) UI 아이콘
        Sprite icon = Resources.Load<Sprite>($"Item/{fruitId}");
        if (icon == null) icon = fruitImage; // fallback

        character.getItemUI.GetComponent<GetItemUI>().SetGetItemImage(icon, getFruitQuantity);
        character.getItemUI.gameObject.SetActive(true);

        // 4) 회복 게이지 증가
        character.currentRecoveryGauge = Mathf.Clamp(
            character.currentRecoveryGauge + defaultGaugeUpValue * getFruitQuantity,
            0,
            character.maxRecoveryGauge
        );

        pendingFruitId = -1;
    }
    void CacheBushFruitCandidates()
    {
        // 채집(Bush)로 얻을 수 있는 열매 후보만 추림
        bushFruitCandidates = gameManager.itemDatas.FindAll(it =>
            it.AcquisitionList != null &&
            it.AcquisitionList.Contains(Acquisition.Bush) &&
            it.takePercentByAcquisition != null &&
            it.takePercentByAcquisition.ContainsKey(Acquisition.Bush) &&
            it.Preytype == PreyType.FRUIT
        );
    }
    int PickBushFruitId()
    {
        if (bushFruitCandidates == null || bushFruitCandidates.Count == 0)
            CacheBushFruitCandidates();

        if (bushFruitCandidates == null || bushFruitCandidates.Count == 0)
            return -1;

        int total = 0;
        foreach (var it in bushFruitCandidates)
            total += Mathf.Max(0, it.takePercentByAcquisition[Acquisition.Bush]);

        if (total <= 0) return -1;

        int roll = Random.Range(1, total + 1);
        int acc = 0;

        foreach (var it in bushFruitCandidates)
        {
            acc += Mathf.Max(0, it.takePercentByAcquisition[Acquisition.Bush]);
            if (roll <= acc) return it.ItemId;
        }

        return -1;


    }



    public void InteractionRightButtonFuc(GameObject hitObject)
    {
    }

    public bool ReturnCanInteraction()
    {
        return canGather;
    }

    private void OnMouseOver()
    {
        if (canGather)
        {
            if (outlineColor.a == 1)
                return;

            outlineColor.a = 1;

            spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
        }
        else
        {
            if (outlineColor.a == 0)
                return;

            outlineColor.a = 0;

            spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
        }
    }

    private void OnMouseExit()
    {
        if (outlineColor.a == 0)
            return;

        outlineColor.a = 0;

        spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
    }
}
