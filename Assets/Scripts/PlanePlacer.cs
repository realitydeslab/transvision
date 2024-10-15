using UnityEngine;
using HoloKit;

public class PlanePlacer : MonoBehaviour
{
    public HoloKitCameraManager m_HoloKitCameraManager;
    public Camera monoCamera;
    public Camera stereoCamera;

    Vector2 screenResoluion;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        screenResoluion = new Vector2(Screen.width, Screen.height);
        MatchPlaneToScreenSize();
    }

    //// Update is called once per frame
    //void Update()
    //{
    //    Camera cam = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? monoCamera : stereoCamera;

    //    float pos = (cam.nearClipPlane + 10.0f);

    //    transform.position = cam.transform.position + cam.transform.forward * pos;
    //    //transform.forward = cam.transform.forward;
    //    transform.LookAt(cam.transform);
    //    float h = (Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * pos * 2f) * cam.aspect / 10.0f;
    //    float w = h * Screen.height / Screen.width;
    //    transform.localScale = new Vector3(h, w, 1) * 10;
    //}

    void Update()
    {
        //if(screenResoluion.x != Screen.width || screenResoluion.y != Screen.height)
        //{
        //    MatchPlaneToScreenSize();
        //    screenResoluion.x = Screen.width;
        //    screenResoluion.y = Screen.height;
        //}
        MatchPlaneToScreenSize();
    }

    void MatchPlaneToScreenSize()
    {
        Camera cam = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? monoCamera : stereoCamera;

        float planeToCameraDistance = Vector3.Distance(gameObject.transform.position, cam.transform.position);
        float planeHeightScale = (2.0f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) * planeToCameraDistance) / 10.0f;
        float planeWidthScale = planeHeightScale * cam.aspect;

        gameObject.transform.localScale = new Vector3(planeWidthScale, 1, planeHeightScale);
    }
}
