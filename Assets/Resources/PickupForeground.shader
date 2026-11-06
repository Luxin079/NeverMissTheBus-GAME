Shader "NeverMissTheBus/PickupForeground"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white" {} }
 SubShader {
 Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Cull Off ZWrite Off ZTest Always
 Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 p:POSITION; float2 uv:TEXCOORD0; float4 c:COLOR; };
 struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float4 c:COLOR; };
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 V vert(A a) { V o; o.p=TransformObjectToHClip(a.p.xyz); o.uv=a.uv; o.c=a.c; return o; }
 half4 frag(V i):SV_Target { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.c; }
 ENDHLSL
 }
 }
}
