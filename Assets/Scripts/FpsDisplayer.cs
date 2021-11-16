using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This FPS monitor is from http://wiki.unity3d.com/index.php?title=FramesPerSecond

public class FpsDisplayer : MonoBehaviour
{
    protected float lastTime = 0;
    protected float deltaTime;
    protected float fixedDeltaTime;

    protected bool showFps = false;

    private void Start()
    {
        Config.Get(() => Config.showFps, false);
    }

    void Update () {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    protected void OnGUI()
    {
        if (showFps)
        {
            int w = Screen.width, h = Screen.height;
            GUIStyle style = new GUIStyle();

            Rect rect = new Rect(0, 0, w, h * 2 / 100);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = h * 4 / 100;
            style.normal.textColor = new Color(0.5f, 0.0f, 0.0f, 1.0f);
            float msec = deltaTime * 1000.0f;
            float fps = 1.0f / deltaTime;
            float guiFps = Time.time - lastTime;
            lastTime = Time.time;
            string text = string.Format("{0:0.0} ms ({1:0.} fps) ({1:0.} gui fps)", msec, fps, guiFps);
            GUI.Label(rect, text, style);
        }
    }
}
