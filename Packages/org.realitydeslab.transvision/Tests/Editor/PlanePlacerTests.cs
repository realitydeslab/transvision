using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TransVision.Tests
{
    /// <summary>
    /// Stereo shows the AR camera's picture on a plane: a child of the AR camera spanning its view
    /// (ARKit's off-centre projection, widened to the whole image) at PlanePlacer.PlaneDistance, so each
    /// picture pixel sits on the world point that camera pixel sees there; PictureZoom then magnifies
    /// it about the view's centre.
    /// </summary>
    public class PlanePlacerTests
    {
        // iPhone 15 Pro, the whole sensor (4:3) in LandscapeLeft: ARKit's projection widened from the
        // screen's crop (m11 3.216 x 0.615), as read on device.
        static readonly Matrix4x4 s_ArKitProjection = Projection(1.483f, 1.978f, -0.0006f, 0.014f);

        readonly List<Object> m_Created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Created)
                Object.DestroyImmediate(o);
            m_Created.Clear();
        }

        [Test]
        public void FitToCameraView_SpansTheArCameraView_AtThePlaneDistance()
        {
            Camera cam = NewArCamera();
            Transform plane = NewPlane(cam);

            PlanePlacer.FitToCameraView(plane, cam.projectionMatrix, PlanePlacer.PlaneDistance);

            var corners = Corners(cam, plane);
            foreach (var p in corners)
                Assert.That(p.z, Is.EqualTo(PlanePlacer.PlaneDistance).Within(1e-3f), "depth");
            Assert.That(corners.Select(Rounded), Is.EquivalentTo(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) }));
            // The plane's lit (+Y) side faces the camera.
            Assert.That(Vector3.Dot(plane.up, cam.transform.forward), Is.EqualTo(-1f).Within(1e-4f));
        }

        /// The zoom magnifies the picture about the view's centre: 1.1 puts its corners at -0.05 and 1.05.
        [Test]
        public void FitToCameraView_ZoomMagnifiesAboutTheCentre()
        {
            Camera cam = NewArCamera();
            Transform plane = NewPlane(cam);

            PlanePlacer.FitToCameraView(plane, cam.projectionMatrix, PlanePlacer.PlaneDistance, 1.1f);

            Assert.That(Corners(cam, plane).Select(Rounded),
                Is.EquivalentTo(new[] { new Vector2(-0.05f, -0.05f), new Vector2(-0.05f, 1.05f), new Vector2(1.05f, -0.05f), new Vector2(1.05f, 1.05f) }));
        }

        /// Each texel (u, v) of the picture lands on the camera's viewport point (u, v): the picture
        /// is neither flipped nor turned, and the eyes see it on that pixel's world point.
        [Test]
        public void FitToCameraView_PutsEachPictureTexelOnItsCameraPixel()
        {
            Camera cam = NewArCamera();
            Transform plane = NewPlane(cam);
            PlanePlacer.FitToCameraView(plane, cam.projectionMatrix, PlanePlacer.PlaneDistance);

            Mesh mesh = plane.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            for (int i = 0; i < vertices.Length; i += 7)
            {
                Vector3 p = cam.WorldToViewportPoint(plane.TransformPoint(vertices[i]));
                Assert.That(p.x, Is.EqualTo(uvs[i].x).Within(1e-3f), $"u of vertex {i}");
                Assert.That(p.y, Is.EqualTo(uvs[i].y).Within(1e-3f), $"v of vertex {i}");
            }
        }

        /// The same holds wherever the camera is and however the phone is held.
        [TestCase(0f, 0f, 0f)]
        [TestCase(10f, 40f, 5f)]
        [TestCase(-30f, 170f, 90f)]
        public void FitToCameraView_FollowsTheCameraPose(float x, float y, float z)
        {
            Camera cam = NewArCamera();
            cam.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(x, y, z));
            Transform plane = NewPlane(cam);
            PlanePlacer.FitToCameraView(plane, cam.projectionMatrix, PlanePlacer.PlaneDistance, PlanePlacer.PictureZoom);

            Vector3 centre = cam.WorldToViewportPoint(plane.position);
            Assert.That(centre.x, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(centre.y, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(centre.z, Is.EqualTo(PlanePlacer.PlaneDistance).Within(1e-3f));
        }

        static List<Vector3> Corners(Camera cam, Transform plane)
        {
            Vector3 e = plane.GetComponent<MeshFilter>().sharedMesh.bounds.extents;
            return new[] { new Vector3(-e.x, 0, -e.z), new Vector3(-e.x, 0, e.z), new Vector3(e.x, 0, -e.z), new Vector3(e.x, 0, e.z) }
                .Select(local => cam.WorldToViewportPoint(plane.TransformPoint(local))).ToList();
        }

        static Vector2 Rounded(Vector3 p) => new(Mathf.Round(p.x * 1000f) / 1000f, Mathf.Round(p.y * 1000f) / 1000f);

        Camera NewArCamera()
        {
            var go = new GameObject("AR Camera");
            m_Created.Add(go);
            var cam = go.AddComponent<Camera>();
            cam.aspect = 4f / 3f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.projectionMatrix = s_ArKitProjection;
            return cam;
        }

        Transform NewPlane(Camera cam)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            m_Created.Add(plane);
            Assert.That(plane.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(PlanePlacer.PlaneMeshSize).Within(1e-4f), "built-in Plane size");
            plane.transform.SetParent(cam.transform, false);
            return plane.transform;
        }

        // An OpenGL-style projection with ARKit's focal terms and principal-point offset.
        static Matrix4x4 Projection(float m00, float m11, float m02, float m12)
        {
            const float near = 0.1f, far = 50f;
            var p = Matrix4x4.zero;
            p.m00 = m00;
            p.m11 = m11;
            p.m02 = m02;
            p.m12 = m12;
            p.m22 = -(far + near) / (far - near);
            p.m23 = -2f * far * near / (far - near);
            p.m32 = -1f;
            return p;
        }
    }
}
