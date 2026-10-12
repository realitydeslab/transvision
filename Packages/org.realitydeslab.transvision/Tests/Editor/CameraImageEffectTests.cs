using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TransVision.Tests
{
    /// <summary>
    /// The effect runs inside URP (CameraImageProcessor's Render Graph pass); it must produce the
    /// image the former Graphics.Blit chain did, from the same still and the same shader time.
    /// </summary>
    public class CameraImageEffectTests
    {
        const string Root = "Packages/org.realitydeslab.transvision/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        readonly List<Object> m_Created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Created)
                Object.DestroyImmediate(o);
            m_Created.Clear();
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        [Test]
        public void UrpPass_MatchesTheFormerBlitChain()
        {
            var still = Load<Texture2D>("Debug/IMG_1272.jpg");
            var cameraImage = Load<RenderTexture>("ShaderGraph/Step0_CameraImage.renderTexture");
            var maskTexture = Load<RenderTexture>("ShaderGraph/Step1_MaskRT.renderTexture");
            var blurred = Load<RenderTexture>("ShaderGraph/Step2_BlurredMaskRT.renderTexture");
            var offsetTexture = Load<RenderTexture>("ShaderGraph/Step3_OffsetRT.renderTexture");
            var result = Load<RenderTexture>("ShaderGraph/Step10_DistortionRT.renderTexture");
            var mask = Load<Material>("ShaderGraph/Step1_CalculateMask.mat");
            var blur = Load<Material>("ShaderGraph/Step2_Blur.mat");
            var offset = Load<Material>("ShaderGraph/Step3_GenerateOffset.mat");
            var distortion = Load<Material>("ShaderGraph/Step10_Distortion.mat");

            var processorObject = new GameObject("Camera Image Processor");
            m_Created.Add(processorObject);
            var processor = processorObject.AddComponent<CameraImageProcessor>();
            var so = new SerializedObject(processor);
            so.FindProperty("m_TestTexture").objectReferenceValue = still;
            so.FindProperty("m_RenderTexture").objectReferenceValue = cameraImage;
            so.FindProperty("m_CalculateMaskMaterial").objectReferenceValue = mask;
            so.FindProperty("m_MaskTexture").objectReferenceValue = maskTexture;
            so.FindProperty("m_BlurMaterial").objectReferenceValue = blur;
            so.FindProperty("m_BlurredMaskTexture").objectReferenceValue = blurred;
            so.FindProperty("m_GenerateOffsetMaterial").objectReferenceValue = offset;
            so.FindProperty("m_OffsetTexture").objectReferenceValue = offsetTexture;
            so.FindProperty("m_DistortionMaterial").objectReferenceValue = distortion;
            so.FindProperty("m_DistortionTexture").objectReferenceValue = result;
            so.FindProperty("m_Effect").enumValueIndex = (int)CameraImageProcessor.Effect.Classic;
            so.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Camera");
            m_Created.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(64, 64, 24);
            m_Created.Add(target);
            camera.targetTexture = target;

            // Edit mode runs no MonoBehaviour messages: subscribe as Play mode's OnEnable would.
            typeof(CameraImageProcessor).GetMethod("OnEnable", Private).Invoke(processor, null);
            try
            {
                Clear(result);
                camera.Render();
            }
            finally
            {
                typeof(CameraImageProcessor).GetMethod("OnDisable", Private).Invoke(processor, null);
                typeof(CameraImageProcessor).GetMethod("OnDestroy", Private).Invoke(processor, null);
            }
            Color32[] fromUrp = Read(result);

            // The former chain, under the shader time URP just set.
            Graphics.Blit(still, cameraImage);
            mask.SetTexture("_CameraImage", cameraImage);
            Graphics.Blit(cameraImage, maskTexture, mask, 0);
            blur.SetTexture("_TextureIn", maskTexture);
            Graphics.Blit(maskTexture, blurred, blur, 0);
            offset.SetTexture("_HeightMap", blurred);
            Graphics.Blit(null, offsetTexture, offset, 0);
            distortion.SetTexture("_CameraTexture", cameraImage);
            distortion.SetTexture("_MaskTexture", blurred);
            Graphics.Blit(cameraImage, result, distortion, 0);
            Color32[] fromBlit = Read(result);

            int maxDifference = 0, lit = 0;
            for (int i = 0; i < fromUrp.Length; i++)
            {
                maxDifference = Mathf.Max(maxDifference,
                    Mathf.Abs(fromUrp[i].r - fromBlit[i].r), Mathf.Abs(fromUrp[i].g - fromBlit[i].g), Mathf.Abs(fromUrp[i].b - fromBlit[i].b));
                if (fromUrp[i].r + fromUrp[i].g + fromUrp[i].b > 24)
                    lit++;
            }
            Assert.That(lit, Is.GreaterThan(fromUrp.Length / 2), "the URP pass wrote the result");
            Assert.That(maxDifference, Is.LessThanOrEqualTo(1));
        }

        /// With a camera frame, Step0 is the frame converted by the camera-image shader (on device the
        /// AR background shader drew nothing here: Metal rejects its depth write without a depth target).
        [Test]
        public void UrpPass_WritesTheCameraFrameIntoStep0()
        {
            var processor = NewProcessor(out var camera);
            var cameraImage = Load<RenderTexture>("ShaderGraph/Step0_CameraImage.renderTexture");
            typeof(CameraImageProcessor).GetMethod("OnEnable", Private).Invoke(processor, null);
            try
            {
                // A uniform mid-grey frame: Y 0.5, neutral chroma.
                var material = (Material)typeof(CameraImageProcessor).GetField("m_CameraImageMaterial", Private).GetValue(processor);
                material.SetTexture("_textureY", Texture(new Color32(128, 0, 0, 255)));
                material.SetTexture("_textureCbCr", Texture(new Color32(128, 128, 0, 255)));
                material.SetMatrix("_TransVisionDisplayTransform", Matrix4x4.identity);
                typeof(CameraImageProcessor).GetField("m_HasCameraFrame", Private).SetValue(processor, true);
                Clear(cameraImage);
                camera.Render();
            }
            finally
            {
                typeof(CameraImageProcessor).GetMethod("OnDisable", Private).Invoke(processor, null);
                typeof(CameraImageProcessor).GetMethod("OnDestroy", Private).Invoke(processor, null);
            }

            Color32[] pixels = Read(cameraImage);
            byte expected = (byte)Mathf.RoundToInt(Expected(128 / 255f, 128 / 255f, 128 / 255f).r * 255f);
            Assert.That(pixels[pixels.Length / 2].r, Is.EqualTo(expected).Within(3), "centre of Step0");
            Assert.That(pixels[0].g, Is.EqualTo(expected).Within(3), "corner of Step0");
        }

        /// On iOS 27 AR Foundation writes the frame's (wrong) display transform into the AR background
        /// material when the frame arrives; the HoloKit SDK, which replaced that material, corrects it
        /// later in the frame. Step0 must use the corrected transform the material holds at render time.
        [Test]
        public void CameraImage_UsesTheBackgroundTransformCorrectedBeforeRendering()
        {
            var processor = NewProcessor(out var camera);
            var arCamera = new GameObject("AR Camera");
            m_Created.Add(arCamera);
            var background = arCamera.AddComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            var replaced = new Material(Shader.Find("Unlit/Color"));
            m_Created.Add(replaced);
            background.customMaterial = replaced;
            background.useCustomMaterial = true;
            var so = new SerializedObject(processor);
            so.FindProperty("m_ARCameraBackground").objectReferenceValue = background;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fromFrame = Matrix4x4.TRS(new Vector3(0, 1, 0), Quaternion.Euler(0, 0, 90), Vector3.one);
            var corrected = Matrix4x4.TRS(new Vector3(1, 0, 0), Quaternion.Euler(0, 0, -90), new Vector3(1, -1, 1));
            typeof(CameraImageProcessor).GetMethod("OnEnable", Private).Invoke(processor, null);
            try
            {
                // AR Foundation, then TransVision, receive the frame...
                replaced.SetMatrix("_UnityDisplayTransform", fromFrame);
                var frame = new UnityEngine.XR.ARFoundation.ARCameraFrameEventArgs
                {
                    displayMatrix = fromFrame, textures = new List<Texture2D>(), propertyNameIds = new List<int>(),
                };
                typeof(CameraImageProcessor).GetMethod("OnCameraFrameReceived", Private).Invoke(processor, new object[] { frame });
                // ...then the SDK corrects the replaced material, then the frame renders.
                replaced.SetMatrix("_UnityDisplayTransform", corrected);
                camera.Render();

                var material = (Material)typeof(CameraImageProcessor).GetField("m_CameraImageMaterial", Private).GetValue(processor);
                Assert.That(material.GetMatrix("_TransVisionDisplayTransform"), Is.EqualTo(corrected));
            }
            finally
            {
                typeof(CameraImageProcessor).GetMethod("OnDisable", Private).Invoke(processor, null);
                typeof(CameraImageProcessor).GetMethod("OnDestroy", Private).Invoke(processor, null);
            }
        }

        /// Mono: the AR camera shows the effect's result full screen, the right way up (no plane).
        [Test]
        public void MonoCamera_ShowsTheResultFullScreen()
        {
            var processor = NewProcessor(out var camera);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta; // what the camera shows without the result
            var background = camera.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            background.enabled = false;
            var replaced = new Material(Shader.Find("Unlit/Color")); // as the HoloKit SDK replaces it on iOS 27
            m_Created.Add(replaced);
            background.customMaterial = replaced;
            background.useCustomMaterial = true;
            var so = new SerializedObject(processor);
            so.FindProperty("m_ARCameraBackground").objectReferenceValue = background;
            // Top half green, bottom half blue: a flip or a missing copy shows.
            const int w = 192, h = 108;
            var still = new Texture2D(w, h, TextureFormat.RGBA32, false);
            m_Created.Add(still);
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = i / w >= h / 2 ? new Color32(30, 200, 60, 255) : new Color32(40, 70, 210, 255);
            still.SetPixels32(pixels);
            still.Apply();
            so.FindProperty("m_TestTexture").objectReferenceValue = still;
            so.ApplyModifiedPropertiesWithoutUndo();
            typeof(CameraImageProcessor).GetMethod("OnEnable", Private).Invoke(processor, null);
            try
            {
                camera.Render();
            }
            finally
            {
                typeof(CameraImageProcessor).GetMethod("OnDisable", Private).Invoke(processor, null);
                typeof(CameraImageProcessor).GetMethod("OnDestroy", Private).Invoke(processor, null);
            }

            // Away from the red and from the halves' border the picture is the still itself.
            Color32[] shown = Read(camera.targetTexture);
            int sw = camera.targetTexture.width, sh = camera.targetTexture.height;
            foreach (var (u, v) in new[] { (0.5f, 0.8f), (0.5f, 0.2f), (0.1f, 0.9f), (0.9f, 0.1f) })
            {
                Color32 a = shown[(int)(v * sh) * sw + (int)(u * sw)];
                Color32 b = pixels[(int)(v * h) * w + (int)(u * w)];
                Assert.That(Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)), Is.LessThanOrEqualTo(8),
                    $"screen ({u}, {v}): shown {a}, still {b}");
            }
        }

        /// Stereo's picture is the camera's whole image: its transform keeps the screen transform's
        /// orientation and centre without the crop, and the screen's view sits at ScreenCrop in it.
        [TestCase(false)]
        [TestCase(true)]
        public void WholeImageTransform_UncropsTheScreenTransform(bool portrait)
        {
            var screen = Matrix4x4.identity;
            Vector2 expectedCrop;
            if (portrait)
            {
                // Screen x along the image's y (cropped to 0.6), screen y along the image's x, flipped.
                screen.m00 = 0f; screen.m01 = 0.6f; screen.m10 = -1f; screen.m11 = 0f; screen.m20 = 1f; screen.m21 = 0.2f;
                expectedCrop = new Vector2(0.6f, 1f);
            }
            else
            {
                // iPhone 15 Pro in LandscapeLeft, as the HoloKit SDK computes it on device.
                screen.m00 = 1f; screen.m01 = 0f; screen.m10 = 0f; screen.m11 = -0.615f; screen.m20 = 0f; screen.m21 = 0.808f;
                expectedCrop = new Vector2(1f, 0.615f);
            }

            Matrix4x4 whole = CameraImageProcessor.WholeImageTransform(screen, out Vector2 crop);

            Assert.That(crop.x, Is.EqualTo(expectedCrop.x).Within(1e-5f));
            Assert.That(crop.y, Is.EqualTo(expectedCrop.y).Within(1e-5f));
            var centre = new Vector2(0.5f, 0.5f);
            Assert.That(Vector2.Distance(CameraImageProcessor.ImageUv(whole, centre), CameraImageProcessor.ImageUv(screen, centre)), Is.LessThan(1e-5f), "same centre");
            foreach (var corner in new[] { Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(1, 0) })
            {
                Vector2 image = CameraImageProcessor.ImageUv(whole, corner);
                foreach (float c in new[] { image.x, image.y })
                    Assert.That(Mathf.Min(Mathf.Abs(c), Mathf.Abs(c - 1f)), Is.LessThan(0.01f), $"corner {corner} reaches the image's edge ({image})");
            }
            var random = new System.Random(3);
            for (int i = 0; i < 20; i++)
            {
                var s = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                Vector2 inPicture = s * crop + (Vector2.one - crop) * 0.5f;
                Assert.That(Vector2.Distance(CameraImageProcessor.ImageUv(whole, inPicture), CameraImageProcessor.ImageUv(screen, s)), Is.LessThan(1e-5f), $"screen point {s}");
            }

            // The plane's projection widens by the same crop: 2 fy / 1440 for the whole sensor height.
            var projection = Matrix4x4.Perspective(34.5f, 2.168f, 0.1f, 50f);
            projection.m12 = 0.0227f;
            Matrix4x4 wide = PlanePlacer.WholeImageProjection(projection, crop);
            Assert.That(wide.m00, Is.EqualTo(projection.m00 * crop.x).Within(1e-6f));
            Assert.That(wide.m11, Is.EqualTo(projection.m11 * crop.y).Within(1e-6f));
            Assert.That(wide.m12, Is.EqualTo(0.0227f * crop.y).Within(1e-6f));
        }

        /// Without an AR background transform (no SDK correction, or none written yet), the frame's.
        [Test]
        public void CameraImage_FallsBackToTheFrameTransform()
        {
            var processor = NewProcessor(out var camera);
            var fromFrame = Matrix4x4.TRS(new Vector3(0, 1, 0), Quaternion.Euler(0, 0, 90), Vector3.one);
            typeof(CameraImageProcessor).GetMethod("OnEnable", Private).Invoke(processor, null);
            try
            {
                var frame = new UnityEngine.XR.ARFoundation.ARCameraFrameEventArgs
                {
                    displayMatrix = fromFrame, textures = new List<Texture2D>(), propertyNameIds = new List<int>(),
                };
                typeof(CameraImageProcessor).GetMethod("OnCameraFrameReceived", Private).Invoke(processor, new object[] { frame });
                camera.Render();

                var material = (Material)typeof(CameraImageProcessor).GetField("m_CameraImageMaterial", Private).GetValue(processor);
                Assert.That(material.GetMatrix("_TransVisionDisplayTransform"), Is.EqualTo(fromFrame));
            }
            finally
            {
                typeof(CameraImageProcessor).GetMethod("OnDisable", Private).Invoke(processor, null);
                typeof(CameraImageProcessor).GetMethod("OnDestroy", Private).Invoke(processor, null);
            }
        }

        CameraImageProcessor NewProcessor(out Camera camera)
        {
            var processorObject = new GameObject("Camera Image Processor");
            m_Created.Add(processorObject);
            var processor = processorObject.AddComponent<CameraImageProcessor>();
            var so = new SerializedObject(processor);
            so.FindProperty("m_TestTexture").objectReferenceValue = Load<Texture2D>("Debug/IMG_1272.jpg");
            so.FindProperty("m_RenderTexture").objectReferenceValue = Load<RenderTexture>("ShaderGraph/Step0_CameraImage.renderTexture");
            so.FindProperty("m_CalculateMaskMaterial").objectReferenceValue = Load<Material>("ShaderGraph/Step1_CalculateMask.mat");
            so.FindProperty("m_MaskTexture").objectReferenceValue = Load<RenderTexture>("ShaderGraph/Step1_MaskRT.renderTexture");
            so.FindProperty("m_BlurMaterial").objectReferenceValue = Load<Material>("ShaderGraph/Step2_Blur.mat");
            so.FindProperty("m_BlurredMaskTexture").objectReferenceValue = Load<RenderTexture>("ShaderGraph/Step2_BlurredMaskRT.renderTexture");
            so.FindProperty("m_GenerateOffsetMaterial").objectReferenceValue = Load<Material>("ShaderGraph/Step3_GenerateOffset.mat");
            so.FindProperty("m_OffsetTexture").objectReferenceValue = Load<RenderTexture>("ShaderGraph/Step3_OffsetRT.renderTexture");
            so.FindProperty("m_DistortionMaterial").objectReferenceValue = Load<Material>("ShaderGraph/Step10_Distortion.mat");
            so.FindProperty("m_DistortionTexture").objectReferenceValue = Load<RenderTexture>("ShaderGraph/Step10_DistortionRT.renderTexture");
            so.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Camera");
            m_Created.Add(cameraObject);
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(64, 64, 24);
            m_Created.Add(target);
            camera.targetTexture = target;
            return processor;
        }

        /// The liquid effect spreads red into its surroundings, and its shrink mode does the opposite.
        [TestCase(false)]
        [TestCase(true)]
        public void LiquidEffect_SpreadsRed_OrShrinksIt(bool shrink)
        {
            var processor = NewProcessor(out var camera);
            var so = new SerializedObject(processor);
            so.FindProperty("m_TestTexture").objectReferenceValue = RedDiskOnChecker();
            so.FindProperty("m_Effect").enumValueIndex = (int)CameraImageProcessor.Effect.Liquid;
            so.FindProperty("m_Shrink").boolValue = shrink;
            so.ApplyModifiedPropertiesWithoutUndo();
            var cameraImage = Load<RenderTexture>("ShaderGraph/Step0_CameraImage.renderTexture");
            var result = Load<RenderTexture>("ShaderGraph/Step10_DistortionRT.renderTexture");

            typeof(CameraImageProcessor).GetMethod("OnEnable", Private).Invoke(processor, null);
            try
            {
                Clear(result);
                camera.Render();
            }
            finally
            {
                typeof(CameraImageProcessor).GetMethod("OnDisable", Private).Invoke(processor, null);
                typeof(CameraImageProcessor).GetMethod("OnDestroy", Private).Invoke(processor, null);
            }

            int before = RedPixels(Read(cameraImage)), after = RedPixels(Read(result));
            Assert.That(before, Is.GreaterThan(1000), "the disk reached Step0");
            if (shrink)
                Assert.That(after, Is.LessThan(before * 0.95f), $"red pixels {before} -> {after}");
            else
                Assert.That(after, Is.GreaterThan(before * 1.05f), $"red pixels {before} -> {after}");
        }

        /// Red regions distort and keep moving; the rest of the picture only has a small, fixed
        /// distortion. The composite outside the red must not depend on time or on the ripples (which
        /// travel past the red); inside it must change with them.
        [Test]
        public void LiquidComposite_MovesInsideTheRed_StaysStillOutside()
        {
            const int fieldW = 240, fieldH = 135, waveW = 320, waveH = 180, w = 480, h = 270;
            var material = new Material(Resources.Load<Shader>("TransVisionLiquid"));
            m_Created.Add(material);
            typeof(CameraImageProcessor).GetMethod("ApplyLiquidSettings", Private).Invoke(NewProcessor(out _), new object[] { material });
            material.SetTexture("_TVNoise", LiquidTextures.Noise);
            material.SetTexture("_TVBubble", LiquidTextures.Bubbles);

            // Red field: a soft disk in the middle (radius 24 to 36 field texels).
            static float Red(float u, float v) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 36f,
                Vector2.Distance(new Vector2(u * fieldW, v * fieldH), new Vector2(fieldW / 2f, fieldH / 2f))));
            var field = FloatTexture(fieldW, fieldH, (x, y) => Red((x + 0.5f) / fieldW, (y + 0.5f) / fieldH));
            material.SetTexture("_TVField", field);
            material.SetVector("_TVField_TexelSize", new Vector4(1f / fieldW, 1f / fieldH, fieldW, fieldH));
            material.SetVector("_TVCur_TexelSize", new Vector4(1f / waveW, 1f / waveH, waveW, waveH));
            material.SetFloat("_TVAspect", w / (float)h);

            // A fine grey checker everywhere, so any displacement shows.
            var picture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            m_Created.Add(picture);
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte g = (byte)((((i % w) / 6 + (i / w) / 6) & 1) == 0 ? 90 : 170);
                pixels[i] = new Color32(g, g, g, 255);
            }
            picture.SetPixels32(pixels);
            picture.Apply();

            Color32[] Composite(float time, int seed)
            {
                var random = new System.Random(seed);
                material.SetTexture("_TVCur", FloatTexture(waveW, waveH, (_, _) => (float)random.NextDouble() * 0.4f - 0.2f));
                material.SetFloat("_TVTime", time);
                var target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                m_Created.Add(target);
                Graphics.Blit(picture, target, material, 3);
                return Read(target);
            }

            Color32[] a = Composite(1.0f, 1), b = Composite(7.3f, 2);
            int outsideMax = 0, inside = 0, insideMoved = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float red = Red((x + 0.5f) / w, (y + 0.5f) / h);
                    var pa = a[y * w + x];
                    var pb = b[y * w + x];
                    int diff = Mathf.Max(Mathf.Abs(pa.r - pb.r), Mathf.Abs(pa.g - pb.g), Mathf.Abs(pa.b - pb.b));
                    if (red == 0f)
                        outsideMax = Mathf.Max(outsideMax, diff);
                    else if (red > 0.95f)
                    {
                        inside++;
                        if (diff > 8) insideMoved++;
                    }
                }
            Assert.That(outsideMax, Is.LessThanOrEqualTo(1), "outside the red the picture stays still");
            Assert.That(insideMoved, Is.GreaterThan(inside / 5), $"inside the red it moves ({insideMoved} of {inside} pixels)");
        }

        Texture2D FloatTexture(int width, int height, System.Func<int, int, float> value)
        {
            var texture = new Texture2D(width, height, TextureFormat.RFloat, false, linear: true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var data = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    data[y * width + x] = value(x, y);
            texture.SetPixelData(data, 0);
            texture.Apply();
            m_Created.Add(texture);
            return texture;
        }

        // A saturated red disk on a grey checkerboard, 480x270.
        Texture2D RedDiskOnChecker()
        {
            const int w = 480, h = 270;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool red = (x - w / 2) * (x - w / 2) + (y - h / 2) * (y - h / 2) < 60 * 60;
                    byte grey = (byte)(((x / 24 + y / 24) & 1) == 0 ? 110 : 150);
                    pixels[y * w + x] = red ? new Color32(220, 20, 25, 255) : new Color32(grey, grey, grey, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            m_Created.Add(texture);
            return texture;
        }

        static int RedPixels(Color32[] pixels)
        {
            int count = 0;
            foreach (var p in pixels)
                if (p.r > 96 && p.g < 40 && p.b < 40)
                    count++;
            return count;
        }

        /// The camera-image shader must match ARKit's background shader: YCbCr to sRGB with ARKit's
        /// matrix, then to linear (this project renders in linear space), UVs through the display transform.
        [Test]
        public void CameraImageShader_ConvertsYCbCrAndRemapsUvLikeARKit()
        {
            var shader = Resources.Load<Shader>("TransVisionCameraImage");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            m_Created.Add(material);

            // Y: dark left texel, bright right texel. CbCr: one red-leaning chroma for both.
            var y = Texture(new Color32(64, 0, 0, 255), new Color32(191, 0, 0, 255));
            var cbcr = Texture(new Color32(110, 200, 0, 255));
            material.SetTexture("_textureY", y);
            material.SetTexture("_textureCbCr", cbcr);
            var target = new RenderTexture(2, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            m_Created.Add(target);

            material.SetMatrix("_TransVisionDisplayTransform", Matrix4x4.identity);
            Graphics.Blit(null, target, material);
            Color[] straight = ReadFloat(target);
            AssertColor(straight[0], Expected(64 / 255f, 110 / 255f, 200 / 255f), "left");
            AssertColor(straight[1], Expected(191 / 255f, 110 / 255f, 200 / 255f), "right");

            // u -> 1 - u (row-vector convention: x' = u * m00 + m20): the texels swap.
            var flip = Matrix4x4.identity;
            flip.m00 = -1f;
            flip.m20 = 1f;
            material.SetMatrix("_TransVisionDisplayTransform", flip);
            Graphics.Blit(null, target, material);
            Color[] flipped = ReadFloat(target);
            AssertColor(flipped[0], straight[1], "flipped left");
            AssertColor(flipped[1], straight[0], "flipped right");
        }

        Texture2D Texture(params Color32[] texels)
        {
            var texture = new Texture2D(texels.Length, 1, TextureFormat.RGBA32, false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(texels);
            texture.Apply();
            m_Created.Add(texture);
            return texture;
        }

        // ARKit's YCbCr-to-sRGB matrix, then sRGB to linear with the polynomial both ARKit's shader
        // (FastSRGBToLinear) and UnityCG (GammaToLinearSpace) use; it also covers values above 1.
        static Color Expected(float y, float cb, float cr)
        {
            float r = y + 1.4020f * cr - 0.7010f;
            float g = y - 0.3441f * cb - 0.7141f * cr + 0.5291f;
            float b = y + 1.7720f * cb - 0.8860f;
            return new Color(ToLinear(r), ToLinear(g), ToLinear(b), 1f);
        }

        static float ToLinear(float c) => c * (c * (c * 0.305306011f + 0.682171111f) + 0.012522878f);

        // Half precision in the shader.
        static void AssertColor(Color actual, Color expected, string where)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), where + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), where + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), where + " b");
        }

        static Color[] ReadFloat(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, linear: true);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            var pixels = copy.GetPixels();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        static void Clear(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        static Color32[] Read(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            var pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }
    }
}
