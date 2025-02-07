/*
 * Copyright (c) 2025 Rune Skovbo Johansen
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

sampler3D _DitherTex;
sampler2D _DitherRampTex;
float4 _DitherTex_TexelSize;
float _Scale;
float _SizeVariability;
float _Contrast;
float _StretchSmoothness;
float _InputExposure;
float _InputOffset;

// dx is the delta in u and v coordinates along the screen X axis.
// dy is the delta in u and v coordinates along the screen Y axis.
fixed4 GetDither3D_(float2 uv_DitherTex, float4 screenPos, float2 dx, float2 dy, fixed brightness)
{
    #if (INVERSE_DOTS)
        brightness = 1.0 - brightness;
    #endif

    // Get texture X resolution (width) based on Unity builtin data.
    // We assume the Y resolution is the same.
    float xRes = _DitherTex_TexelSize.z;
    float invXres = _DitherTex_TexelSize.x;

    // The relationship between X resolution, dots per side, and total number of
    // dots - which is also the Z resolution - is hardcoded in the script that
    // creates the 3D texture. Unity has no way to query the Z resolution
    // of a 3D texture in a shader.
    float dotsPerSide = xRes / 16.0;
    float dotsTotal = pow(dotsPerSide, 2); // Could also have been named zRes
    float invZres = 1.0 / dotsTotal;

    // Lookup brightness to make dither output have correct output
    // brightness at different input brightness values.
    float2 lookup = float2((0.5 * invXres + (1 - invXres) * brightness), 0.5);
    fixed brightnessCurve = tex2D(_DitherRampTex, lookup).r;

    #if (RADIAL_COMPENSATION)
        // Make screenPos have 0,0 in the center of the screen.
        float2 screenP = (screenPos.xy / screenPos.w - 0.5) * 2.0;
        // Calculate view direction projected onto camera plane.
        float2 viewDirProj = float2(
            screenP.x /  UNITY_MATRIX_P[0][0],
            screenP.y / -UNITY_MATRIX_P[1][1]);
        // Calculate how much dots should be larger towards the edges of the screen.
        // This is meant to keep dots completely stable under camera rotation.
        // Currently it doesn't entirely work but is more stable than no compensation.
        float radialCompensation = dot(viewDirProj, viewDirProj) + 1;
        dx *= radialCompensation;
        dy *= radialCompensation;
    #endif

    // Get frequency based on singular value decomposition.
    // A simpler approach would have been to use fwidth(uv_DitherTex).
    // However:
    //  1) fwidth is not accurate and produces axis-aligned biases/artefacts.
    //  2) We need both the minimum and maximum rate of change.
    //     These can be along any directions (orthogonal to each other),
    //     not necessarily aligned with x, y, u or v.
    //     So we use (a subset of) singular value decomposition to get these.
    float2x2 matr = { dx, dy };
    float4 vectorized = float4(dx, dy);
    float Q = dot(vectorized, vectorized);
    float R = determinant(matr); //ad-bc
    float discriminantSqr = max(0, Q*Q-4*R*R);
    float discriminant = sqrt(discriminantSqr);

    // "freq" here means rate of change of the UV coordinates on the screen.
    // Something smaller on the screen has a larger rate of change of its
    // UV coordinates from one pixel to the next.
    //
    // The freq variable: (max-freq, min-freq)
    //
    // If a surface has non-uniform scaling, or is seen at an angle,
    // or has UVs that are stretched more in one direction than the other,
    // the min and max frequency won't be the same.
    float2 freq = sqrt(float2(Q + discriminant, Q - discriminant) / 2);

    // We define a spacing variable which linearly correlates with
    // the average distance between dots.
    // For this dot spacing, we use the smaller frequency, which
    // corresponds to the largest amount of stretching.
    // This for example means that dots seen at an angle will be
    // compressed in one direction rather than enlarged in the other.
    float spacing = freq.y;

    // Scale the spacing by the specified input (power of two) scale.
    float scaleExp = exp2(_Scale);
    spacing *= scaleExp;

    // We keep the spacing the same regardless of whether we're using
    // a pattern with more or less dots in it.
    spacing *= dotsPerSide * 0.125;

    // We produce higher brightness by having the dots be larger
    // compared to the pattern size (based on a contrast threshold
    // further down), and lower brightness by having them be smaller.
    //
    // If we don't want variable dot sizes, we can keep the dot sizes
    // approximately constant regardless of brightness by dividing
    // the spacing by the brightness. This makes both the dots and
    // the spacing between them larger, the lower the brightness is.
    // In this case, the two adjustments of dot size cancel out each
    // other, leaving only the effect on the spacing between the dots.
    //
    // Any behavior in between these two is also possible, controlled by
    // the _SizeVariability input.
    //
    // A*pow(B,-1) is the same as A/B, so when _SizeVariability is 0,
    // we divide the spacing by the brightness.
    //
    // A*pow(B,0) is the same as A, so when _SizeVariability is 1,
    // we leave the spacing alone.
    //
    // The "* 2" is there so the mid-size dots keeps constant throughout
    // the spectrum, rather than the largest-sized dots.
    // The "+ 0.001" is there to avoid dividing by zero.
    float brightnessSpacingMultiplier =
        pow(brightnessCurve * 2 + 0.001, -(1 - _SizeVariability));
    spacing *= brightnessSpacingMultiplier;

    // Find the power-of-two level that corresponds to the dot spacing.
    float spacingLog = log2(spacing);
    int patternScaleLevel = floor(spacingLog); // Fractal level.
    float f = spacingLog - patternScaleLevel; // Fractional part.

    // Get the UV coordinates in the current fractal level.
    float2 uv = uv_DitherTex / exp2(patternScaleLevel);

    // Get the third coordinate for the 3D texture lookup.
    // Each layer along the 3rd dimension in the 3D texture has one more dot.
    // The first layer we use is the one that has 1/4 of the dots.
    // The last layer we use is the one with all the dots.
    float subLayer = lerp(0.25 * dotsTotal, dotsTotal, 1 - f);

    // If we don't want to interpolate between different layers, we can
    // restrict the sampled values so they correspond exactly to one layer.
    #if (QUANTIZE_LAYERS)
        float origSubLayer = subLayer;
        subLayer = floor(subLayer + 0.5);

        // When we quantize the layers, we can't rely on pattern interpolation
        // to keep the dot size constant within each sub-layer, so we have to
        // tweak the threshold values to compensate instead.
        float thresholdTweak = sqrt(subLayer / origSubLayer);
    #endif

    // Texels are half a texel offset from the texture border, so we
    // need to subtract half a texel. We also normalize to the 0-1 range.
    subLayer = (subLayer - 0.5) * invZres;

    // Sample the 3D texture.
    fixed pattern = tex3D(_DitherTex, float3(uv, subLayer)).r;

    // The dots in the pattern are radial gradients.
    // We create sharp dots from them by increasing the contrast.
    // The desired amount of contrast can be set in the material,
    // for example such that there is 1 pixel of blurring around dots,
    // which looks equivalent to anti-aliasing.
    float contrast = _Contrast * scaleExp * brightnessSpacingMultiplier * 0.1;

    // The spacing is derived from the lowest frequency, but the
    // contrast must be based on the highest frequency to avoid aliasing.
    // Hence we multiply the contrast by the factor of the smallest
    // frequency (freq.y) relative to the highest frequency (freq.x).
    // This compensation can be increased or decreased by using exponents
    // other than 1, as provided by the _StretchSmoothness input.
    contrast *= pow(freq.y / freq.x, _StretchSmoothness);

    // The base brightness value that we scale the contrast around
    // should normally be 0.5, but if the pattern is very blurred,
    // that would just make the brightness everywhere close to 0.5.
    // To avoid this, we lerp towards a base value of the original
    // brightness the lower the contrast is.
    // The specific formula is arrived at experimentally to maintain
    // brightness levels across various contrast and scale values.
    fixed baseVal = lerp(0.5, brightness, saturate(1.05 / (1 + contrast)));

    // The brighter output we want, the lower threshold we need to use,
    // which makes the resulting dots larger relative to the pattern.
    #if (QUANTIZE_LAYERS)
        fixed threshold = 1 - brightnessCurve * thresholdTweak;
    #else
        fixed threshold = 1 - brightnessCurve;
    #endif

    // Get the pattern value relative to the threshold, scale it
    // according to the contrast, and add the base value.
    fixed bw = saturate((pattern - threshold) * contrast + baseVal);

    #if (INVERSE_DOTS)
        bw = 1.0 - bw;
    #endif

    return fixed4(bw, frac(uv.x), frac(uv.y), subLayer);
}

fixed GetDither3D(float2 uv_DitherTex, float4 screenPos, fixed brightness)
{
    // Get the rates of change of the UV coordinates.
    float2 dx = ddx(uv_DitherTex);
    float2 dy = ddy(uv_DitherTex);
    return GetDither3D_(uv_DitherTex, screenPos, dx, dy, brightness);
}

fixed GetDither3DAltUV(float2 uv_DitherTex, float2 uv_DitherTexAlt, float4 screenPos, fixed brightness)
{
    // Get the rates of change of two sets of UV coordinates and use the smaller ones.
    // This can remove seams caused by discontinuities in the UV coordinates,
    // as long as the alternative coordinates don't have seams in the same place.
    float2 dxA = ddx(uv_DitherTex);
    float2 dyA = ddy(uv_DitherTex);
    float2 dxB = ddx(uv_DitherTexAlt);
    float2 dyB = ddy(uv_DitherTexAlt);
    float2 dx = dot(dxA, dxA) < dot(dxB, dxB) ? dxA : dxB;
    float2 dy = dot(dyA, dyA) < dot(dyB, dyB) ? dyA : dyB;
    return GetDither3D_(uv_DitherTex, screenPos, dx, dy, brightness);
}

// COLOR HANDLING

fixed GetGrayscale(fixed4 color)
{
    return saturate(0.299 * color.r + 0.587 * color.g + 0.114 * color.b);
}

fixed3 CMYKtoRGB(fixed4 cmyk) {
    fixed c = cmyk.x;
    fixed m = cmyk.y;
    fixed y = cmyk.z;
    fixed k = cmyk.w;

    fixed invK = 1.0 - k;
    fixed r = 1.0 - min(1.0, c * invK + k);
    fixed g = 1.0 - min(1.0, m * invK + k);
    fixed b = 1.0 - min(1.0, y * invK + k);
    return saturate(fixed3(r, g, b));
}

fixed4 RGBtoCMYK(fixed3 rgb) {
    fixed r = rgb.r;
    fixed g = rgb.g;
    fixed b = rgb.b;
    fixed k = min(1.0 - r, min(1.0 - g, 1.0 - b));
    fixed3 cmy = 0.0;
    fixed invK = 1.0 - k;
    if (invK != 0.0) {
        cmy.x = (1.0 - r - k) / invK;
        cmy.y = (1.0 - g - k) / invK;
        cmy.z = (1.0 - b - k) / invK;
    }
    return saturate(fixed4(cmy, k));
}

float2 RotateUV(float2 uv, float2 xUnitDir)
{
    return uv.x * xUnitDir + uv.y * float2(-xUnitDir.y, xUnitDir.x);
}

fixed4 GetDither3DColor_(float2 uv_DitherTex, float4 screenPos, float2 dx, float2 dy, fixed4 color)
{
    // Adjust brightness according to shader exposure and offset properties.
    color.rgb = saturate(color.rgb * _InputExposure + _InputOffset);
    
    #ifdef DITHERCOL_GRAYSCALE
        fixed4 dither = GetDither3D_(uv_DitherTex, screenPos, dx, dy, GetGrayscale(color));
        color.rgb = dither.x;
        #if (DEBUG_FRACTAL)
            fixed3 uvVis = dither.yzw;
            color.rgb = lerp(color.rgb, uvVis, 0.7);
        #endif
    #elif DITHERCOL_RGB
        color.r = GetDither3D_(uv_DitherTex, screenPos, dx, dy, color.r).x;
        color.g = GetDither3D_(uv_DitherTex, screenPos, dx, dy, color.g).x;
        color.b = GetDither3D_(uv_DitherTex, screenPos, dx, dy, color.b).x;
    #elif DITHERCOL_CMYK
        fixed4 cmyk = RGBtoCMYK(color.rgb);
        // Get dither pattern for C, M, Y, K and angles 15, 75, 0, 45.
        cmyk.x = GetDither3D_(RotateUV(uv_DitherTex, float2(0.966, 0.259)), screenPos, dx, dy, cmyk.x).x;
        cmyk.y = GetDither3D_(RotateUV(uv_DitherTex, float2(0.259, 0.966)), screenPos, dx, dy, cmyk.y).x;
        cmyk.z = GetDither3D_(RotateUV(uv_DitherTex, float2(1.000, 0.000)), screenPos, dx, dy, cmyk.z).x;
        cmyk.w = GetDither3D_(RotateUV(uv_DitherTex, float2(0.707, 0.707)), screenPos, dx, dy, cmyk.w).x;
        color.rgb = CMYKtoRGB(cmyk);
    #endif

    return color;
}

fixed4 GetDither3DColor(float2 uv_DitherTex, float4 screenPos, fixed4 color)
{
    // Get the rates of change of the UV coordinates.
    float2 dx = ddx(uv_DitherTex);
    float2 dy = ddy(uv_DitherTex);
    return GetDither3DColor_(uv_DitherTex, screenPos, dx, dy, color);
}

fixed4 GetDither3DColorAltUV(float2 uv_DitherTex, float2 uv_DitherTexAlt, float4 screenPos, fixed4 color)
{
    // Get the rates of change of two sets of UV coordinates and use the smaller ones.
    // This can remove seams caused by discontinuities in the UV coordinates,
    // as long as the alternative coordinates don't have seams in the same place.
    float2 dxA = ddx(uv_DitherTex);
    float2 dyA = ddy(uv_DitherTex);
    float2 dxB = ddx(uv_DitherTexAlt);
    float2 dyB = ddy(uv_DitherTexAlt);
    float2 dx = dot(dxA, dxA) < dot(dxB, dxB) ? dxA : dxB;
    float2 dy = dot(dyA, dyA) < dot(dyB, dyB) ? dyA : dyB;
    return GetDither3DColor_(uv_DitherTex, screenPos, dx, dy, color);
}
