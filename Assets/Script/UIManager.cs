using UnityEngine;
using UnityEngine.UI; // UI 기능을 사용하기 위해 맨 위에 모아둡니다.

public class UIManager : MonoBehaviour
{
    // --- [1. 인벤토리 설정] ---
    [Header("Inventory Settings")]
    public GameObject inventoryPanel;

    // --- [2. HP(하트) 설정] ---
    [Header("HP Settings")]
    public GameObject[] hearts; // 하트 이미지 5개를 담을 배열
    public int currentHp = 5;   // 현재 체력

    // --- [3. 기력(스테미나) 설정] ---
    [Header("Stamina Settings")]
    public Slider staminaSlider;
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float recoverRate = 10f; // 초당 회복량

    void Update()
    {
        // [기력 시스템] 매 프레임마다 조금씩 회복됨
        if (currentStamina < maxStamina)
        {
            currentStamina += recoverRate * Time.deltaTime;
        }

        // 슬라이더 UI에 현재 기력 실시간 반영
        if (staminaSlider != null)
        {
            staminaSlider.value = currentStamina;
        }
    }

    // --- [기능 함수들] ---

    // 인벤토리 열고 닫기 (토글)
    public void ToggleInventory()
    {
        if (inventoryPanel != null)
        {
            bool isActive = inventoryPanel.activeSelf;
            inventoryPanel.SetActive(!isActive);
        }
    }

    // 체력 감소 (하트 하나 끄기)
    public void DecreaseHp()
    {
        if (currentHp > 0)
        {
            currentHp--;
            hearts[currentHp].SetActive(false);
        }
    }

    // 체력 회복 (하트 하나 켜기)
    public void IncreaseHp()
    {
        if (currentHp < 5)
        {
            hearts[currentHp].SetActive(true);
            currentHp++;
        }
    }

    // 기력 소모 (예: 대시나 공격 시 호출)
    public void UseStamina(float amount)
    {
        currentStamina -= amount;
        if (currentStamina < 0) currentStamina = 0;
    }
}