using UnityEngine;

public class CameraFacingBillboard : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        // Ruota il Canvas per essere parallelo al piano della telecamera
        if (cam != null)
        {
            transform.rotation = transform.rotation = cam.transform.rotation;
        }
    }
}