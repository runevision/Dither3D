/*
 * Copyright (c) 2025 Rune Skovbo Johansen
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;
using UnityEngine;

public class OverridePropertyAttribute : PropertyAttribute { }

[ExecuteAlways]
public class Dither3DGlobalProperties : MonoBehaviour
{
    static List<Material> ditherMaterials = new List<Material>();

    public enum DitherColorMode { Grayscale, RGB, CMYK }

    [Header("Global Options")]
    public DitherColorMode colorMode;

    public bool inverseDots;
    public bool radialCompensation;
    public bool quantizeLayers;
    public bool debugFractal;

    [Header("Global Overrides")]
    public bool setOnEnable = true;
    public bool saveInMaterials;

    [Space]
    
    [OverrideProperty] public float inputExposure = 1;
    [HideInInspector] public bool inputExposureOverride;
    
    [OverrideProperty] public float inputOffset = 0;
    [HideInInspector] public bool inputOffsetOverride;

    [Space]

    [OverrideProperty] public float dotScale = 5;
    [HideInInspector] public bool dotScaleOverride;
    
    [OverrideProperty] public float dotSizeVariability = 0;
    [HideInInspector] public bool dotSizeVariabilityOverride;
    
    [OverrideProperty] public float dotContrast = 1;
    [HideInInspector] public bool dotContrastOverride;
    
    [OverrideProperty] public float stretchSmoothness = 1;
    [HideInInspector] public bool stretchSmoothnessOverride;

    [Header("Dots Scaling Behavior")]
    public bool scaleWithScreen = true;
    public int referenceRes = 1080;

    void OnEnable()
    {
        CollectMaterials();
        UpdateGlobalOptions();
        if (setOnEnable)
            UpdateGlobalOverrides();
    }

    void OnDisable()
    {
    }

    void OnValidate()
    {
        if (ditherMaterials != null && ditherMaterials.Count > 0 && ditherMaterials[0] == null)
            CollectMaterials();

        UpdateGlobalOptions();
        UpdateGlobalOverrides();
    }

    void OnDidApplyAnimationProperties()
    {
        UpdateGlobalOptions();
        UpdateGlobalOverrides();
    }

    void CollectMaterials()
    {
        ditherMaterials.Clear();
        Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
        foreach (var mat in materials)
        {
            if (mat.HasProperty("_DitherTex"))
            {
                ditherMaterials.Add(mat);
            }
        }
    }

    void UpdateGlobalOptions()
    {
        EnableKeyword("DITHERCOL_GRAYSCALE", colorMode == DitherColorMode.Grayscale);
        EnableKeyword("DITHERCOL_RGB", colorMode == DitherColorMode.RGB);
        EnableKeyword("DITHERCOL_CMYK", colorMode == DitherColorMode.CMYK);
        EnableKeyword("INVERSE_DOTS", inverseDots);
        EnableKeyword("RADIAL_COMPENSATION", radialCompensation);
        EnableKeyword("QUANTIZE_LAYERS", quantizeLayers);
        EnableKeyword("DEBUG_FRACTAL", debugFractal);
    }

    void UpdateGlobalOverrides()
    {
        bool changed = false;
        if (inputExposureOverride)
            SetShaderOverride("_InputExposure", inputExposure, ref changed);
        if (inputOffsetOverride)
            SetShaderOverride("_InputOffset", inputOffset, ref changed);
        if (dotScaleOverride)
        {
            float shaderDotScale = dotScale;
            if (scaleWithScreen)
            {
                float multiplier = Screen.height / referenceRes;
                if (multiplier > 0f)
                {
                    float logDelta = Mathf.Log(multiplier, 2f);
                    shaderDotScale += logDelta;
                }
            }
            SetShaderOverride("_Scale", shaderDotScale, ref changed);
        }
        if (dotSizeVariabilityOverride)
            SetShaderOverride("_SizeVariability", dotSizeVariability, ref changed);
        if (dotContrastOverride)
            SetShaderOverride("_Contrast", dotContrast, ref changed);
        if (stretchSmoothnessOverride)
            SetShaderOverride("_StretchSmoothness", stretchSmoothness, ref changed);

        #if UNITY_EDITOR
        if (changed && saveInMaterials)
        {
            foreach (var mat in ditherMaterials)
            {
                UnityEditor.EditorUtility.SetDirty(mat);
            }
        }
        #endif
    }

    void EnableKeyword(string keyword, bool enable)
    {
        if (enable)
            Shader.EnableKeyword(keyword);
        else
            Shader.DisableKeyword(keyword);
    }

    void SetShaderOverride(string property, float value, ref bool changed)
    {
        foreach (var mat in ditherMaterials)
        {
            if (mat.GetFloat(property) != value)
            {
                mat.SetFloat(property, value);
                changed = true;
            }
        }
    }
}
