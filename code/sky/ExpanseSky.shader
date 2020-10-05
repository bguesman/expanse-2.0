Shader "Hidden/HDRP/Sky/ExpanseSky"
{
  HLSLINCLUDE

  #pragma vertex Vert

  #pragma editor_sync_compilation
  #pragma target 4.5
  #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
  #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"
  #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
  #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/CookieSampling.hlsl"

  float4 _SkyParam; // x exposure, y multiplier, zw rotation (cosPhi and sinPhi)

  #define _Intensity          _SkyParam.x
  #define _CosPhi             _SkyParam.z
  #define _SinPhi             _SkyParam.w
  #define _CosSinPhi          _SkyParam.zw

  TEXTURE2D(_SkyRTFullscreen);
  TEXTURE2D(_SkyRTCubemap);

  /* Sampler for tables. */
  #ifndef UNITY_SHADER_VARIABLES_INCLUDED
      SAMPLER(s_linear_clamp_sampler);
      SAMPLER(s_trilinear_clamp_sampler);
  #endif

  struct Attributes
  {
      uint vertexID : SV_VertexID;
      UNITY_VERTEX_INPUT_INSTANCE_ID
  };

  struct Varyings
  {
      float4 positionCS : SV_POSITION;
      UNITY_VERTEX_OUTPUT_STEREO
  };

  Varyings Vert(Attributes input)
  {
      Varyings output;
      UNITY_SETUP_INSTANCE_ID(input);
      UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
      output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
      return output;
  }

  float3 RotationUp(float3 p, float2 cos_sin)
  {
      float3 rotDirX = float3(cos_sin.x, 0, -cos_sin.y);
      float3 rotDirY = float3(cos_sin.y, 0,  cos_sin.x);

      return float3(dot(rotDirX, p), p.y, dot(rotDirY, p));
  }

  float4 GetColorWithRotation(float3 dir, float2 cos_sin)
  {
      return float4(0.0, 0.0, 0.5, 1.0);
  }

  float4 RenderSky(Varyings input)
  {
    float3 viewDirWS = GetSkyViewDirWS(input.positionCS.xy);

    // Reverse it to point into the scene
    float3 dir = -viewDirWS;

    return GetColorWithRotation(dir, _CosSinPhi);
  }

  float4 Composite(Varyings input, bool cubemap, float exposure) {
    float4 skyColor = float4(0.5, 0.0, 0.0, 1.0);
    // float4 screenSpace = computeScreenPos(input.positionCS);
    // float2 uv = input.xy;
    /* TODO: branch will be slow? */
    float2 textureCoordinate = float2(0.21,0.21);//input.positionCS.xy / input.positionCS.w;
    // if (cubemap) {
      // float4 skyColor = _SkyRTCubemap[computeScreenPos(input.xy)];
    // } else {
      skyColor += SAMPLE_TEXTURE2D_LOD(_SkyRTFullscreen, s_linear_clamp_sampler, textureCoordinate, 0);
    // }
    return skyColor;
  }

  float4 SkyCubemap(Varyings input) : SV_Target {
    return RenderSky(input);
  }

  float4 SkyFullscreen(Varyings input) : SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return RenderSky(input);
  }

  float4 CompositeCubemap(Varyings input) : SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return Composite(input, true, 1.0);
    // return RenderSky(input);
  }

  float4 CompositeFullscreen(Varyings input) : SV_Target {
    return Composite(input, false, GetCurrentExposureMultiplier());
    // return RenderSky(input);
  }

  ENDHLSL

  SubShader
  {
    /* Sky cubemap */
    Pass
    {
      ZWrite Off
      ZTest Always
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment SkyCubemap
      ENDHLSL
    }

    /* Sky fullscreen */
    Pass
    {
      ZWrite Off
      ZTest LEqual
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment SkyFullscreen
      ENDHLSL
    }

    /* Cubemap compositing. */
    Pass
    {
      ZWrite Off
      ZTest Always
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment CompositeCubemap
      ENDHLSL
    }

    /* Fullscreen compositing. */
    Pass
    {
      ZWrite Off
      ZTest LEqual
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment CompositeFullscreen
      ENDHLSL
    }
  }
  Fallback Off
}
