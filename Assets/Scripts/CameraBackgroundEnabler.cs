using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class CameraBackgroundEnabler : MonoBehaviour
{
    [SerializeField]
    ARCameraBackground m_ARCameraBackground;

    public void EnableCameraBackground()
    {
        m_ARCameraBackground.enabled = true;
    }
}
