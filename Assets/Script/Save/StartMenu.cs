using UnityEngine;

// StartScene에 두는 시작 메뉴(OnGUI). 슬롯 3개: 비어있으면 '새 게임', 차있으면 '불러오기' + '삭제'.
public class StartMenu : MonoBehaviour
{
    public string startScene = "TutorialScene";   // New Game이 시작할 씬

    private GUIStyle titleStyle, slotStyle, delStyle;

    void OnGUI()
    {
        if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold };
        if (slotStyle == null)  slotStyle  = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        if (delStyle == null)   delStyle   = new GUIStyle(GUI.skin.button) { fontSize = 14 };

        float w = 460f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;

        GUI.Label(new Rect(x, y, w, 50), "게임 제목 (임시)", titleStyle);
        y += 80;

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            var data = SaveSystem.Read(i);
            if (data == null)
            {
                if (GUI.Button(new Rect(x, y, w, 62), $"슬롯 {i + 1}   +  새 게임", slotStyle))
                    SaveSystem.NewGame(i, startScene);
            }
            else
            {
                if (GUI.Button(new Rect(x, y, w - 96, 62), $"슬롯 {i + 1}   {data.sceneName}\n{data.lastPlayed}", slotStyle))
                    SaveSystem.LoadGame(i);
                if (GUI.Button(new Rect(x + w - 90, y, 90, 62), "삭제", delStyle))
                    SaveSystem.Delete(i);
            }
            y += 74;
        }
    }
}
