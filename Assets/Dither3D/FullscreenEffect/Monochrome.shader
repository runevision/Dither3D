/*
 * Copyright (c) 2025 Rune Skovbo Johansen
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

Shader "Hidden/Monochrome"
{
    Properties
    {
        _MainTex ("Base", 2D) = "" {}
    }

    CGINCLUDE
    
    #pragma multi_compile __ MONOCHROME_1BIT
    
    #include "UnityCG.cginc"
    
    struct v2f
    {
        half4 pos : SV_POSITION;
        half2 uv : TEXCOORD0;
    }; 
            
    sampler2D _MainTex;
    half4 _DarkColor;
    half4 _LightColor;
    
    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv.xy = v.texcoord.xy;
        return o;
    }

    half4 fragMonochrome (v2f i) : SV_Target
    {
        half4 color = saturate(tex2D(_MainTex, i.uv.xy));
        #ifdef MONOCHROME_1BIT
            color.rgb = color.rgb > 0.5 ? 1.0 : 0.0;
        #endif
        color.rgb = lerp(_DarkColor, _LightColor, color.rgb);
        return color;
    }
 
    ENDCG
    
    Subshader
    {
        // pass 0
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert 
            #pragma fragment fragMonochrome
            ENDCG
        }
    }

    Fallback off
}
