#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public static class IconGenerator
{
    private struct IconDef
    {
        public string FileName;
        public string Label;
        public Color BgColor;
        public Color SymbolColor;

        public IconDef(string _fileName, string _label, Color _bgColor, Color _symbolColor)
        {
            FileName = _fileName;
            Label = _label;
            BgColor = _bgColor;
            SymbolColor = _symbolColor;
        }
    }

    [MenuItem("Tools/Generate Item Icons")]
    public static void GenerateIcons()
    {
        string outputDir = "Assets/GameResource/Image/ItemIcon";
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var icons = new List<IconDef>
        {
            // 자원
            new IconDef("wood_icon", "W", new Color(0.55f, 0.35f, 0.15f), Color.white),
            new IconDef("stone_icon", "S", new Color(0.5f, 0.5f, 0.55f), Color.white),
            new IconDef("herb_icon", "H", new Color(0.2f, 0.65f, 0.2f), Color.white),
            new IconDef("leather_icon", "L", new Color(0.6f, 0.4f, 0.2f), Color.white),
            new IconDef("bone_icon", "B", new Color(0.85f, 0.82f, 0.75f), new Color(0.3f, 0.3f, 0.3f)),
            new IconDef("ironore_icon", "Fe", new Color(0.4f, 0.4f, 0.45f), new Color(0.8f, 0.6f, 0.2f)),

            // 소비
            new IconDef("potion_hp_icon", "+", new Color(0.8f, 0.15f, 0.15f), Color.white),
            new IconDef("potion_cold_icon", "~", new Color(0.15f, 0.6f, 0.8f), Color.white),
            new IconDef("bandage_icon", "+", new Color(0.9f, 0.85f, 0.75f), new Color(0.8f, 0.2f, 0.2f)),

            // 장비
            new IconDef("sword_icon", "/", new Color(0.3f, 0.3f, 0.35f), new Color(0.8f, 0.8f, 0.9f)),
            new IconDef("ironsword_icon", "/", new Color(0.2f, 0.2f, 0.3f), new Color(0.9f, 0.7f, 0.2f)),
            new IconDef("shield_icon", "O", new Color(0.45f, 0.3f, 0.15f), new Color(0.8f, 0.7f, 0.4f)),
            new IconDef("leatherarmor_icon", "A", new Color(0.5f, 0.35f, 0.2f), new Color(0.9f, 0.8f, 0.6f)),

            // 건물
            new IconDef("campfire_icon", "*", new Color(0.6f, 0.3f, 0.1f), new Color(1f, 0.7f, 0.1f)),
            new IconDef("torch_icon", "!", new Color(0.5f, 0.25f, 0.1f), new Color(1f, 0.8f, 0.2f)),
        };

        foreach (var icon in icons)
        {
            var tex = CreateIcon(icon.Label, icon.BgColor, icon.SymbolColor);
            string path = $"{outputDir}/{icon.FileName}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        AssetDatabase.Refresh();

        // Addressable 주소 설정 (파일명 = Address)
        foreach (var icon in icons)
        {
            string path = $"{outputDir}/{icon.FileName}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100;
                importer.SaveAndReimport();
            }

            // Addressable 등록
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.address = icon.FileName;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[IconGenerator] {icons.Count} icons generated and registered as Addressable!");
    }

    private static Texture2D CreateIcon(string _label, Color _bgColor, Color _symbolColor)
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        // 배경: 둥근 사각형
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cornerRadius = 16f;
                float dx = Mathf.Max(0, Mathf.Abs(x - size / 2f) - (size / 2f - cornerRadius));
                float dy = Mathf.Max(0, Mathf.Abs(y - size / 2f) - (size / 2f - cornerRadius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= cornerRadius)
                {
                    // 그라데이션 효과
                    float gradient = 1f - (float)y / size * 0.3f;
                    Color c = _bgColor * gradient;
                    c.a = 1f;

                    // 테두리
                    float borderDist = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
                    if (borderDist < 3)
                    {
                        c = Color.Lerp(Color.black, c, borderDist / 3f);
                        c.a = 1f;
                    }

                    tex.SetPixel(x, y, c);
                }
                else
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }

        // 심볼/글자 그리기 (간단한 비트맵 폰트)
        DrawSymbol(tex, _label, _symbolColor, size);

        tex.Apply();
        return tex;
    }

    private static void DrawSymbol(Texture2D _tex, string _label, Color _color, int _size)
    {
        // 중앙에 큰 원형 심볼 영역
        int centerX = _size / 2;
        int centerY = _size / 2;
        int radius = _size / 4;

        // 심볼별 패턴
        switch (_label)
        {
            case "W": // 나무 — 세로 막대
                DrawRect(_tex, centerX - 4, centerY - radius, 8, radius * 2, _color);
                DrawRect(_tex, centerX - radius, centerY - 4, radius * 2, 8, _color * 0.8f);
                break;
            case "S": // 돌 — 다이아몬드
                DrawDiamond(_tex, centerX, centerY, radius, _color);
                break;
            case "H": // 약초 — 잎 모양
                DrawLeaf(_tex, centerX, centerY, radius, _color);
                break;
            case "L": // 가죽 — 사각형
                DrawRect(_tex, centerX - radius + 4, centerY - radius + 8, (radius - 4) * 2, (radius - 8) * 2, _color);
                break;
            case "B": // 뼈 — X자
                DrawRect(_tex, centerX - radius, centerY - 3, radius * 2, 6, _color);
                DrawCircle(_tex, centerX - radius, centerY, 6, _color);
                DrawCircle(_tex, centerX + radius, centerY, 6, _color);
                break;
            case "Fe": // 철광석 — 육각형 느낌
                DrawDiamond(_tex, centerX, centerY, radius, _color);
                DrawDiamond(_tex, centerX, centerY, radius - 6, _color * 0.7f);
                break;
            case "+": // 회복 — 십자가
                DrawRect(_tex, centerX - 4, centerY - radius, 8, radius * 2, _color);
                DrawRect(_tex, centerX - radius, centerY - 4, radius * 2, 8, _color);
                break;
            case "~": // 체온 — 물결
                for (int x = centerX - radius; x < centerX + radius; x++)
                {
                    int waveY = centerY + (int)(Mathf.Sin((x - centerX) * 0.15f) * 8);
                    DrawRect(_tex, x, waveY - 3, 1, 6, _color);
                }
                break;
            case "/": // 검 — 대각선
                for (int i = -radius; i < radius; i++)
                {
                    DrawRect(_tex, centerX + i - 2, centerY - i - 2, 5, 5, _color);
                }
                break;
            case "O": // 방패 — 원
                DrawCircle(_tex, centerX, centerY, radius, _color);
                DrawCircle(_tex, centerX, centerY, radius - 6, _color * 0.6f);
                break;
            case "A": // 갑옷 — 역삼각형
                for (int y = centerY - radius; y < centerY + radius; y++)
                {
                    int halfW = (int)((float)(centerY + radius - y) / (radius * 2) * radius);
                    DrawRect(_tex, centerX - halfW, y, halfW * 2, 1, _color);
                }
                break;
            case "*": // 모닥불 — 불꽃
                DrawFlame(_tex, centerX, centerY, radius, _color);
                break;
            case "!": // 횃불 — 세로 막대 + 불꽃
                DrawRect(_tex, centerX - 3, centerY - radius, 6, radius + 8, new Color(0.4f, 0.25f, 0.1f));
                DrawFlame(_tex, centerX, centerY - radius + 4, radius / 2, _color);
                break;
        }
    }

    private static void DrawRect(Texture2D _tex, int _x, int _y, int _w, int _h, Color _color)
    {
        for (int y = _y; y < _y + _h; y++)
            for (int x = _x; x < _x + _w; x++)
                if (x >= 0 && x < _tex.width && y >= 0 && y < _tex.height)
                    _tex.SetPixel(x, y, _color);
    }

    private static void DrawCircle(Texture2D _tex, int _cx, int _cy, int _r, Color _color)
    {
        for (int y = _cy - _r; y <= _cy + _r; y++)
            for (int x = _cx - _r; x <= _cx + _r; x++)
            {
                float dist = Mathf.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy));
                if (dist <= _r && x >= 0 && x < _tex.width && y >= 0 && y < _tex.height)
                    _tex.SetPixel(x, y, _color);
            }
    }

    private static void DrawDiamond(Texture2D _tex, int _cx, int _cy, int _r, Color _color)
    {
        for (int y = _cy - _r; y <= _cy + _r; y++)
            for (int x = _cx - _r; x <= _cx + _r; x++)
            {
                if (Mathf.Abs(x - _cx) + Mathf.Abs(y - _cy) <= _r)
                    if (x >= 0 && x < _tex.width && y >= 0 && y < _tex.height)
                        _tex.SetPixel(x, y, _color);
            }
    }

    private static void DrawLeaf(Texture2D _tex, int _cx, int _cy, int _r, Color _color)
    {
        for (int y = _cy - _r; y <= _cy + _r; y++)
            for (int x = _cx - _r; x <= _cx + _r; x++)
            {
                float nx = (float)(x - _cx) / _r;
                float ny = (float)(y - _cy) / _r;
                if (nx * nx + ny * ny * 0.5f <= 0.8f && nx + ny < 1.2f)
                    if (x >= 0 && x < _tex.width && y >= 0 && y < _tex.height)
                        _tex.SetPixel(x, y, _color);
            }
    }

    private static void DrawFlame(Texture2D _tex, int _cx, int _cy, int _r, Color _color)
    {
        for (int y = _cy - _r; y <= _cy + _r; y++)
        {
            float t = (float)(y - (_cy - _r)) / (_r * 2);
            int halfW = (int)((1f - t * t) * _r * 0.8f);
            for (int x = _cx - halfW; x <= _cx + halfW; x++)
            {
                if (x >= 0 && x < _tex.width && y >= 0 && y < _tex.height)
                {
                    Color c = Color.Lerp(_color, new Color(1f, 0.2f, 0f), t);
                    _tex.SetPixel(x, y, c);
                }
            }
        }
    }
}
#endif
