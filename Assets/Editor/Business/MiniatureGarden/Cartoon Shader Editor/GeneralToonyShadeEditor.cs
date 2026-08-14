using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// CT 卡通着色器的分组材质面板编辑器。
/// </summary>
public class GeneralToonyShadeEditor : ShaderGUI
{
    #region Utilities

    // BoxScope 样式：所有分组的外框背景。
    static GUIStyle boxScopeStyle;
    public static GUIStyle BoxScopeStyle
    {
        get
        {
            if (boxScopeStyle == null)
            {
                boxScopeStyle = new GUIStyle(EditorStyles.helpBox);
                var p = boxScopeStyle.padding;
                p.right += 6;
                p.top += 1;
                p.left += 3;
            }
            return boxScopeStyle;
        }
    }

    // ToonLabel 样式：分组标题的粗体大标签。
    static GUIStyle toonLabelStyle;
    public static GUIStyle ToonLabelStyle
    {
        get
        {
            if (toonLabelStyle == null)
            {
                toonLabelStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
                toonLabelStyle.fontStyle = FontStyle.Bold;
            }
            return toonLabelStyle;
        }
    }
    #endregion

    #region MaterialProperties

    //主纹理区
    MaterialProperty albedoMap = null;
    MaterialProperty albedoColor = null;
    MaterialProperty occlusionMap = null;
    MaterialProperty occlusionMapScale = null;
    MaterialProperty normalMap = null;
    MaterialProperty normalMapScale = null;
    MaterialProperty indirectLightScale = null;
    MaterialProperty ambientScale = null;

    //卡通漫反射区
    MaterialProperty diffuseSteps = null;
    MaterialProperty diffuseSmooth = null;
    MaterialProperty mainLightDiffuseScale = null;
    MaterialProperty diffuseWrap = null;
    MaterialProperty highlightColor = null;
    MaterialProperty shadowColor = null;

    //附加光漫反射开关及其参数
    MaterialProperty useAdditionalLightsDiffuse = null;
    MaterialProperty additionalLightsScale = null;

    //高光区
    MaterialProperty specularMap = null;
    MaterialProperty specularColor = null;
    MaterialProperty specularScale = null;
    MaterialProperty specularSize = null;
    MaterialProperty specularPosterizeSteps = null;
    MaterialProperty specularFaloff = null;
    MaterialProperty additionalSpecularFaloff = null;
    MaterialProperty useSpecular = null;
    MaterialProperty useAdditionalLightsSpecular = null;
    MaterialProperty useEnvironmentReflection = null;
    MaterialProperty envReflectionStrength = null;

    //边缘光区
    MaterialProperty rimColor = null;
    MaterialProperty rimColorMask = null;
    MaterialProperty rimMin = null;
    MaterialProperty rimMax = null;
    MaterialProperty useRimLight = null;

    //描边区
    MaterialProperty useOutline = null;
        MaterialProperty outlineMode = null;
        MaterialProperty outlineColor = null;
        MaterialProperty outlineWidth = null;
        MaterialProperty outlineWidthParams = null;
        MaterialProperty outlineZOffset = null;
        MaterialProperty outlinePosBlend = null;
        MaterialProperty tipTaper = null;
        MaterialProperty adaptiveWidth = null;
        MaterialProperty outlineMaxScale = null;

    #endregion

    #region EditorVariables

    //材质编辑器与当前材质实例，供分组绘制使用。
    MaterialEditor m_MaterialEditor;

    #endregion

