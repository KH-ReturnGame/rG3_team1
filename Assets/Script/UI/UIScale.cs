using UnityEngine;

// IMGUI UI 좌표 헬퍼.
//  ⚠️ 한때 GUI.matrix로 전역 스케일(기준 1080p)을 적용했으나, IMGUI 동적 폰트는 원래 크기로 래스터된 글리프를
//     매트릭스로 리샘플링하면 흐려진다(특히 고배율 디스플레이/풀스크린 S≠1에서 심함). → 텍스트 선명도 유지를 위해 비활성.
//  · 현재는 '패스스루' — Apply()는 아무것도 안 하고, W/H는 실제 화면 크기를 그대로 반환(원래 고정픽셀 동작과 동일).
//  · 각 UI는 그대로 UIScale.Apply() / UIScale.W / UIScale.H 를 호출하므로, 추후 '선명한 해상도 독립'(폰트 크기 스케일 방식)을
//    구현하려면 이 한 파일만 바꾸면 된다.
public static class UIScale
{
    public static float S { get { return 1f; } }                 // 스케일 비활성(선명도 유지)
    public static float W { get { return Screen.width; } }       // 실제 화면 폭
    public static float H { get { return Screen.height; } }      // 실제 화면 높이

    public static void Apply() { }   // no-op: GUI.matrix를 건드리지 않음 → 폰트 선명

    public static Vector2 PxToVirtual(Vector2 screenPxTopDown) { return screenPxTopDown; }
    public static Vector2 WorldTopDown(Camera cam, Vector3 world)
    {
        Vector3 p = cam.WorldToScreenPoint(world);
        return new Vector2(p.x, Screen.height - p.y);
    }
}
