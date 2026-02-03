using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemInventory : MonoBehaviour
{
    [SerializeField] GameObject descriptionPanel;
    [SerializeField] Transform slotParent;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] GameObject invenPanel;
    [SerializeField] GameObject dragSlot;

    public GameObject DragSlot => dragSlot;

    int currentCategory;
    List<ItemInvenSlot> slots;

    GameManager gameManager;

    private void Awake()
    {
        slots = new List<ItemInvenSlot>();
        gameManager = GameManager.Instance;

        currentCategory = -1;

        for (int i = 0; i < 28; i++)
        {
            ItemInvenSlot slot = Instantiate(slotPrefab, slotParent).GetComponent<ItemInvenSlot>();
            slot.inventory = this;
            slots.Add(slot);
        }

        descriptionPanel.GetComponent<Text>().text = "";

        ChangeCategory(0);

        invenPanel.SetActive(false);    
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            invenPanel.SetActive(!invenPanel.activeSelf);

            if(invenPanel.activeSelf)
                ChangeCategory(currentCategory);
        }
    }

    public void ChangeCategory(int type)
    {
        int index = 0;
        ItemType mask = (ItemType)(1 << type);

        foreach (var kv in gameManager.haveItems)
        {
            int itemId = kv.Key;

            // itemDatas에서 itemId 찾기 (미리 Dictionary로 캐싱하면 더 좋음)
            Item item = gameManager.itemDatas.Find(x => x.ItemId == itemId);
            if (item == null) continue;
            
            //하나라도 포함시 통과
            if (!item.CompareType(mask, false)) continue;

            if (index >= slots.Count) break;

            // 슬롯 UI가 ItemInfo를 받는 구조라면
            if (!gameManager.itemInfos.TryGetValue(itemId, out ItemInfo info)) continue;

            slots[index].SetSlot(info);
            index++;
        }

        for (int i = index; i < slots.Count; i++)
            slots[i].SetSlot(null);

        currentCategory = type;
    }



    public void SetItemDescription(ItemInfo item)
    {
        descriptionPanel.GetComponent<Text>().text = $"{item.itemName}\n\n{item.effect.Replace("\\n", "\n")}\n\n{item.decription.Replace("\\n","\n")}";
    }
}