    /// <summary>
    /// 按名称绑定 CT.shader 的全部材质属性。
    /// </summary>
    /// <param name="props">材质面板传入的属性数组。</param>
    public void FindProperties(MaterialProperty[] props)
    {
        albedoMap = FindProperty("_Albedo", props);
        albedoColor = FindProperty("_Color", props);
        occlusionMap = FindProperty("_OcclusionMap", props);
        occlusionMapScale = FindProperty("_OcclusionMapScale", props);
        normalMap = FindProperty("_NormalMap", props);
        normalMapScale = FindProperty("_NormalMapScale", props);
        indirectLightScale = FindProperty("_IndirectlightScale", props);
        ambientScale = FindProperty("_AmbientScale", props);

        diffuseSteps = FindProperty("_DiffuseSteps", props);
        diffuseSmooth = FindProperty("_DiffuseSmooth", props);
        mainLightDiffuseScale = FindProperty("_MainLightDiffuseScale", props);
        diffuseWrap = FindProperty("_DiffuseWrap", props);
        highlightColor = FindProperty("_HColor", props);
        shadowColor = FindProperty("_ShadowColor", props);

        useAdditionalLightsDiffuse = FindProperty("_UseAdditionalLightsDiffuse", props);
        additionalLightsScale = FindProperty("_AdditionalLightsScale", props);

        specularMap = FindProperty("_SpecularMap", props);
        specularColor = FindProperty("_SpecularColor", props);
        specularScale = FindProperty("_SpecularScale", props);
        specularSize = FindProperty("_SpecularSize", props);
        specularPosterizeSteps = FindProperty("_SpecularPosterizeSteps", props);
        specularFaloff = FindProperty("_SpecularFaloff", props);
        additionalSpecularFaloff = FindProperty("_AdditionalSpecularFaloff", props);
        useSpecular = FindProperty("_UseSpecular", props);
        useAdditionalLightsSpecular = FindProperty("_UseAdditionalLightsSpecular", props);
        useEnvironmentReflection = FindProperty("_UseEnvironmentReflection", props);
        envReflectionStrength = FindProperty("_EnvReflectionStrength", props);

        rimColor = FindProperty("_RimColor", props);
        rimColorMask = FindProperty("_RimColorMask", props);
        rimMin = FindProperty("_RimMin", props);
        rimMax = FindProperty("_RimMax", props);
        useRimLight = FindProperty("_UseRimLight", props);

        useOutline = FindProperty("_UseOutline", props);
        outlineMode = FindProperty("_OutlineMode", props);
        outlineColor = FindProperty("_OutlineColor", props);
        outlineWidth = FindProperty("_OutlineWidth", props);
        outlineWidthParams = FindProperty("_OutlineWidthParams", props);
        outlineZOffset = FindProperty("_OutlineZOffset", props);
        outlinePosBlend = FindProperty("_OutlinePosBlend", props);
        tipTaper = FindProperty("_TipTaper", props);
        adaptiveWidth = FindProperty("_AdaptiveWidth", props);
        outlineMaxScale = FindProperty("_OutlineMaxScale", props);
    }

    /// <summary>
    /// 材质面板主入口，按分组绘制全部属性。
    /// </summary>
    /// <param name="materialEditor">材质编辑器实例。</param>
    /// <param name="props">材质面板传入的属性数组。</param>
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        FindProperties(props);
        m_MaterialEditor = materialEditor;

