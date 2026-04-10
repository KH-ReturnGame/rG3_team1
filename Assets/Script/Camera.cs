using UnityEngine;

public class Camera_controller : MonoBehaviour
{
    public Transform target;

    void Update()
    {
        if (target == null) return;

        transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }
}