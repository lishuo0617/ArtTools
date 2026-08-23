using UnityEngine;
using System.Collections;
using System.IO;

namespace ArtTools.ImageTools
{
    public class ImageExporterController : MonoBehaviour
    {
        [HideInInspector] public Camera cam;
        [HideInInspector] public string imageFormat;
        [HideInInspector] public bool isEnabledAlpha;
        [HideInInspector] public Vector2 resolution;
        [HideInInspector] public int frameCount;
        [HideInInspector] public string fileName;
        [HideInInspector] public string filePath;

        void Awake()
        {
            if (cam == null)
                cam = Camera.main;
        }

        void Start()
        {
            if (frameCount > 0)
                Time.captureFramerate = frameCount;
        }

        public void TakeSequenceScreenShot()
        {
            StartCoroutine(CaptureCoroutine());
        }

        private IEnumerator CaptureCoroutine()
        {
            yield return new WaitForEndOfFrame();

            if (cam == null)
                yield break;

            int width = Mathf.RoundToInt(resolution.x);
            int height = Mathf.RoundToInt(resolution.y);

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            TextureFormat texFormat = isEnabledAlpha ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            Texture2D tex = new Texture2D(width, height, texFormat, false);

            RenderTexture prevRT = RenderTexture.active;
            RenderTexture prevCamRT = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevCamRT;
            RenderTexture.active = prevRT;

            byte[] bytes = imageFormat == ".jpg"
                ? ImageConversion.EncodeToJPG(tex)
                : ImageConversion.EncodeToPNG(tex);

            string path = Path.Combine(
                filePath,
                $"{fileName}_{Time.frameCount}{imageFormat}"
            );

            File.WriteAllBytes(path, bytes);

#if UNITY_EDITOR
            DestroyImmediate(rt);
            DestroyImmediate(tex);
#else
            Destroy(rt);
            Destroy(tex);
#endif
        }
    }
}
