using UnityEngine;
using UnityEngine.SceneManagement;

public class EndPointTeleporter : MonoBehaviour
{
    public string sceneToLoad = "StartingArea";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            AdvanceStageAndReturn();
        }
    }

    void AdvanceStageAndReturn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentStage++;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}