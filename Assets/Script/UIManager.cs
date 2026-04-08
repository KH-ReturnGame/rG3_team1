using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject inventoryPanel; // 인벤토리 패널을 연결할 변수

    public void ToggleInventory()
    {
        // 현재 켜져있으면 끄고, 꺼져있으면 켜는 기능
        bool isActive = inventoryPanel.activeSelf;
        inventoryPanel.SetActive(!isActive);
    }
}
