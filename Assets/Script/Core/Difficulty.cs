using UnityEngine;

// 난이도(타이틀 화면에서 선택, PlayerPrefs로 유지 — 세이브와 무관하게 기기 설정).
//  쉬움: 체력 반 칸(위기)일 때 피격 '직전' 예지가 자동 발동 — 짧고 굵게(훅 켜졌다 훅 꺼짐)
//  어려움: 자동 예지 없음. (장신구 '예지안'과 튜토리얼 첫 각성 연출은 난이도와 무관하게 유지)
public static class Difficulty
{
    public enum Mode { Easy = 0, Hard = 1 }

    private const string Key = "difficulty";
    private static Mode? cur;   // 도메인 리로드 대비 지연 로드

    public static Mode Current
    {
        get { if (cur == null) cur = (Mode)PlayerPrefs.GetInt(Key, 0); return cur.Value; }
        set { cur = value; PlayerPrefs.SetInt(Key, (int)value); PlayerPrefs.Save(); }
    }

    public static bool AutoPrecog => Current == Mode.Easy;
    public static string Label => Current == Mode.Easy ? "쉬움" : "어려움";
}