        ShaderPropertiesGUI();
    }

    /// <summary>
    /// 按顺序绘制各分组：主纹理、卡通漫反射、高光、边缘光、描边、高级设置。
    /// </summary>
    private void ShaderPropertiesGUI()
    {
        MainEditor();
        DiffuseEditor();
        SpecularEditor();
        RimEditor();
        OutlineEditor();
        Advanced();
    }

    #region HelperFunctions

    /// <summary>
    /// 绘制一个带标题的外框分组，内部依次绘制给定属性。
    /// </summary>
    /// <param name="header">分组标题。</param>
    /// <param name="props">分组内要绘制的属性列表。</param>
    private void DrawBoxSpace(string header, List<MaterialProperty> props)
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label(header, ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        foreach (var prop in props)
        {
            DrawProperty(prop);
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制开关外框分组：标题显示开关，开启时展开参数列表。
    /// </summary>
    /// <param name="header">作为开关的属性。</param>
    /// <param name="props">开启后要绘制的属性列表。</param>
    /// <param name="name">自定义标题；为空时用开关属性展示名。</param>
    private void DrawToggleBoxScope(MaterialProperty header, List<MaterialProperty> props, string name = null)
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawToggleHeader(header, name);

        bool isParamPropEnabled = !Mathf.Approximately(header.floatValue, 0f);
        if (isParamPropEnabled && props.Count > 0)
        {
            EditorGUILayout.BeginVertical(BoxScopeStyle);
            EditorGUILayout.Space(2);

            foreach (var prop in props)
            {
                DrawProperty(prop);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 按属性的展示名绘制单个属性。
    /// </summary>
    /// <param name="prop">要绘制的材质属性。</param>
    private void DrawProperty(MaterialProperty prop)
    {
        m_MaterialEditor.ShaderProperty(prop, prop.displayName);
    }

    /// <summary>
    /// 绘制开关行：左侧粗体标题，右侧开关控件。
    /// </summary>
    /// <param name="prop">开关属性。</param>
    /// <param name="name">自定义标题；为空时用属性展示名。</param>
    private void DrawToggleHeader(MaterialProperty prop, string name = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = prop.displayName.Replace("Use", "");
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(name, ToonLabelStyle);
        m_MaterialEditor.ShaderProperty(prop, string.Empty);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    #endregion

    #region EditorFunctions

    /// <summary>
    /// 绘制主纹理组：主纹理/主色、法线、遮蔽、间接光。
    /// </summary>
    private void MainEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Main", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Albedo"), albedoMap, albedoColor);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), normalMap, normalMapScale);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Occlusion Map"), occlusionMap, occlusionMapScale);
        DrawProperty(indirectLightScale);
        DrawProperty(ambientScale);

        m_MaterialEditor.TextureScaleOffsetProperty(occlusionMap);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制卡通漫反射组：色带参数与附加光漫反射开关。
    /// </summary>
    private void DiffuseEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawBoxSpace("Toon Shading",
            new List<MaterialProperty>
            {
                diffuseSteps, diffuseSmooth, diffuseWrap, mainLightDiffuseScale, highlightColor, shadowColor
            });

        EditorGUILayout.Space();

        DrawToggleBoxScope(useAdditionalLightsDiffuse,
            new List<MaterialProperty>
            {
                additionalLightsScale
            }, "Additional Lights Diffuse");

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制高光组：高光贴图/颜色，以及高光、附加光高光、环境反射开关。
    /// </summary>
    private void SpecularEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Specular Shading", ToonLabelStyle);

        DrawToggleBoxScope(useSpecular,
            new List<MaterialProperty>
            {
                specularScale, specularSize, specularPosterizeSteps, specularFaloff
            }, "Specular Highlights");

        DrawToggleBoxScope(useAdditionalLightsSpecular, new List<MaterialProperty> { additionalSpecularFaloff }, "Additional Lights Specular");

        DrawToggleBoxScope(useEnvironmentReflection,
            new List<MaterialProperty>
            {
                envReflectionStrength
            }, "Environment Reflection");

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Specular Map"), specularMap, specularColor);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制边缘光组：边缘光开关及其参数。
    /// </summary>
    private void RimEditor()
    {
        DrawToggleBoxScope(useRimLight,
            new List<MaterialProperty>
            {
                rimColor, rimColorMask, rimMin, rimMax
            }, "Rim Light");
    }

    /// <summary>
    /// 绘制描边组：描边开关、模式下拉框，按模式显示各自参数。
    /// ViewSpace：宽度参数 + Z偏移；WorldSpace：自适应 + 最大宽度。
    /// </summary>
    private void OutlineEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawToggleHeader(useOutline, "Outline");

        if (!Mathf.Approximately(useOutline.floatValue, 0f))
        {
            EditorGUILayout.BeginVertical(BoxScopeStyle);
            EditorGUILayout.Space(2);

            // 描边颜色（两种模式共用）
            DrawProperty(outlineColor);
            // 基础宽度（两种模式共用）
            DrawProperty(outlineWidth);
            // 模式下拉框
            m_MaterialEditor.ShaderProperty(outlineMode, outlineMode.displayName);

            EditorGUILayout.Space(4);
            bool isViewSpace = Mathf.Approximately(outlineMode.floatValue, 0f);
            if (isViewSpace)
            {
                // 视图空间模式：XY拍平 + 视线偏移
                DrawProperty(outlineWidthParams);
                DrawProperty(outlineZOffset);
                DrawProperty(outlinePosBlend);
                DrawProperty(tipTaper);
            }
            else
            {
                // 世界空间模式：纯顶点外拓 + 距离自适应
                DrawProperty(adaptiveWidth);
                DrawProperty(outlineMaxScale);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制高级设置组：渲染队列、实例化与双面全局光照。
    /// </summary>
    private void Advanced()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Advanced", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.RenderQueueField();
        m_MaterialEditor.EnableInstancingField();
        m_MaterialEditor.DoubleSidedGIField();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    #endregion

}
