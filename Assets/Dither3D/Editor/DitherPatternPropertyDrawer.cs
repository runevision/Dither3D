using UnityEngine;
using UnityEditor;

// ReSharper disable once UnusedType.Global - make Rider not complain that class is unused
public class DitherPatternPropertyDrawer : MaterialPropertyDrawer
{
    static readonly GUIContent[] s_PopupNames = {new GUIContent("1x1"), new GUIContent("2x2"), new GUIContent("4x4"), new GUIContent("8x8")};
    static readonly int[] s_PopupValues = {0, 1, 2, 3};
    public override void OnGUI (Rect position, MaterialProperty prop, string label, MaterialEditor editor)
    {
        int value = (int)prop.floatValue;
        EditorGUI.BeginChangeCheck();
        EditorGUI.showMixedValue = prop.hasMixedValue;
        value = EditorGUI.IntPopup(position, new GUIContent(label), value, s_PopupNames, s_PopupValues);
        EditorGUI.showMixedValue = false;
        if (EditorGUI.EndChangeCheck())
        {
            prop.floatValue = value;
            Texture ditherTex = null;
            Texture ditherRampTex = null;
            switch (value)
            {
                case 0:
                    ditherTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_1x1.asset");
                    ditherRampTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_1x1_Ramp.png");
                    break;
                case 1:
                    ditherTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_2x2.asset");
                    ditherRampTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_2x2_Ramp.png");
                    break;
                case 2:
                    ditherTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_4x4.asset");
                    ditherRampTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_4x4_Ramp.png");
                    break;
                case 3:
                    ditherTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_8x8.asset");
                    ditherRampTex = AssetDatabase.LoadAssetAtPath<Texture>($"{Dither3DTextureMaker.kTexturesPath}/Dither3D_8x8_Ramp.png");
                    break;
            }

            if (ditherTex != null)
            {
                foreach (Material mat in editor.targets)
                    mat.SetTexture("_DitherTex", ditherTex);
            }
            if (ditherRampTex != null)
            {
                foreach (Material mat in editor.targets)
                    mat.SetTexture("_DitherRampTex", ditherRampTex);
            }
        }
    }
}
