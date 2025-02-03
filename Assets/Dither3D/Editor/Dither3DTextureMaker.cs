/*
 * Copyright (c) 2025 Rune Skovbo Johansen
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Dither3DTextureMaker : MonoBehaviour
{
    internal const string kTexturesPath = "Assets/Dither3D";

    [MenuItem("Assets/Create/Dither 3D Texture/Bayer 1x1")]
    static void CreateDither3DTexture1x1()
    {
        CreateDither3DTexture(0);
    }

    [MenuItem("Assets/Create/Dither 3D Texture/Bayer 2x2")]
    static void CreateDither3DTexture2x2()
    {
        CreateDither3DTexture(1);
    }

    [MenuItem("Assets/Create/Dither 3D Texture/Bayer 4x4")]
    static void CreateDither3DTexture4x4()
    {
        CreateDither3DTexture(2);
    }

    [MenuItem("Assets/Create/Dither 3D Texture/Bayer 8x8")]
    static void CreateDither3DTexture8x8()
    {
        CreateDither3DTexture(3);
    }

    static void CreateDither3DTexture(int recursion)
    {
        // Create Bayer points.
        EditorUtility.DisplayProgressBar("Generating Dither Texture", "Bayer Points", 0.0f);
        List<Vector2> bayerPoints = new List<Vector2>();
        bayerPoints.Add(new Vector2(0.00f, 0.00f));
        bayerPoints.Add(new Vector2(0.50f, 0.50f));
        bayerPoints.Add(new Vector2(0.50f, 0.00f));
        bayerPoints.Add(new Vector2(0.00f, 0.50f));

        for (int r = 0; r < recursion - 1; r++)
        {
            int count = bayerPoints.Count;
            float offset = Mathf.Pow(0.5f, r + 1);
            for (int i = 1; i < 4; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    bayerPoints.Add(bayerPoints[j] + bayerPoints[i] * offset);
                }
            }
        }

        // Determine the texture size.
        // If the relationship between layers and size is changed,
        // the shader needs to be changed as well. There's no support in shaders
        // (in Unity) to get the Z resolution of a 3D texture, so it's inferred
        // from the X resolution, based on the logic here.
        int dotsPerSide = Mathf.RoundToInt(Mathf.Pow(2, recursion));
        int layers = dotsPerSide * dotsPerSide;
        int size = 16 * dotsPerSide;

        // Configure the texture.
        Texture3D texture = new Texture3D(size, size, layers, TextureFormat.R8, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        // Create a 3-dimensional array to store color data
        Color[] colors = new Color[size * size * layers];

        // Keep track of how many pixels are above given brightness levels,
        // so we can construct a brightness lookup curve.
        int bucketCount = 256;
        int[] brightnessBuckets = new int[bucketCount];

        // Populate the array so that the x, y, and z values of the texture will
        // map to red, blue, and green colors
        EditorUtility.DisplayProgressBar("Generating Dither Texture", "Brightness Buckets", 0.2f);
        float invRes = 1.0f / size;
        for (int z = 0; z < layers; z++)
        {
            int dotCount = z + 1;
            float dotArea = 0.5f / dotCount;
            float dotRadius = Mathf.Sqrt(dotArea / Mathf.PI);

            int zOffset = z * size * size;
            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) * invRes, (y + 0.5f) * invRes);
                    float dist = Mathf.Infinity;
                    for (int i = 0; i < dotCount; i++)
                    {
                        Vector2 vec = point - bayerPoints[i];
                        vec.x = Mathf.Repeat(vec.x + 0.5f, 1) - 0.5f;
                        vec.y = Mathf.Repeat(vec.y + 0.5f, 1) - 0.5f;
                        float curDist = vec.magnitude;
                        dist = Mathf.Min(dist, curDist);
                    }
                    // Normalize dist.
                    dist = dist / (dotRadius * 2.4f);
                    // Calculate value based on dist.
                    float val = Mathf.Clamp01(1 - dist);

                    colors[x + yOffset + zOffset] = new Color(val, val, val, 1f);

                    int bucket = Mathf.Clamp((int)(val * bucketCount), 0, bucketCount - 1);
                    brightnessBuckets[bucket]++;
                }
            }
        }

        // Calculate brightness ramp.
        EditorUtility.DisplayProgressBar("Generating Dither Texture", "Brightness Ramp", 0.4f);
        float[] brightnessRamp = new float[brightnessBuckets.Length + 1];
        int sum = 0;
        int pixelCount = (size * size * layers);
        for (int i = 0; i < brightnessBuckets.Length; i++)
        {
            sum += brightnessBuckets[brightnessBuckets.Length - 1 - i];
            brightnessRamp[i + 1] = sum / (float)pixelCount;
        }

        // Calculate inverse brightness ramp for looking up threshold values
        // to achieve a given brightness.
        float[] lookupRamp = new float[size];
        float lowerIndexBrightness = 0;
        int higherIndex = 1;
        float higherIndexBrightness = brightnessRamp[1];
        for (int i = 0; i < size; i++)
        {
            float desiredBrightness = i / (float)(size - 1);
            while (higherIndexBrightness < desiredBrightness)
            {
                higherIndex++;
                higherIndexBrightness = brightnessRamp[higherIndex];
            }
            float l = Mathf.InverseLerp(
                lowerIndexBrightness,
                higherIndexBrightness,
                desiredBrightness);
            lookupRamp[i] = (higherIndex - 1 + l) / (brightnessRamp.Length - 1);
        }

        // Create 3D texture.
        EditorUtility.DisplayProgressBar("Generating Dither Texture", "Create Assets", 0.8f);
        texture.SetPixels(colors);
        texture.Apply();
        string name = "Dither3D_" + dotsPerSide + "x" + dotsPerSide;
        AssetDatabase.CreateAsset(texture, $"{kTexturesPath}/{name}/.asset");

        // Create 2D texture for inspection and debugging.
        // (Some versions of Unity can supposedly also create
        // a 3D texture from this via the import settings.)
        Texture2D tex = new Texture2D(size, size * layers, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes($"{kTexturesPath}/{name}/.png", bytes);

        // Create ramp texture
        CreateRampTexture($"Dither3D_{dotsPerSide}x{dotsPerSide}_Ramp", lookupRamp);
        EditorUtility.ClearProgressBar();
    }

    static void CreateRampTexture(string name, float[] lookupRamp)
    {
        Texture2D tex = new Texture2D(lookupRamp.Length, 1, TextureFormat.R8, false);
        Color[] colors = new Color[lookupRamp.Length];
        for (int x = 0; x < lookupRamp.Length; x++)
        {
            colors[x] = new Color(lookupRamp[x], lookupRamp[x], lookupRamp[x], 1.0f);
        }
        tex.SetPixels(colors);

        byte[] bytes = tex.EncodeToPNG();
        string path = $"{kTexturesPath}/{name}.png";
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        TextureImporterPlatformSettings settings = new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            overridden = true,
            format = TextureImporterFormat.R8
        };
        importer.SetPlatformTextureSettings(settings);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.anisoLevel = 0;
        importer.SaveAndReimport();
    }
}
