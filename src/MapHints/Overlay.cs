using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapHints
{
    /// <summary>
    /// Draws markers as plain sprites in map world space. The map camera only renders layer 13 and map items
    /// live at world tile coordinates, so a sprite at (x, y) on that layer lands on the matching map spot.
    /// </summary>
    internal sealed class Overlay
    {
        const int MapLayer = 13;
        static Sprite _unit;   // 1x1 world unit white square

        GameObject _root;

        static Sprite Unit()
        {
            if (_unit != null) return _unit;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            _unit = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.zero, 4f);   // 4px at 4 ppu = 1 unit
            UnityEngine.Object.DontDestroyOnLoad(tex);
            UnityEngine.Object.DontDestroyOnLoad(_unit);
            return _unit;
        }

        public void Clear()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        public void Begin()
        {
            Clear();
            _root = new GameObject("MapHintsOverlay") { layer = MapLayer };
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        void Rect(float x, float y, float w, float h, Color c, int order)
        {
            var go = new GameObject("m") { layer = MapLayer };
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(x, y, -1f);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Unit();
            sr.color = c;
            sr.sortingOrder = order;
        }

        /// <summary>Faint fill plus a solid frame, so a large zone does not hide the room underneath.</summary>
        public void Zone(float x, float y, float w, float h, Color c)
        {
            const float t = 0.5f;
            Rect(x, y, w, h, new Color(c.r, c.g, c.b, 0.16f), 500);
            Rect(x, y, w, t, c, 501);
            Rect(x, y + h - t, w, t, c, 501);
            Rect(x, y, t, h, c, 501);
            Rect(x + w - t, y, t, h, c, 501);
        }

        /// <summary>Square marker centred on (x, y) with a light outline.</summary>
        public void Marker(float x, float y, Color c)
        {
            Rect(x - 1.25f, y - 1.25f, 2.5f, 2.5f, new Color(1f, 1f, 1f, 0.95f), 502);
            Rect(x - 0.95f, y - 0.95f, 1.9f, 1.9f, c, 503);
        }
    }
}
