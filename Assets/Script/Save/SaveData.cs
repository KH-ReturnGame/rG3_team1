using System.Collections.Generic;

// 한 슬롯에 저장되는 데이터(JSON 직렬화)
[System.Serializable]
public class SavedItem
{
    public string id;
    public int count;
}

[System.Serializable]
public class SavedQuest
{
    public string id;
    public int progress;
}

[System.Serializable]
public class SaveSlotData
{
    public string saveName = "새 게임";
    public string lastPlayed = "";
    public string sceneName = "";      // 저장 당시 씬(불러오면 이 씬으로)
    public int hearts;
    public int maxHearts = 6;
    public float maxStamina = 100f;
    public int gold;
    public List<SavedItem> items = new List<SavedItem>();
    public List<string> equipped = new List<string>();   // 착용한 장신구 id(빈칸은 "")
    public List<SavedQuest> acceptedQuests = new List<SavedQuest>();   // 수주 중인 퀘스트(id+진행도)
    public int bonusJumps;   // 상점 영구 점프 업그레이드
    public List<string> completedQuests = new List<string>();   // 완료한 퀘스트 id(연계 진행)
    public int level = 1;
    public int xp = 0;
    public int modPoints = 0;
    public int statRegen, statAttack, statAdapt, statLuck;   // 개조 포인트 투자 레벨
}
