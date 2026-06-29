using UnityEngine;

// IMGUI 해상도 독립 스케일러. IMGUI 폰트·크기는 '픽셀 고정'이라 게임뷰/해상도가 작아지면 UI가 상대적으로 커 보인다.
//  · 기준 해상도 높이(RefH=1080)로 설계하고, 실제 화면 높이에 맞춰 GUI.matrix를 균일 스케일 → 어떤 해상도에서도 같은 비율로 보임.
//  · 사용법: 화면 UI의 OnGUI 맨 첫 줄에 UIScale.Apply(); 호출하고, Screen.width/height 대신 UIScale.W / UIScale.H 사용.
//  · GUI.matrix는 Unity가 OnGUI마다 초기화하므로 복원 불필요(각 OnGUI가 자기 첫 줄에서 다시 Apply).
//  · 월드 추종 UI(적 체력바·상호작용 프롬프트)나 전체화면 오버레이(페이드)는 Apply 안 해도 됨 — 좌표가 화면 비율 기반이라 그대로 동작.
public static class UIScale
{
    public const float RefH = 1080f;

    public static float S { get { return Mathf.Max(0.0001f, Screen.height / RefH); } }  // 스케일 배율(화면 높이 기준)
    public static float W { get { return Screen.width / S; } }   // 가상 화면 폭 (= Screen.width * 1080 / Screen.height)
    public static float H { get { return RefH; } }               // 가상 화면 높이 (고정 1080)

    public static void Apply() { GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f)); }

    // 월드 앵커 UI를 스케일 좌표계에서 그릴 때: 실제 스크린 픽셀(GUI 상단원점) → 가상 좌표
    public static Vector2 PxToVirtual(Vector2 screenPxTopDown) { return screenPxTopDown / S; }
    public static Vector2 WorldTopDown(Camera cam, Vector3 world)
    {
        Vector3 p = cam.WorldToScreenPoint(world);
        return new Vector2(p.x, Screen.height - p.y) / S;
    }
}
