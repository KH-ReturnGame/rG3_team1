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
        if (string.IsNullOrEmpty(startScene)) startScene = "TutorialScene";

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
        LoadSceneSafe(startScene);
    }

    // 불러오기: 저장된 씬·데이터로 복귀
    public static void LoadGame(int slot)
    {
        var data = Read(slot);
        if (data == null) return;
        CurrentSlot = slot;
        pending = data;
        LoadSceneSafe(string.IsNullOrEmpty(data.sceneName) ? "TutorialScene" : data.sceneName);
    }

    // 씬을 안전하게 로드 — Build Settings에 없으면 조용히 실패하지 않고 콘솔에 명확히 에러
    private static void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SaveSystem] 시작 씬 이름이 비어있습니다. StartMenu의 Start Scene을 'TutorialScene'으로 설정하세요.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SaveSystem] '{sceneName}' 씬을 로드할 수 없습니다. File ▸ Build Settings의 'Scenes In Build'에 이 씬이 추가·체크돼 있는지 확인하세요.");
            return;
        }
        SceneFader.FadeToScene(sceneName);   // 페이드아웃 후 로드
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
            data.maxHearts = GameManager.Instance.maxHearts;       // 장신구 보너스 제외(기본 최대)
            data.maxStamina = GameManager.Instance.maxStamina;
            data.gold = GameManager.Instance.Gold;
            data.bonusJumps = GameManager.Instance.bonusJumps;
        }
        data.items = new List<SavedItem>();
        if (Inventory.Instance != null)
            foreach (var s in Inventory.Instance.slots)
                if (s != null && !s.IsEmpty)
                    data.items.Add(new SavedItem { id = ItemDatabase.Key(s.item), count = s.count });
        if (Equipment.Instance != null) data.equipped = Equipment.Instance.SaveIds();
        if (QuestManager.Instance != null) data.acceptedQuests = QuestManager.Instance.SaveAccepted();
    }

    private static void Apply(SaveSlotData data)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadStats(data.hearts, data.maxHearts, data.maxStamina, data.gold);
            GameManager.Instance.bonusJumps = data.bonusJumps;   // 점프 업그레이드 복원(Equipment.LoadIds가 ApplyEquipment로 반영)
        }
        if (Inventory.Instance != null)
            Inventory.Instance.LoadFromSaved(data.items);
        if (Equipment.Instance != null)
            Equipment.Instance.LoadIds(data.equipped);   // 착용 장신구 복원(스탯 보너스 재적용)
        if (QuestManager.Instance != null)
            QuestManager.Instance.LoadAccepted(data.acceptedQuests);   // 수주 퀘스트 복원
    }

    // 게임 시작 시 1회 씬 로드 콜백 등록
    [RuntimeInitializeOnLoadMethod]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAutoLoad(SceneManager.GetActiveScene());   // 초기 씬(직접 Play)은 sceneLoaded가 안 떠서 여기서 복원
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pending != null)
        {
            Apply(pending);       // 새 게임/불러오기 → 적용
            pending = null;
            return;
        }
        if (CurrentSlot >= 0 && GameManager.Instance != null)
        {
            SaveCurrent();        // 진행 중 씬 이동 → 자동 저장
            return;
        }
        // 스타트메뉴를 안 거치고 게임플레이 씬을 바로 Play한 경우(에디터 반복 테스트):
        // 가장 최근 세이브가 있으면 자동 복원 → 레포트 기록한 인벤·스탯이 유지됨.
        TryAutoLoad(scene);
    }

    // 스타트메뉴를 안 거치고 게임플레이 씬을 바로 Play했을 때, 가장 최근 세이브를 자동 복원(에디터 반복 테스트 편의).
    private static void TryAutoLoad(Scene scene)
    {
        if (pending != null || CurrentSlot >= 0) return;
        if (scene.name == "StartScene") return;
        int recent = MostRecentSlot();
        if (recent < 0) return;
        CurrentSlot = recent;
        var data = Read(recent);
        if (data != null) Apply(data);
    }

    // 가장 최근(lastPlayed)에 저장된 슬롯. 없으면 -1.
    private static int MostRecentSlot()
    {
        int best = -1;
        string bestTime = "";
        for (int i = 0; i < SlotCount; i++)
        {
            var d = Read(i);
            if (d == null) continue;
            if (best < 0 || string.Compare(d.lastPlayed, bestTime) > 0) { best = i; bestTime = d.lastPlayed; }
        }
        return best;
    }
}
