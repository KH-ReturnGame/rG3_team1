using System.Collections.Generic;

// 한 슬롯에 저장되는 데이터(JSON 직렬화)
[System.Serializable]
public class SavedItem
{
    public string id;
    public int count;
    public int px = -1, py = -1;   // 그리드 배치 좌표(-1 = 미기록 → 자동 배치. 옛 세이브 호환)
    public int rot;                // R 회전 단계(0~3 = 0/90/180/270도)
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
    public int moduleMinimap, moduleScan;                    // 후드 모듈 해금(0/1)
    public int moduleQuickdraw;                              // (구) 빨리 뽑기 — 핫바 폐지로 미사용(하위호환 유지)
    public int invCols;                                      // 배낭 확장(그리드 가로 열 수, 0=기본)
}
