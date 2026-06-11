using System.Collections.Generic;

// 한 슬롯에 저장되는 데이터(JSON 직렬화)
[System.Serializable]
public class SavedItem
{
    public string id;
    public int count;
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
}
