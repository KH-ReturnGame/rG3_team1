using UnityEngine;
using UnityEngine.SceneManagement;

public class StartAreaTeleporter : MonoBehaviour
{
    public string stageSceneName = "Stage1";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            LoadStage();
        }
    }

    void LoadStage()
    {
        SceneManager.LoadScene(stageSceneName);
    }
}