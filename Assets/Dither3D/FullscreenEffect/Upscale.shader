/*
 * Copyright (c) 2025 Rune Skovbo Johansen
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

Shader "Hidden/Upscale"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;

            v2f vert(appdata_img v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                // Shift by half a pixel so UV is (0, 0) half a texel from the screen corner.
                IN.uv -= _MainTex_TexelSize.xy * 0.5;

                // Get texel coordinates that are integers at the center of each texel.
                float2 uv_pixels = IN.uv * _MainTex_TexelSize.zw;

                // Get vector with coordinates that are 0 at texel corners 
                // and flip from 0.5 to -0.5 at texel centers.
                float2 texel_border_gradient = frac(uv_pixels) - 0.5;

                float2 texel_size = fwidth(uv_pixels);
                float2 texel_border_gradient_1px = saturate(texel_border_gradient / texel_size);
                float2 sample_offset = (texel_border_gradient_1px - texel_border_gradient);

                float2 clampedUV = IN.uv + sample_offset * _MainTex_TexelSize.xy;
                return tex2Dlod(_MainTex, float4(clampedUV, 0, 0));
            }

            ENDCG
        }
    }
}
