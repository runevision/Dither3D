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
public class Monochrome : MonoBehaviour
{
    public bool monochrome;
    public Color darkColor = Color.black;
    public Color lightColor = Color.white;

    public Shader monochromeShader;
    Material monochromeMaterial;

    void OnDisable()
    {
        if (monochromeMaterial)
            DestroyImmediate(monochromeMaterial);
        monochromeMaterial = null;
    }

    public void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!monochromeMaterial)
            monochromeMaterial = new Material (monochromeShader);
        
        // Set material properties.
        EnableKeyword("MONOCHROME_1BIT", monochrome);
        monochromeMaterial.SetColor("_DarkColor", darkColor);
        monochromeMaterial.SetColor("_LightColor", lightColor);
        monochromeMaterial.SetPass (0);

        // Apply effect.
        Graphics.Blit(source, destination, monochromeMaterial);
    }

    void EnableKeyword(string keyword, bool enable)
    {
        if (enable)
            Shader.EnableKeyword(keyword);
        else
            Shader.DisableKeyword(keyword);
    }
}
