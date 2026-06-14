using System.Collections.Generic;
using UnityEngine;

public enum QuestCategory { Main, Gather, Combat }   // 주요 / 수집 / 전투
public enum QuestGoal { Gather, Kill }               // 채집 / 처치

[System.Serializable]
public class Quest
{
    public string id;
    public QuestCategory category;
    public string giver;
    public string title;
    [TextArea] public string description;
    public QuestGoal goal;
    public string targetId;
    public string targetName;            // 표시용(비우면 아이템 이름에서 유추)
    public int targetCount = 1;
    public int rewardGold;
    public string rewardItemId;
    public int rewardItemCount = 1;
    public int xpReward = 50;            // 완료 시 경험치
    public string prereqId;              // 선행 퀘스트 id(이게 완료돼야 게시판에 등장 — 연계)
    public Sprite icon;
    [System.NonSerialized] public int progress;

    public string CategoryLabel()
    { switch (category) { case QuestCategory.Main: return "주요"; case QuestCategory.Gather: return "수집"; case QuestCategory.Combat: return "전투"; } return ""; }
    public string TargetDisplay()
    { if (!string.IsNullOrEmpty(targetName)) return targetName; if (goal == QuestGoal.Gather) { var it = ItemDatabase.Get(targetId); if (it != null) return it.itemName; } return targetId; }
    public string ObjectiveText()
    { return TargetDisplay() + (goal == QuestGoal.Gather ? " 채집하기 (" : " 처치 (") + progress + "/" + targetCount + ")"; }
}

