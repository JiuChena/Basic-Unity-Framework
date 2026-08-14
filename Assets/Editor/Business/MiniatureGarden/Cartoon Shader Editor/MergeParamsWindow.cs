using System;
using UnityEditor;
using UnityEngine;

/// <summary>修改合并参数（角度 + 距离）的 Modal 弹窗。角度单位度；距离单位 mm，确定后由绑定组件 ÷1000 转米参与计算。</summary>
public class MergeParamsWindow : EditorWindow
{
    private float angle = 60f;
    private float distanceMm = 2f;
    private Action<float, float> onConfirm;

    /// <param name="currentAngle">当前合并角度（度）。</param>
    /// <param name="currentDistanceMm">当前合并距离（mm）。</param>
    /// <param name="confirm">确定回调，参数依次为（角度°，距离mm）。</param>
    public static void Show(float currentAngle, float currentDistanceMm, Action<float, float> confirm)
    {
        var win = GetWindow<MergeParamsWindow>(true, "修改合并参数", true);
        win.angle = currentAngle;
        win.distanceMm = currentDistanceMm;
        win.onConfirm = confirm;
        win.minSize = win.maxSize = new Vector2(300, 200);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("合并角度", EditorStyles.boldLabel);
        angle = EditorGUILayout.Slider("角度（°）", angle, 0f, 180f);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("合并距离", EditorStyles.boldLabel);
        distanceMm = EditorGUILayout.Slider("距离（mm）", distanceMm, 0f, 100f);

        EditorGUILayout.HelpBox($"当前：{angle:0.#}° / {distanceMm:0.##}mm。距离越大平滑区域越广，角度越大越易合并大角度转折。", MessageType.Info);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("取消"))
        {
            Close();
            return;
        }
        if (GUILayout.Button("确定"))
        {
            onConfirm?.Invoke(angle, distanceMm);
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
