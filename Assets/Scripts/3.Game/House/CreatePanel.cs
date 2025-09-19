using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreatePanel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI itemName;
    [SerializeField] Image itemImage;
    [SerializeField] Transform itemMaterialSlot;
    [SerializeField] GameObject createMaterialSlotPrefab;
    [SerializeField] TextMeshProUGUI createCountText;
    [SerializeField] Transform createItemListParent;
    [SerializeField] GameObject createItemSlotPrefab;
    [SerializeField] GameObject CreateSuccessPanel;

    GameManager gameManager;

    Item item;

    int createCount = 1;
    bool canCreate = false;

    Acquisition acquisition;

    int createType = 0;

    private void Awake()
    {
        gameManager = GameManager.Instance;
        createCountText.text = $"제작 개수:\t{createCount}";
    }

    public void OpenFor(Acquisition acq, int type = 0)
    {
        acquisition = acq;
        createType = type;
        createCount = 1;
        createCountText.text = $"제작 개수:\t{createCount}";

        UpdateCreateItemList();     // 매개변수 없이 내부 상태(acquisition, createType)로 빌드
        SetCreateItemPanelInfo();   // 선택 아이템에 맞춰 패널 갱신
        gameObject.SetActive(true);
    }


    public void SetCreateAcquisition(Acquisition acquisition)
    {
        this.acquisition = acquisition;
    }

    public void SelectCreateItemType(int num)
    {
        createType = num;

        UpdateCreateItemList();
    }

    public void UpdateCreateItemList()
    {
        // 1) 현재 제작대 종류 + 탭(타입)으로 필터링한 "표시용 리스트"를 먼저 만든다.
        var filtered = gameManager.itemDatas
            .Where(x => x.AcquisitionList.Contains(acquisition) && x.Type == (ItemType)createType)
            .ToList();

        // 2) 기존 슬롯 전부 비활성화
        for (int i = 0; i < createItemListParent.childCount; ++i)
            createItemListParent.GetChild(i).gameObject.SetActive(false);

        // 3) 필터링된 리스트 기준으로 0..N-1 인덱스를 사용해 슬롯 구성
        for (int j = 0; j < filtered.Count; j++)
        {
            var data = filtered[j];

            GameObject slotGO;
            if (createItemListParent.childCount <= j)
                slotGO = Instantiate(createItemSlotPrefab, createItemListParent);
            else
            {
                slotGO = createItemListParent.GetChild(j).gameObject;
                slotGO.SetActive(true);
            }

            // 아이콘/이름
            slotGO.transform.Find("ItemImage").GetComponent<Image>().sprite =
                Resources.Load<Sprite>($"Item/{data.ImageId}");
            slotGO.transform.Find("ItemName").GetComponent<TextMeshProUGUI>().text = data.ItemName;

            // 버튼 리스너 '모두 제거' 후, 현재 아이템만 등록 (중첩 방지)
            var btn = slotGO.transform.Find("Button").GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                item = data;
                SetCreateItemPanelInfo();
            });
        }

        // 4) 기본 선택
        item = filtered.Count > 0 ? filtered[0] : null;
    }

    public void SetCreateItemPanelInfo()
    {
        // 선택 아이템이 없으면 패널만 초기화하고 끝
        if (item == null)
        {
            itemName.text = "";
            itemImage.sprite = null;
            canCreate = false;

            for (int i = 0; i < itemMaterialSlot.childCount; ++i)
                itemMaterialSlot.GetChild(i).gameObject.SetActive(false);
            return;
        }

        itemName.text = item.ItemName;
        itemImage.sprite = Resources.Load<Sprite>($"Item/{item.ImageId}");

        canCreate = true;

        int index = 0;

        for(int i = 0; i< itemMaterialSlot.childCount; ++i)
            itemMaterialSlot.GetChild(i).gameObject.SetActive(false);

        foreach (var material in item.NeedMaterials)
        {
            GameObject materialPrefab = null;

            if (itemMaterialSlot.childCount <= index)
                materialPrefab = Instantiate(createMaterialSlotPrefab, itemMaterialSlot);
            else
            {
                materialPrefab = itemMaterialSlot.GetChild(index).gameObject;
                materialPrefab.SetActive(true);
            }

            materialPrefab.transform.Find("MatImage").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Item/{GameManager.Instance.idByMaterialType[material.Key]}");
            TextMeshProUGUI countText = materialPrefab.transform.Find("Count").GetComponent<TextMeshProUGUI>();
            int haveItemAmount = !gameManager.haveItems.ContainsKey(gameManager.idByMaterialType[material.Key]) ? 0 : gameManager.haveItems[gameManager.idByMaterialType[material.Key]];
            countText.text = $"{material.Value * createCount} / {haveItemAmount}";
            countText.color = haveItemAmount < material.Value * createCount ? Color.red : Color.white;

            if (canCreate)
                canCreate = haveItemAmount >= material.Value * createCount;

            index++;
        }
    }

    public void UpDownCreateCount(int num)
    {
        createCount += num;

        if (createCount < 1)
        {
            createCount = 1;
            return;
        }

        SetCreateItemPanelInfo();
        createCountText.text = $"제작 개수:\t{createCount}";
    }

    public void CreateItem()
    {
        //int index = transform.GetSiblingIndex();

        if (!canCreate || createCount <= 0)
            return;

        foreach (var material in item.NeedMaterials)
        {
            int need = material.Value * createCount;
            int have = gameManager.haveItems[gameManager.idByMaterialType[material.Key]];
            if (have < need) return;
        }

        foreach (var material in item.NeedMaterials)
        {
            int id = gameManager.idByMaterialType[material.Key];
            int need = material.Value * createCount;
            gameManager.haveItems[id] -= need;
            Debug.Log($"{gameManager.itemInfos[id].itemName}: {need}개 소모");
        }

        StartCoroutine(CraftRoutine());
    }

    public void CreateItem(Item item)
    {
        foreach (var material in item.NeedMaterials)
        {
            if (!gameManager.haveMaterials.ContainsKey(material.Key))
                return;

            else if (gameManager.haveMaterials[material.Key] < material.Value)
                return;
        }

        foreach (var material in item.NeedMaterials)
        {
            gameManager.haveMaterials[material.Key] -= material.Value;
            Debug.Log($"{gameManager.itemInfos[GameManager.Instance.idByMaterialType[material.Key]].itemName}: {material.Value}개 소모");
        }

        item.AddItem();
        Debug.Log($"{item.ItemName}을 제작했습니다.");
    }
    private IEnumerator CraftRoutine()
    {
        float craftTime = item.CreateTime; // 인스펙터에서 설정한 제작시간(초)

        for (int i = 0; i < createCount; i++)
        {
            if (craftTime > 0f)
                yield return CoroutineCaching.WaitForSeconds(craftTime);

            GameManager.Instance.haveItems[item.ItemId] += 1;
            Debug.Log($"[제작 완료] {item.ItemName} 1개 제작 (소요시간 {craftTime}초)");
        }

        SetCreateItemPanelInfo();
        CreateSuccessPanel.transform.Find("BackPanel/ItemImage")
            .GetComponent<Image>().sprite = Resources.Load<Sprite>($"Item/{item.ImageId}");
        CreateSuccessPanel.transform.Find("BackPanel/CreateCount")
            .GetComponent<TextMeshProUGUI>().text = $"{item.ItemName}\n{createCount}개 제작";
        CreateSuccessPanel.SetActive(true);
    }


    public void ChangeItemList(Acquisition aquisition)
    {
        List<ItemInfo> items = new List<ItemInfo>();

        foreach (var itemInfo in gameManager.itemInfos)
        {
            string[] aquisitions = itemInfo.Value.acquisitions.Split(",");

            for (int i = 0; i < aquisitions.Length; i++)
            {
                if ((Acquisition)int.Parse(aquisitions[i]) == aquisition)
                {
                    items.Add(itemInfo.Value);
                }
            }
        }
    }
}
