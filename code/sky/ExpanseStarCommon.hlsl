#ifndef EXPANSE_STARS_COMMON_INCLUDED
#define EXPANSE_STARS_COMMON_INCLUDED

/******************************************************************************/
/***************************** GLOBAL VARIABLES *******************************/
/******************************************************************************/

CBUFFER_START(ExpanseStar) // Expanse Star

float4 _resStar;
bool _useHighDensityMode;
float _starDensity;
float4 _starDensitySeed;
float _starSizeMin;
float _starSizeMax;
float _starSizeBias;
float4 _starSizeSeed;
float _starIntensityMin;
float _starIntensityMax;
float _starIntensityBias;
float4 _starIntensitySeed;
float _starTemperatureMin;
float _starTemperatureMax;
float _starTemperatureBias;
float4 _starTemperatureSeed;

/* Nebula. */
float4 _resNebulae;
float _nebulaOverallDefinition;
float _nebulaOverallIntensity;
float _nebulaCoverageScale;

// float _nebulaHazeBrightness;
float4 _nebulaHazeColor; // Brightness and color.
float _nebulaHazeScale;
float _nebulaHazeScaleFactor;
float _nebulaHazeDetailBalance;
float _nebulaHazeOctaves;
float _nebulaHazeBias;
float _nebulaHazeSpread;
float _nebulaHazeCoverage;
float _nebulaHazeStrength;

// float _nebulaCloudBrightness;
float4 _nebulaCloudColor; // Brightness and color.
float _nebulaCloudScale;
float _nebulaCloudScaleFactor;
float _nebulaCloudDetailBalance;
float _nebulaCloudOctaves;
float _nebulaCloudBias;
float _nebulaCloudSpread;
float _nebulaCloudCoverage;
float _nebulaCloudStrength;

// float _nebulaCoarseStrandBrightness;
float4 _nebulaCoarseStrandColor; // Brightness and color.
float _nebulaCoarseStrandScale;
float _nebulaCoarseStrandScaleFactor;
float _nebulaCoarseStrandDetailBalance;
float _nebulaCoarseStrandOctaves;
float _nebulaCoarseStrandBias;
float _nebulaCoarseStrandDefinition;
float _nebulaCoarseStrandSpread;
float _nebulaCoarseStrandCoverage;
float _nebulaCoarseStrandStrength;

// float _nebulaFineStrandBrightness;
float4 _nebulaFineStrandColor; // Brightness and color.
float _nebulaFineStrandScale;
float _nebulaFineStrandScaleFactor;
float _nebulaFineStrandDetailBalance;
float _nebulaFineStrandOctaves;
float _nebulaFineStrandBias;
float _nebulaFineStrandDefinition;
float _nebulaFineStrandSpread;
float _nebulaFineStrandCoverage;
float _nebulaFineStrandStrength;

float _nebulaTransmittanceMin;
float _nebulaTransmittanceMax;
float _nebulaTransmittanceScale;

CBUFFER_END // Expanse Star

float3 directionToTex2DArrayCubemapUV(float3 xyz)
{
    // Find which dimension we're pointing at the most
    float3 absxyz = abs(xyz);
    int xMoreY = absxyz.x > absxyz.y;
    int yMoreZ = absxyz.y > absxyz.z;
    int zMoreX = absxyz.z > absxyz.x;
    int xMost = (xMoreY) && (!zMoreX);
    int yMost = (!xMoreY) && (yMoreZ);
    int zMost = (zMoreX) && (!yMoreZ);

    // Determine which index belongs to each +- dimension
    // 0: +X; 1: -X; 2: +Y; 3: -Y; 4: +Z; 5: -Z;
    float xSideIdx = 0 + (xyz.x < 0);
    float ySideIdx = 2 + (xyz.y < 0);
    float zSideIdx = 4 + (xyz.z < 0);

    // Composite it all together to get our side
    float side = xMost * xSideIdx + yMost * ySideIdx + zMost * zSideIdx;

    // Depending on side, we use different components for UV and project to square
    float3 useComponents = float3(0, 0, 0);
    if (xMost) useComponents = xyz.yzx;
    if (yMost) useComponents = xyz.xzy;
    if (zMost) useComponents = xyz.xyz;
    float2 uv = useComponents.xy / useComponents.z;

    // Transform uv from [-1,1] to [0,1]
    uv = uv * 0.5 + float2(0.5, 0.5);

    return float3(uv, side);
}

float3 tex2DArrayCubemapUVToDirection(float3 uvw)
{
    // Use side to decompose primary dimension and negativity
    int side = uvw.z;
    int xMost = side < 2;
    int yMost = side >= 2 && side < 4;
    int zMost = side >= 4;
    int wasNegative = side & 1;

    // Insert a constant plane value for the dominant dimension in here
    uvw.z = 1;

    // Depending on the side we swizzle components back (NOTE: uvw.z is 1)
    float3 useComponents = float3(0, 0, 0);
    if (xMost) useComponents = uvw.zxy;
    if (yMost) useComponents = uvw.xzy;
    if (zMost) useComponents = uvw.xyz;

    // Transform components from [0,1] to [-1,1]
    useComponents = useComponents * 2 - float3(1, 1, 1);
    useComponents *= 1 - 2 * wasNegative;

    return normalize(useComponents);
}

float3 blackbodyTempToColor(float t) {
  t = t / 100;
  float3 result = float3(0, 0, 0);

  /* Red. */
  if (t <= 66) {
    result.r = 255;
  } else {
    result.r = t - 60;
    result.r = 329.698727446f * (pow(result.r, -0.1332047592f));
  }

  /* Green. */
  if (t <= 66) {
    result.g = t;
    result.g = 99.4708025861f * log(t) - 161.1195681661f;
  } else {
    result.g = 288.1221695283f * (pow((t-60), -0.0755148492f));
  }

  /* Blue. */
  if (t >= 66) {
    result.b = 255;
  } else {
    if (t <= 19) {
      result.b = 0;
    } else {
      result.b = 138.5177312231f * log(t-result.b) - 305.0447927307f;
    }
  }

  return clamp(result, 0, 255) / 255;
}

float bias0To1(float val, float bias, float multiplier) {
  bias = saturate(bias);
  float power = (1 + abs(bias - 0.5) * multiplier);
  if (bias < 0.5) {
    return 1 - pow(1-val, power);
  } else {
    return pow(val, power);
  }
}


#endif // EXPANSE_STARS_COMMON_INCLUDED
