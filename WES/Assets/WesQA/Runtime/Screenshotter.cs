using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesQA
{
    /// <summary>화면을 캡처→요청 width로 리사이즈→JPG→base64. 메인스레드(서버 펌프)에서 호출됨.
    /// 서버 펌프는 프레임 중간에 돌기 때문에 ScreenCapture.CaptureScreenshotAsTexture()는
    /// 'end of frame' 전이라 실패한다. 활성 카메라들을 depth 순으로 RenderTexture에 수동 렌더해서 픽셀을 얻는다.</summary>
    public static class Screenshotter
    {
        public static object[] Capture(int width)
        {
            int srcW = Screen.width;
            int srcH = Screen.height;
            int outW = (width > 0 && width < srcW) ? width : srcW;
            int outH = Mathf.Max(1, Mathf.RoundToInt(srcH * (outW / (float)srcW)));

            var rt = RenderTexture.GetTemporary(outW, outH, 24, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            Texture2D outTex = null;
            try
            {
                // depth 오름차순 정렬 (낮은 depth부터 그려야 위 카메라가 덮어씀)
                var cams = new List<Camera>(Camera.allCameras);
                cams.Sort((a, b) => a.depth.CompareTo(b.depth));

                bool first = true;
                foreach (var cam in cams)
                {
                    if (cam == null || !cam.isActiveAndEnabled) continue;
                    var prevTarget = cam.targetTexture;
                    cam.targetTexture = rt;
                    if (first)
                    {
                        // 첫 카메라: 명시적으로 RT를 클리어
                        RenderTexture.active = rt;
                        GL.Clear(true, true, Color.black);
                        RenderTexture.active = prevActive;
                        first = false;
                    }
                    cam.Render();
                    cam.targetTexture = prevTarget;
                }

                if (first)
                {
                    // 카메라가 하나도 없었던 경우라도 빈 검정 텍스처 반환
                    RenderTexture.active = rt;
                    GL.Clear(true, true, Color.black);
                }

                RenderTexture.active = rt;
                outTex = new Texture2D(outW, outH, TextureFormat.RGB24, false);
                outTex.ReadPixels(new Rect(0, 0, outW, outH), 0, 0);
                outTex.Apply();

                byte[] jpg = outTex.EncodeToJPG(80);
                return new object[] { Convert.ToBase64String(jpg), "jpg" };
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                if (outTex != null) UnityEngine.Object.Destroy(outTex);
            }
        }
    }
}
