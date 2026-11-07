using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SC_FPSCounter : MonoBehaviour
{
    public float updateInterval = 0.5f; //How often should the number update

    float accum = 0.0f;
    int frames = 0;
    float timeleft;
    float fps;

    GUIStyle textStyle = new GUIStyle();

    // Use this for initialization
    void Start()
    {
        Debug.Log($"FPS Counter Started | " +
          $"IMGUI: {SystemInfo.graphicsDeviceType} | " +
          $"TimeScale: {Time.timeScale} | " +
          $"FrameRate: {Application.targetFrameRate}");

#if RELEASE
Debug.LogError("FPS Counter DESTROYED: RELEASE define detected!");
#endif
        timeleft = updateInterval;
        useGUILayout = false;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 25;
#if RELEASE
Destroy(gameObject);
#endif
    }

    // Update is called once per frame
    void Update()
    {
        timeleft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        // Interval ended - update GUI text and start new interval
        if (timeleft <= 0.0)
        {
            // display two fractional digits (f2 format)
            fps = (accum / frames);
            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        GUI.depth = -1000;
        GUI.color = Color.white;

        GUIStyle style = GUI.skin.label;
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.red;

        GUI.Label(
            new Rect(20, 20, 500, 100),
            "FPS: " + fps.ToString("F2"),
            style
        );
    }
}