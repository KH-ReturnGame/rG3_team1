using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// 3슬롯 JSON 세이브 (persistentDataPath/save_0.json ...).
// New Game/Load는 데이터를 'pending'에 담고 씬을 로드 → 씬 로드 완료 시 GameManager/Inventory에 적용.
// 진행 중 씬 이동 시 자동 저장.
public static class SaveSystem
{
    public const int SlotCount = 3;
    public static int CurrentSlot = -1;

    private static SaveSlotData pending;

    private static string PathFor(int slot)
        => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

    public static bool Exists(int slot) => File.Exists(PathFor(slot));

    public static SaveSlotData Read(int slot)
    {
        if (!Exists(slot)) return null;
        try { return JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(PathFor(slot))); }
        catch { return null; }
    }

    public static void Write(int slot, SaveSlotData data)
    {
        data.lastPlayed = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
    }

    public static void Delete(int slot) { if (Exists(slot)) File.Delete(PathFor(slot)); }

    // 새 게임: 기본 데이터 생성·저장 후 시작 씬 로드
    public static void NewGame(int slot, string startScene)
    {
        var data = new SaveSlotData
        {
            saveName = $"슬롯 {slot + 1}",
            sceneName = startScene,
            hearts = -1,            // -1 = 최대치로 시작
            maxHearts = 6,
            maxStamina = 100f,
            gold = 0,
            items = new List<SavedItem>()
        };
        Write(slot, data);
        CurrentSlot = slot;
        pending = data;
        SceneManager.LoadScene(startScene);
    }

    // 불러오기: 저장된 씬·데이터로 복귀
    public static void LoadGame(int slot)
    {
        var data = Read(slot);
        if (data == null) return;
        CurrentSlot = slot;
        pending = data;
        SceneManager.LoadScene(string.IsNullOrEmpty(data.sceneName) ? "TutorialScene" : data.sceneName);
    }

    // 현재 상태를 현재 슬롯에 저장(자동저장 등)
    public static void SaveCurrent()
    {
        if (CurrentSlot < 0 || GameManager.Instance == null) return;
        var data = Read(CurrentSlot) ?? new SaveSlotData { saveName = $"슬롯 {CurrentSlot + 1}" };
        Capture(data);
        Write(CurrentSlot, data);
    }

    private static void Capture(SaveSlotData data)
    {
        data.sceneName = SceneManager.GetActiveScene().name;
        if (GameManager.Instance != null)
        {
            data.hearts = GameManager.Instance.CurrentHearts;
            data.maxHearts = GameManager.Instance.MaxHearts;
            data.maxStamina = GameManager.Instance.MaxStamina;
            data.gold = GameManager.Instance.Gold;
        }
        data.items = new List<SavedItem>();
        if (Inventory.Instance != null)
            foreach (var s in Inventory.Instance.slots)
                if (s != null && !s.IsEmpty)
                    data.items.Add(new SavedItem { id = ItemDatabase.Key(s.item), count = s.count });
    }

    private static void Apply(SaveSlotData data)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadStats(data.hearts, data.maxHearts, data.maxStamina, data.gold);
        if (Inventory.Instance != null)
            Inventory.Instance.LoadFromSaved(data.items);
    }

    // 게임 시작 시 1회 씬 로드 콜백 등록
    [RuntimeInitializeOnLoadMethod]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pending != null)
        {
            Apply(pending);       // 새 게임/불러오기 → 적용
            pending = null;
        }
        else if (CurrentSlot >= 0 && GameManager.Instance != null)
        {
            SaveCurrent();        // 진행 중 씬 이동 → 자동 저장
        }
    }
}
