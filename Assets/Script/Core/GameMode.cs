// 게임 모드 — 새 게임 시작 시 선택(축제용 타임어택 포함). 세이브에 함께 기록된다.
//  · 일반: 스토리를 따라 자유롭게.
//  · 트레져 헌터: 지하(메트로배니아)의 모든 보물상자를 최대한 빨리 모으기.
//  · 스피드런: 심층부의 지배자(첫 보스)를 최대한 빨리 처치.
public static class GameMode
{
    public enum Mode { Normal, TreasureHunter, SpeedRun }

    public static Mode Current = Mode.Normal;

    public static bool IsTimeAttack => Current != Mode.Normal;

    public static string Label(Mode m)
    {
        switch (m)
        {
            case Mode.TreasureHunter: return "트레져 헌터";
            case Mode.SpeedRun:       return "스피드런";
            default:                  return "일반";
        }
    }
    public static string CurLabel => Label(Current);

    public static string Desc(Mode m)
    {
        switch (m)
        {
            case Mode.TreasureHunter: return "지하의 보물상자를 전부 모으는 시간을 겨룹니다";
            case Mode.SpeedRun:       return "심층부의 지배자를 쓰러뜨리는 시간을 겨룹니다";
            default:                  return "이야기를 따라 자유롭게 모험합니다";
        }
    }

    // 모드 순환(메뉴에서 클릭 전환)
    public static Mode Next(Mode m) => (Mode)(((int)m + 1) % 3);
}
