using UnityEngine;
using UnityEngine.SceneManagement;

public class StartAreaTeleporter : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            int stage = GameManager.Instance.currentStage;

            string sceneName = "Stage" + stage;

            SceneManager.LoadScene(sceneName);
        }
    }
}