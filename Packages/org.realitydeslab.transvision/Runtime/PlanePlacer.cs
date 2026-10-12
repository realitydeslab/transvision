using UnityEngine;
using UnityEngine.Animations;
using HoloKit;

/// <summary>
/// Shows the effect's result to Stereo's eyes. The picture is the AR camera's whole image, so the
/// plane is that camera's view: a child of the AR camera spanning exactly its frustum (ARKit's
/// projection, kept correct on iOS 27 by the HoloKit SDK, widened from the screen's crop to the whole
/// image) at <see cref="PlaneDistance"/>, then magnified by <see cref="PictureZoom"/> about its centre.
/// The eye cameras, placed and projected by the SDK's HoloKit calibration, see each camera pixel on
/// its real point for points at PlaneDistance. The camera sits 7.8 cm in front of and 6.2 cm above
/// the eyes, so a flat picture is right at one distance only: nearer points show lower and larger,
/// farther ones higher and smaller.
/// Mono draws the result full screen instead (CameraImageProcessor), so the plane is hidden there.
/// </summary>
public class PlanePlacer : MonoBehaviour
{
    public HoloKitCameraManager m_HoloKitCameraManager;

    /// The AR camera, whose picture the plane shows.
    public Camera monoCamera;

    /// Distance from the AR camera to the plane, in meters, where Stereo's picture lines up. 1 m keeps
    /// the error within about 4 degrees and 11% from 0.4 m to 4 m (before the zoom).
    public const float PlaneDistance = 1f;

    /// Magnification of Stereo's picture about its centre (1: true size at PlaneDistance).
    public const float PictureZoom = 1.0f;

    /// Edge length of Unity's built-in Plane mesh, which this object renders.
    public const float PlaneMeshSize = 10f;

    /// Turns the XZ Plane to face the camera: its X along the view's width, its Z along the height.
    public static readonly Vector3 PlaneRotationOffset = new Vector3(90, -90, 90);

    Renderer m_Renderer;
    CameraImageProcessor m_Processor;

    void Start()
    {
        m_Renderer = GetComponent<Renderer>();
        m_Processor = FindFirstObjectByType<CameraImageProcessor>();
        // A child of the AR camera follows it at render time; a constraint would lag a pose update.
        if (TryGetComponent<ParentConstraint>(out var constraint))
            constraint.constraintActive = false;
        transform.SetParent(monoCamera.transform, false);
    }

    // After this frame's Update, where the HoloKit SDK and AR Foundation set the AR camera's projection.
    void LateUpdate()
    {
        bool stereo = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Stereo;
        m_Renderer.enabled = stereo;
        // Stereo shows the camera's whole image: more than the screen's crop, which leaves the lower
        // part of HoloKit's eye view without a picture.
        if (m_Processor != null)
            m_Processor.WholeCameraImage = stereo;
        if (!stereo)
            return;
        Vector2 crop = m_Processor != null ? m_Processor.ScreenCrop : Vector2.one;
        FitToCameraView(transform, WholeImageProjection(monoCamera.projectionMatrix, crop), PlaneDistance, PictureZoom);
    }

    /// Places <paramref name="plane"/>, a child of a camera with <paramref name="projection"/>, so it
    /// fills that camera's view at <paramref name="distance"/>, texture the right way up, magnified
    /// <paramref name="zoom"/> times about the view's centre.
    public static void FitToCameraView(Transform plane, Matrix4x4 projection, float distance, float zoom = 1f)
    {
        plane.localPosition = CenterOfView(projection, distance);
        plane.localRotation = Quaternion.Euler(PlaneRotationOffset);
        Vector3 scale = ScaleToFillView(projection, distance);
        plane.localScale = new Vector3(scale.x * zoom, 1f, scale.z * zoom);
    }

    /// The AR camera's projection widened from the screen's crop to the camera's whole image:
    /// <paramref name="screenCrop"/> is the part of the whole image the screen shows, per axis.
    public static Matrix4x4 WholeImageProjection(Matrix4x4 projection, Vector2 screenCrop)
    {
        projection.m00 *= screenCrop.x;
        projection.m02 *= screenCrop.x;
        projection.m11 *= screenCrop.y;
        projection.m12 *= screenCrop.y;
        return projection;
    }

    /// Local scale that makes the built-in Plane span the view of <paramref name="projection"/> at
    /// <paramref name="distance"/>: 2d/m00 wide and 2d/m11 high.
    public static Vector3 ScaleToFillView(Matrix4x4 projection, float distance) =>
        new(2f * distance / projection.m00 / PlaneMeshSize, 1, 2f * distance / projection.m11 / PlaneMeshSize);

    /// The centre of <paramref name="projection"/>'s view at <paramref name="distance"/>, in the
    /// camera's space (ARKit's principal point is slightly off-centre).
    public static Vector3 CenterOfView(Matrix4x4 projection, float distance) =>
        new(distance * projection.m02 / projection.m00, distance * projection.m12 / projection.m11, distance);
}
