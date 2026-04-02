using UnityEngine;

public class StageManager : MonoBehaviour
{
    //얘는 지금 고장난 상태
    //근데 꼭 필요한건 아니라 그냥 방치해놓음
    public Transform player;
    public Transform stageEdge;
    public int currentStage = 1;
    private int totalStages = 3;
    private bool stageCompleted = false;

    void Update()
    {
        float distanceToEdge = player.position.x - stageEdge.position.x;

        if (distanceToEdge >= 0 && stageCompleted)
        {
            currentStage++;
            currentStage = Mathf.Clamp(currentStage, 1, totalStages);
            stageCompleted = false;
            OnStageChanged(currentStage);
        }
    }

    public void CompleteStage()
    {
        stageCompleted = true;
        Debug.Log("Stage " + currentStage + " completed!");
    }

    void OnStageChanged(int newStage)
    {
        Debug.Log("Entered stage " + newStage);
    }
}