using UnityEngine;

public class StageManager : MonoBehaviour
{
    //되긴 하는데 나중에 에너미 추가하면 수정해야함
    public Transform player;
    public Transform startPoint;
    public int chunksPerStage = 2;
    public float chunkWidth = 60f;

    private int currentStage = 1;
    public int totalStages = 5;

    void Update()
    {
        float distanceFromStart = player.position.x - startPoint.position.x;
        int detectedStage = GetStage(distanceFromStart);

        if (detectedStage != currentStage)
        {
            currentStage = detectedStage;
            OnStageChanged(currentStage);
        }
    }

    int GetStage(float distance)
    {
        int stage = Mathf.FloorToInt(distance / chunkWidth) + 1;
        return Mathf.Clamp(stage, 1, totalStages);
    }

    void OnStageChanged(int newStage)
    {
        Debug.Log("stage: " + newStage);
    }
}