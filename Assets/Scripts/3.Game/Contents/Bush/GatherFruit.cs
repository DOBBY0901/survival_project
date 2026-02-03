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

    Color outlineColor;

    EffectSound currentSfx;

    bool isGathering = false;

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

            character.MoveToInteractableObject(gatherPoint[num].position, gameObject, 3, 5, -1, num);
        }
    }

    public IEnumerator EndInteraction(Animator anim, float waitTime)
    {
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

        //추가 - 열매 아이템 ID를 가져와서 인벤에 누적
        int fruitId = gameManager.idByMaterialType[MaterialType.Fruit]; // MaterialType 이름이 다르면 그걸로 바꿈
        gameManager.AddItemById(fruitId, getFruitQuantity);             // 공통 방식(안전 누적)
        character.getItemUI.GetComponent<GetItemUI>().SetGetItemImage(fruitImage, getFruitQuantity);
        character.getItemUI.gameObject.SetActive(true);

        character.currentRecoveryGauge = Mathf.Clamp(
            character.currentRecoveryGauge + defaultGaugeUpValue * getFruitQuantity,
            0,
            character.maxRecoveryGauge
        );
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
