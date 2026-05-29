using UnityEngine;

public class FrameRateSetting : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 60; // 원하는 FPS 입력
        QualitySettings.vSyncCount = 0;   // VSync 꺼야 targetFrameRate가 적용됨!
    }
}