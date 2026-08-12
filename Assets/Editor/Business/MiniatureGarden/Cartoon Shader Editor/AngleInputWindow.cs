using UnityEditor;
using UnityEngine;

/// <summary>修改合并角度的 Modal 小弹窗。</summary>
public class AngleInputWindow : EditorWindow
{
    private float angle = 60f;
    private System.Action<float> onConfirm;

    public static void Show(float currentAngle, System.Action<float> confirm)
    {
        var win = GetWindow<AngleInputWindow>(true, "修改合并角度", true);
        win.angle = currentAngle;
        win.onConfirm = confirm;
        win.minSize = win.maxSize = new Vector2(260, 100);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("合并角度", EditorStyles.boldLabel);
        angle = EditorGUILayout.FloatField("角度（0-180）", angle);
        angle = Mathf.Clamp(angle, 0f, 180f);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("取消"))
        {
            Close();
            return;
        }
        if (GUILayout.Button("确定"))
        {
            onConfirm?.Invoke(angle);
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
