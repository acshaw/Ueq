using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cams = Camera.allCameras;
        if (cams.Length == 0) return;
        var cam = cams[0];
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
    }
}
