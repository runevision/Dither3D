/*
 * Copyright (c) 2025 Rune Skovbo Johansen
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class Upscale : MonoBehaviour
{
    public int origVerticalRes = 450;
    public Shader upscaleShader;
    public Camera renderCam;
    
    Camera screenCam;
    RenderTexture rtRender;
    Material upscaleMaterial;
    int origVerticalResLast;

    void OnEnable()
    {
        if (!renderCam || !upscaleShader)
        {
            enabled = false;
            return;
        }
        screenCam = GetComponent<Camera>();
        screenCam.clearFlags = CameraClearFlags.Nothing;
        screenCam.cullingMask = 0;
        upscaleMaterial = new Material (upscaleShader);
        UpdateRenderTextureIfNeeded();
    }
    
    void OnDisable()
    {
        if (upscaleMaterial)
            DestroyImmediate(upscaleMaterial);
        upscaleMaterial = null;
        if (renderCam)
            renderCam.targetTexture = null;
        if (rtRender)
            DestroyImmediate(rtRender);
        rtRender = null;
        origVerticalResLast = 0;
    }

    void OnValidate()
    {
        UpdateRenderTextureIfNeeded();
    }

    void OnDidApplyAnimationProperties()
    {
        UpdateRenderTextureIfNeeded();
    }

    void UpdateRenderTextureIfNeeded()
    {
        if (enabled && screenCam && origVerticalRes != origVerticalResLast)
        {
            renderCam.targetTexture = null;
            if (rtRender)
                DestroyImmediate(rtRender);
            rtRender = new RenderTexture(
                Mathf.RoundToInt(screenCam.pixelWidth * (float)origVerticalRes / screenCam.pixelHeight),
                origVerticalRes,
                32);
            rtRender.hideFlags = HideFlags.DontSave;
            renderCam.targetTexture = rtRender;
            origVerticalResLast = origVerticalRes;
        }
    }

    public void OnRenderImage (RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(rtRender, destination, upscaleMaterial);
    }
}
