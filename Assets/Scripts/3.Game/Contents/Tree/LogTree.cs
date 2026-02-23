using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogTree : MonoBehaviour, IMouseInteraction
{
    [SerializeField] GameObject obstacle;
    [SerializeField] Transform[] logPoses;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] GameObject arrow;
    [SerializeField] Sprite woodSprite;
    [SerializeField] AudioClip loggingSound;
    [SerializeField] AudioClip instanceObstacleSound;

    bool canLog = false;
    [HideInInspector] int posNum;

    Character character;
    GameManager gameManager;
    GamesceneManager gamesceneManager;
    SoundManager soundManager;

    Vector3 obstacleAngle;
    Color outlineColor;
    EffectSound currentSfx;
    bool isLogging = false;

    // 추가: 이번 벌목에서 확정된 드랍/시간
    int pendingDropId = -1;
    int pendingLogTime = 3; // fallback

    // 후보 캐시
    static List<Item> loggingCandidates;
    static Dictionary<int, int> branchChanceByMainId;

    private void Start()
    {
        canLog = false;
        character = Character.Instance;
        gameManager = GameManager.Instance;
        gamesceneManager = GamesceneManager.Instance;
        soundManager = SoundManager.Instance;

        outlineColor = spriteRenderer.material.GetColor("_SolidOutline");

        CacheLoggingCandidates();
        CacheBranchDropRule(); // 옵션
    }

    void CacheLoggingCandidates()
    {
        if (loggingCandidates != null && loggingCandidates.Count > 0) return;

        loggingCandidates = gameManager.itemDatas.FindAll(it =>
            it.AcquisitionList != null &&
            it.AcquisitionList.Contains(Acquisition.Logging) &&
            it.takePercentByAcquisition != null &&
            it.takePercentByAcquisition.ContainsKey(Acquisition.Logging)
        );
    }

    int PickLoggingDropId()
    {
        if (loggingCandidates == null || loggingCandidates.Count == 0)
            CacheLoggingCandidates();

        if (loggingCandidates == null || loggingCandidates.Count == 0)
            return -1;

        int total = 0;
        foreach (var it in loggingCandidates)
            total += Mathf.Max(0, it.takePercentByAcquisition[Acquisition.Logging]);

        if (total <= 0) return -1;

        int roll = Random.Range(1, total + 1);
        int acc = 0;

        foreach (var it in loggingCandidates)
        {
            acc += Mathf.Max(0, it.takePercentByAcquisition[Acquisition.Logging]);
            if (roll <= acc) return it.ItemId;
        }

        return -1;
    }

   
    void CacheBranchDropRule()
    {
        if (branchChanceByMainId != null) return;

        branchChanceByMainId = new Dictionary<int, int>();

        const int BRANCH_ID = 2030003;

        Item branch = gameManager.itemDatas.Find(x => x.ItemId == BRANCH_ID);
        if (branch == null) return;
        if (branch.takeTimeByAcquisition == null || branch.takePercentByAcquisition == null) return;

        branchChanceByMainId[-999] = 30;
    }

    int GetBranchChanceForMain(int mainId)
    {
        if (branchChanceByMainId == null) return 0;

       
        if (branchChanceByMainId.TryGetValue(mainId, out int ch)) return ch;
        if (branchChanceByMainId.TryGetValue(-999, out int common)) return common;

        return 0;
    }

    private void Update()
    {
        if (gamesceneManager.isNight)
        {
            if (canLog) canLog = false;
            else if (isLogging) Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (currentSfx != null)
            soundManager.StopLoopSFX(currentSfx);

        isLogging = false;
        character.canFlip = true;
        character.isCanControll = true;
    }

    Vector3 LogAngle()
    {
        Vector3 logDir = (Character.Instance.transform.position - transform.position).normalized;
        float angle = Mathf.Atan2(logDir.z, logDir.x) * Mathf.Rad2Deg;

        if (angle >= 0 && angle < 90) { posNum = 3; angle = 180; }
        else if (angle >= 90 && angle < 180) { posNum = 0; angle = -90; }
        else if (angle >= -90 && angle < 0) { posNum = 1; angle = 90; }
        else if (angle >= -180 && angle < -90) { posNum = 2; angle = 0; }

        return new Vector3(90, 0, angle);
    }

    public void InteractionLeftButtonFuc(GameObject hitObject)
    {
        if (!canLog) return;

        arrow.SetActive(false);
        obstacleAngle = LogAngle();
        canLog = false;

        //1) 이번 벌목 드랍 먼저 확정
        pendingDropId = PickLoggingDropId();
        if (pendingDropId == -1)
        {
            
            pendingDropId = gameManager.idByMaterialType[MaterialType.Wood];
        }

        
        pendingLogTime = 3; // fallback
        Item picked = gameManager.itemDatas.Find(x => x.ItemId == pendingDropId);
        if (picked != null && picked.takeTimeByAcquisition != null &&
            picked.takeTimeByAcquisition.TryGetValue(Acquisition.Logging, out int t))
        {
            pendingLogTime = Mathf.Max(1, t);
        }

    
        Debug.Log($"[LogTree] pendingDropId={pendingDropId}, pendingLogTime={pendingLogTime}");

        character.MoveToInteractableObject(logPoses[posNum].position, gameObject, pendingLogTime, 3, 1, posNum);

        if (posNum == 0)
        {
            Color color = spriteRenderer.color;
            color.a = 0.4f;
            spriteRenderer.color = color;
        }
    }

    void SpawnObstacle()
    {
        GameObject ob = Instantiate(obstacle, transform.position, Quaternion.Euler(obstacleAngle), GamesceneManager.Instance.treeParent);
        ob.GetComponentInChildren<Obstacle>().SetObstacleImage(posNum);
        Destroy(gameObject);
    }

    public void CanInteraction(bool _canInteraction) => canLog = _canInteraction;

    public IEnumerator EndInteraction(Animator anim, float waitTime)
    {
        isLogging = true;

        if (gameManager.specialStatus[SpecialStatus.DoubleAxe])
            waitTime *= 0.5f;

        currentSfx = soundManager.PlaySFXAndReturn(loggingSound, true);

        yield return CoroutineCaching.WaitForSeconds(waitTime);

        isLogging = false;

        character.isCanControll = true;
        character.canFlip = true;

        if (currentSfx != null)
            soundManager.StopLoopSFX(currentSfx);

        if (gamesceneManager.isNight)
            yield break;

        soundManager.PlaySFX(instanceObstacleSound);

        // 지급
        int amount = Random.Range(1, 6);
        gameManager.AddItemById(pendingDropId, amount);

        // UI 아이콘은 드랍 아이템 기준
        Sprite icon = Resources.Load<Sprite>($"Item/{pendingDropId}");
        if (icon == null) icon = woodSprite;
        character.getItemUI.GetComponent<GetItemUI>().SetGetItemImage(icon, amount);
        character.getItemUI.gameObject.SetActive(true);

        //나뭇가지 추가 드랍
        int branchChance = GetBranchChanceForMain(pendingDropId);
        if (branchChance > 0 && Random.Range(0, 100) < branchChance)
        {
            int branchId = gameManager.idByMaterialType[MaterialType.Branch];
            gameManager.AddItemById(branchId, 1);
            // 필요하면 여기서도 GetItemUI를 띄우거나, 누적 표시 방식으로 변경
        }

        anim.SetBool("isLogging", false);
        character.ChangeAnimationController(0);

        SpawnObstacle();
    }

    public void InteractionRightButtonFuc(GameObject hitObject) { }
    public bool ReturnCanInteraction() => canLog;

    private void OnMouseOver()
    {
        if (canLog)
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
        if (outlineColor.a == 0) return;
        outlineColor.a = 0;
        spriteRenderer.material.SetColor("_SolidOutline", outlineColor);
    }
}