// 퀘스트 전역 관리(자동부팅·영구). 연계(prereq)·완료추적·경험치 보상.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public List<Quest> available = new List<Quest>();    // 정의된 전체 퀘스트
    public List<Quest> accepted = new List<Quest>();      // 수주 중
    public List<string> completed = new List<string>();   // 완료한 id(세이브 유지)

    public event System.Action OnChanged;
    private void Changed() { OnChanged?.Invoke(); }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        BuildQuests();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("QuestManager").AddComponent<QuestManager>(); }

    private void BuildQuests()
    {
        available.Clear();
        // 주요 연계: 첫 걸음 → 더 깊은 곳으로
        available.Add(new Quest { id = "main_first", category = QuestCategory.Main, giver = "지저 마을", title = "지저로의 첫 걸음",
            description = "낯선 지저 세계. 마을을 둘러보고 지저꽃을 하나 채집해 적응을 시작하자.",
            goal = QuestGoal.Gather, targetId = "underground_flower", targetCount = 1, rewardGold = 200, xpReward = 40 });
        available.Add(new Quest { id = "main_second", category = QuestCategory.Main, giver = "지저 마을", title = "더 깊은 곳으로", prereqId = "main_first",
            description = "더 깊은 갱도에서만 피는 화염꽃을 가져와 탐험을 이어가자.",
            goal = QuestGoal.Gather, targetId = "flame_flower", targetCount = 2, rewardGold = 600, xpReward = 100 });
        // 엘마씨 연계
        available.Add(new Quest { id = "gather_flower", category = QuestCategory.Gather, giver = "엘마씨", title = "엘마씨의 의뢰",
            description = "엘마씨가 지저꽃의 재고가 다 떨어져 채집을 요청했다.",
            goal = QuestGoal.Gather, targetId = "underground_flower", targetCount = 5, rewardGold = 500, rewardItemId = "combat_potion", rewardItemCount = 1, xpReward = 60 });
        available.Add(new Quest { id = "gather_flower2", category = QuestCategory.Gather, giver = "엘마씨", title = "엘마씨의 추가 부탁", prereqId = "gather_flower",
            description = "지저꽃이 잘 팔려서, 엘마씨가 더 많이 부탁했다.",
            goal = QuestGoal.Gather, targetId = "underground_flower", targetCount = 8, rewardGold = 750, xpReward = 90 });
        // 단독
        available.Add(new Quest { id = "combat_slime", category = QuestCategory.Combat, giver = "건터", title = "건터의 의뢰",
            description = "건터가 갱도에 늘어난 슬라임을 처치해 달라고 부탁했다.",
            goal = QuestGoal.Kill, targetId = "slime", targetName = "슬라임", targetCount = 7, rewardGold = 400, xpReward = 70 });
        available.Add(new Quest { id = "gather_lizard", category = QuestCategory.Gather, giver = "요리사 핀", title = "핀의 식재료",
            description = "요리사 핀이 도마뱀 고기가 필요하다며 채집을 부탁했다.",
            goal = QuestGoal.Gather, targetId = "lizard", targetCount = 3, rewardGold = 250, rewardItemId = "heal_potion", rewardItemCount = 1, xpReward = 50 });
        available.Add(new Quest { id = "gather_flame", category = QuestCategory.Gather, giver = "대장장이 보라", title = "보라의 불씨",
            description = "대장장이 보라가 화로에 쓸 화염꽃을 모아달라고 했다.",
            goal = QuestGoal.Gather, targetId = "flame_flower", targetCount = 4, rewardGold = 450, xpReward = 55 });
        available.Add(new Quest { id = "gather_slime", category = QuestCategory.Gather, giver = "약초상 델", title = "델의 연구",
            description = "약초상 델이 슬라임 응축액을 연구용으로 수집하고 있다.",
            goal = QuestGoal.Gather, targetId = "slime_condensate", targetCount = 5, rewardGold = 400, rewardItemId = "defense_potion", rewardItemCount = 1, xpReward = 55 });
    }

    public bool IsAccepted(Quest q) => q != null && accepted.Contains(q);
    public bool IsCompleted(Quest q) => q != null && completed.Contains(q.id);
    public bool IsUnlocked(Quest q) => q != null && (string.IsNullOrEmpty(q.prereqId) || completed.Contains(q.prereqId));

    public void Accept(Quest q)
    {
        if (q == null || accepted.Contains(q) || IsCompleted(q) || !IsUnlocked(q)) return;
        q.progress = 0; accepted.Add(q);
        Toast.Show("퀘스트 수락! J키를 눌러 확인할 수 있습니다.", 4f);
        Changed();
    }
    public void Abandon(Quest q) { if (q == null) return; q.progress = 0; accepted.Remove(q); Changed(); }

    public void ReportGather(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return;
        var done = new List<Quest>(); bool changed = false;
        foreach (var q in accepted)
            if (q.goal == QuestGoal.Gather && q.targetId == itemId && q.progress < q.targetCount)
            { q.progress = Mathf.Min(q.targetCount, q.progress + amount); changed = true; if (q.progress >= q.targetCount) done.Add(q); }
        foreach (var q in done) Complete(q);
        if (changed) Changed();
    }
    public void ReportKill(string killId)
    {
        if (string.IsNullOrEmpty(killId)) return;
        var done = new List<Quest>(); bool changed = false;
        foreach (var q in accepted)
            if (q.goal == QuestGoal.Kill && q.targetId == killId && q.progress < q.targetCount)
            { q.progress = Mathf.Min(q.targetCount, q.progress + 1); changed = true; if (q.progress >= q.targetCount) done.Add(q); }
        foreach (var q in done) Complete(q);
        if (changed) Changed();
    }

    private void Complete(Quest q)
    {
        if (GameManager.Instance != null) { if (q.rewardGold > 0) GameManager.Instance.AddGold(q.rewardGold); if (q.xpReward > 0) GameManager.Instance.AddXp(q.xpReward); }
        if (!string.IsNullOrEmpty(q.rewardItemId) && Inventory.Instance != null) { var it = ItemDatabase.Get(q.rewardItemId); if (it != null) Inventory.Instance.Add(it, Mathf.Max(1, q.rewardItemCount)); }
        accepted.Remove(q);
        if (!completed.Contains(q.id)) completed.Add(q.id);
        Toast.Show("퀘스트 완료: " + q.title, 3.5f);   // 연계 퀘스트가 풀렸을 수 있음
    }

    // ── 세이브/로드 ──
    public List<SavedQuest> SaveAccepted()
    { var l = new List<SavedQuest>(); foreach (var q in accepted) l.Add(new SavedQuest { id = q.id, progress = q.progress }); return l; }

    public void LoadAccepted(List<SavedQuest> saved)
    {
        accepted.Clear();
        if (saved != null)
            foreach (var s in saved) { var q = available.Find(x => x.id == s.id); if (q != null) { q.progress = s.progress; if (!accepted.Contains(q)) accepted.Add(q); } }
        Changed();
    }
    public void LoadCompleted(List<string> ids) { completed = ids != null ? new List<string>(ids) : new List<string>(); Changed(); }
}
