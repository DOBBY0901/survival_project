using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreateDesk : MonoBehaviour, IMouseInteraction
{
    [SerializeField] private CreatePanel deskPanel;
    public void CanInteraction(bool _canInteraction)
    {
        throw new System.NotImplementedException();
    }

    public IEnumerator EndInteraction(Animator anim, float waitTime)
    {
        throw new System.NotImplementedException();
    }

    public void InteractionLeftButtonFuc(GameObject hitObject)
    {
        deskPanel.gameObject.SetActive(true);
        deskPanel.SetCreateAcquisition(Acquisition.CraftTable);
        deskPanel.OpenFor(Acquisition.CraftTable); // 제작대 전용
    }

    public void InteractionRightButtonFuc(GameObject hitObject)
    {
        throw new System.NotImplementedException();
    }

    public bool ReturnCanInteraction()
    {
        throw new System.NotImplementedException();
    }
}
