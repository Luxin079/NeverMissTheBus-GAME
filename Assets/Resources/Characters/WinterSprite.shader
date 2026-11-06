Shader "NeverMissTheBus/WinterSprite"
{
 Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _Purple ("Purple jacket", Float) = 0 [PerRendererData] _WalkBob ("Walk bob", Float) = 0
 _BackGrade ("Grade back view", Float) = 0
 _BackSaturation ("Back saturation", Float) = .65
 _BackBrightness ("Back brightness", Float) = .67
 _BackTint ("Back cool tint", Color) = (.94,.97,1,1) }
 SubShader {
 Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" }
 Cull Off ZWrite On
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
 struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float _Purple;
 float _WalkBob;
 float _BackGrade, _BackSaturation, _BackBrightness;
 float4 _BackTint;
 CBUFFER_END
 float _StoryInteriorDarkness;
 V vert(A a) { V o; float3 world=TransformObjectToWorld(a.positionOS.xyz); world.y+=_WalkBob; o.positionCS=TransformWorldToHClip(world); o.uv=a.uv; return o; }
 half4 frag(V i):SV_Target {
 half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv); clip(c.a-.5);
 // Recolour the olive jacket only; retain gloves, straps, fur and scarf.
 bool scarf=i.uv.x>.81 && i.uv.x<.90 && i.uv.y>.48;
 if(_Purple>.5 && i.uv.x>.675 && i.uv.y>.322 && i.uv.y<.61 && !scarf
    && c.g>c.b*1.10 && c.r>c.b*1.10 && c.r<c.g*1.25) {
 float l=dot(c.rgb,float3(.299,.587,.114)); c.rgb=l*float3(1.18,.65,1.40);
 }
 if (_BackGrade > .5) {
 float grey = dot(c.rgb, float3(.2126,.7152,.0722));
 c.rgb = lerp(grey.xxx, c.rgb, _BackSaturation) * _BackBrightness * _BackTint.rgb;
 }
 c.rgb *= lerp(1.0,.55,saturate(_StoryInteriorDarkness));
 return c;
 }
 ENDHLSL
 }
 }
}
