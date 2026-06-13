using System.Collections.Generic;
using UnityEngine;

public enum QuestCategory { Main, Gather, Combat }   // 주요 / 수집 / 전투
public enum QuestGoal { Gather, Kill }               // 채집 / 처치

[System.Serializable]
public class Quest
{
    public string id;
    public QuestCategory category;
    public string giver;                 // 의뢰인(엘마씨, 건터 …)
    public string title;                 // 엘마씨의 의뢰
    [TextArea] public string description;
    public QuestGoal goal;
    public string targetId;              // 채집 아이템 id / 처치 적 id
    public string targetName;            // 표시용(비우면 아이템 이름에서 유추)
    public int targetCount = 1;
    public int rewardGold;
    public string rewardItemId;          // 보상 아이템(옵션)
    public int rewardItemCount = 1;
    public Sprite icon;                  // 퀘스트 이미지(옵션)
    [System.NonSerialized] public int progress;

    public string CategoryLabel()
    {
        switch (category) { case QuestCategory.Main: return "주요"; case QuestCategory.Gather: return "수집"; case QuestCategory.Combat: return "전투"; }
        return "";
    }
    public string TargetDisplay()
    {
        if (!string.IsNullOrEmpty(targetName)) return targetName;
        if (goal == QuestGoal.Gather) { var it = ItemDatabase.Get(targetId); if (it != null) return it.itemName; }
        return targetId;
    }
    public string ObjectiveText()
    {
        return TargetDisplay() + (goal == QuestGoal.Gather ? " 채집하기 (" : " 처치 (") + progress + "/" + targetCount + ")";
    }
}

// 퀘스트 전역 관리(자동부팅·영구). 게시판에서 수주, J 로그에서 확인/포기.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public List<Quest> available = new List<Quest>();   // 게시판에 뜨는 의뢰
    public List<Quest> accepted = new List<Quest>();     // 수주한 의뢰

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
        available.Add(new Quest { id = "main_first", category = QuestCategory.Main, giver = "지저 마을", title = "지저로의 첫 걸음",
            description = "낯선 지저 세계. 우선 마을을 둘러보고 지저꽃을 하나 채집해 적응을 시작하자.",
            goal = QuestGoal.Gather, targetId = "underground_flower", targetCount = 1, rewardGold = 200 });
        available.Add(new Quest { id = "gather_flower", category = QuestCategory.Gather, giver = "엘마씨", title = "엘마씨의 의뢰",
            description = "엘마씨가 지저꽃의 재고가 다 떨어져 채집을 요청했다.",
            goal = QuestGoal.Gather, targetId = "underground_flower", targetCount = 5, rewardGold = 500, rewardItemId = "combat_potion", rewardItemCount = 1 });
        available.Add(new Quest { id = "combat_slime", category = QuestCategory.Combat, giver = "건터", title = "건터의 의뢰",
            description = "건터가 갱도에 늘어난 슬라임을 처치해 달라고 부탁했다.",
            goal = QuestGoal.Kill, targetId = "slime", targetName = "슬라임", targetCount = 7, rewardGold = 400 });
    }

    public bool IsAccepted(Quest q) => q != null && accepted.Contains(q);

    public void Accept(Quest q) { if (q == null || accepted.Contains(q)) return; q.progress = 0; accepted.Add(q); Toast.Show("퀘스트 수락! J키를 눌러 확인할 수 있습니다.", 4f); Changed(); }
    public void Abandon(Quest q) { if (q == null) return; q.progress = 0; accepted.Remove(q); Changed(); }

    // 채집 진행(아이템 주울 때 호출)
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

    // 처치 진행(적 사망 시 호출)
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
        if (GameManager.Instance != null && q.rewardGold > 0) GameManager.Instance.AddGold(q.rewardGold);
        if (!string.IsNullOrEmpty(q.rewardItemId) && Inventory.Instance != null)
        { var it = ItemDatabase.Get(q.rewardItemId); if (it != null) Inventory.Instance.Add(it, Mathf.Max(1, q.rewardItemCount)); }
        accepted.Remove(q);
        available.Remove(q);
        Debug.Log("[Quest] 완료: " + q.title + " — 보상 지급");
    }
}
