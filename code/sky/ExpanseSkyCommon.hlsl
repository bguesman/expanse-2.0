#ifndef EXPANSE_SKY_COMMON_INCLUDED
#define EXPANSE_SKY_COMMON_INCLUDED

/******************************************************************************/
/***************************** GLOBAL VARIABLES *******************************/
/******************************************************************************/

CBUFFER_START(ExpanseSky) // Expanse Sky

/* All of these things have to be in a cbuffer, so we can access them across
 * different shaders. */

/* Planet. */
float _atmosphereRadius;
float _planetRadius;
float4 _groundTint;
float _groundEmissionMultiplier;
float4x4 _planetRotation;
bool _groundAlbedoTextureEnabled;
bool _groundEmissionTextureEnabled;
TEXTURECUBE(_groundAlbedoTexture);
TEXTURECUBE(_groundEmissionTexture);

/* Atmosphere layers. */
#define MAX_LAYERS 8
int _numActiveLayers;
float4 _layerCoefficientsA[MAX_LAYERS]; /* Absorption */
float4 _layerCoefficientsS[MAX_LAYERS]; /* Scattering */
float _layerDensityDistribution[MAX_LAYERS]; /* todo: may get set wrong since we're setting float? */
float _layerHeight[MAX_LAYERS];
float _layerThickness[MAX_LAYERS];
float _layerPhaseFunction[MAX_LAYERS]; /* todo: may get set wrong since we're setting float? */
float _layerAnisotropy[MAX_LAYERS];
float _layerDensity[MAX_LAYERS];
bool _layerUseDensityAttenuation[MAX_LAYERS];
float _layerAttenuationDistance[MAX_LAYERS];
float _layerAttenuationBias[MAX_LAYERS];
float4 _layerTint[MAX_LAYERS];
float _layerMultipleScatteringMultiplier[MAX_LAYERS];

/* Quality. */
int _numTSamples;
int _numLPSamples;
int _numSSSamples;
int _numGISamples;
int _numMSSamples;
int _numMSAccumulationSamples;
bool _useImportanceSampling;

/* Precomputed sky tables. */

/* Transmittance. */
float4 _resT; /* Table resolution. */
TEXTURE2D(_T);

/* Light Pollution. */
float4 _resLP; /* Table resolution. */
TEXTURE2D(_LP0);
TEXTURE2D(_LP1);
TEXTURE2D(_LP2);
TEXTURE2D(_LP3);
TEXTURE2D(_LP4);
TEXTURE2D(_LP5);
TEXTURE2D(_LP6);
TEXTURE2D(_LP7);

/* Single scattering, with and without shadows. */
float4 _resSS; /* Table resolution. */
TEXTURE3D(_SS0);
TEXTURE3D(_SS1);
TEXTURE3D(_SS2);
TEXTURE3D(_SS3);
TEXTURE3D(_SS4);
TEXTURE3D(_SS5);
TEXTURE3D(_SS6);
TEXTURE3D(_SS7);
TEXTURE3D(_SSNoShadow0);
TEXTURE3D(_SSNoShadow1);
TEXTURE3D(_SSNoShadow2);
TEXTURE3D(_SSNoShadow3);
TEXTURE3D(_SSNoShadow4);
TEXTURE3D(_SSNoShadow5);
TEXTURE3D(_SSNoShadow6);
TEXTURE3D(_SSNoShadow7);

/* Multiple scattering. */
float4 _resMS; /* Table resolution. */
TEXTURE2D(_MS);

/* Multiple scattering accumulation. */
float4 _resMSAcc; /* Table resolution. */
TEXTURE3D(_MSAcc0);
TEXTURE3D(_MSAcc1);
TEXTURE3D(_MSAcc2);
TEXTURE3D(_MSAcc3);
TEXTURE3D(_MSAcc4);
TEXTURE3D(_MSAcc5);
TEXTURE3D(_MSAcc6);
TEXTURE3D(_MSAcc7);

/* Ground Irradiance. */
int _resGI; /* Table resolution. */
TEXTURE2D(_GI0);
TEXTURE2D(_GI1);
TEXTURE2D(_GI2);
TEXTURE2D(_GI3);
TEXTURE2D(_GI4);
TEXTURE2D(_GI5);
TEXTURE2D(_GI6);
TEXTURE2D(_GI7);

CBUFFER_END // Expanse Sky

#ifndef GOLDEN_RATIO
#define GOLDEN_RATIO 1.6180339887498948482
#endif

/* Sampler for tables. */
#ifndef UNITY_SHADER_VARIABLES_INCLUDED
    SAMPLER(s_linear_clamp_sampler);
    SAMPLER(s_trilinear_clamp_sampler);
#endif

/******************************************************************************/
/*************************** END GLOBAL VARIABLES *****************************/
/******************************************************************************/



/******************************************************************************/
/***************************** UTILITY FUNCTIONS ******************************/
/******************************************************************************/

#define FLT_EPSILON 0.000001

float clampCosine(float c) {
  return clamp(c, -1.0, 1.0);
}

float safeSqrt(float s) {
  return sqrt(max(0.0, s));
}

/* Returns minimum non-negative number, given that one number is
 * non-negative. If both numbers are negative, returns a negative number. */
float minNonNegative(float a, float b) {
  return (a < 0.0) ? b : ((b < 0.0) ? a : min(a, b));
}

/* True if a is greater than b within tolerance FLT_EPSILON, false
 * otherwise. */
bool floatGT(float a, float b) {
  return a > b - FLT_EPSILON;
}

/* True if a is less than b within tolerance FLT_EPSILON, false
 * otherwise. */
bool floatLT(float a, float b) {
  return a < b + FLT_EPSILON;
}

/******************************************************************************/
/*************************** END UTILITY FUNCTIONS ****************************/
/******************************************************************************/



/******************************************************************************/
/****************************** GEOMETRY TESTS ********************************/
/******************************************************************************/

/* Returns t values of ray intersection with sphere. Third value indicates
 * if there was an intersection at all; if negative, there was no
 * intersection. TODO: maybe optimize. */
float3 intersectSphere(float3 p, float3 d, float r) {
  float A = dot(d, d);
  float B = 2.f * dot(d, p);
  float C = dot(p, p) - (r * r);
  float det = (B * B) - 4.f * A * C;
  if (floatGT(det, 0.0)) {
    det = safeSqrt(det);
    return float3((-B + det) / (2.f * A), (-B - det) / (2.f * A), 1.0);
  }
  return float3(0, 0, -1.0);
}

/* Struct containing data for ray intersection queries. */
struct SkyIntersectionData {
  float startT, endT;
  bool groundHit, atmoHit;
};


/* Traces a ray starting at point O in direction d. Returns information
 * about where the ray hit on the ground/on the boundary of the atmosphere. */
SkyIntersectionData traceSkyVolume(float3 O, float3 d, float planetRadius,
  float atmosphereRadius) {
  /* Perform raw sphere intersections. */
  float3 t_ground = intersectSphere(O, d, planetRadius);
  float3 t_atmo = intersectSphere(O, d, atmosphereRadius);

  SkyIntersectionData toRet = {0, 0, false, false};

  /* We have a hit if the intersection was succesful and if either point
   * is greater than zero (meaning we are in front of the ray, and not
   * behind it). */
  toRet.groundHit = t_ground.z >= 0.0 && (t_ground.x >= 0.0 || t_ground.y >= 0.0);
  toRet.atmoHit = t_atmo.z >= 0.0 && (t_atmo.x >= 0.0 || t_atmo.y >= 0.0);

  if (floatLT(length(O), atmosphereRadius)) {
    /* We are below the atmosphere boundary, and we will start our raymarch
     * at the origin point. */
    toRet.startT = 0;
    if (toRet.groundHit) {
      /* We have hit the ground, and will end our raymarch at the first
       * positive ground hit. */
      toRet.endT = minNonNegative(t_ground.x, t_ground.y);
    } else {
      /* We will end our raymarch at the first positive atmosphere hit. */
      toRet.endT = minNonNegative(t_atmo.x, t_atmo.y);
    }
  } else {
    /* We are outside the atmosphere, and, if we intersect the atmosphere
     * at all, we will start our raymarch at the first atmosphere
     * intersection point. We don't need to be concerned about negative
     * t values, since it's a geometric impossibility to be outside a sphere
     * and intersect both in front of and behind a ray. */
    if (toRet.atmoHit) {
      toRet.startT = min(t_atmo.x, t_atmo.y);
      if (toRet.groundHit) {
        /* If we hit the ground at all, we'll end our ray at the first ground
         * intersection point. */
        toRet.endT = min(t_ground.x, t_ground.y);
      } else {
        /* Otherwise, we'll end our ray at the second atmosphere
         * intersection point. */
        toRet.endT = max(t_atmo.x, t_atmo.y);
      }
    }
    /* If we haven't hit the atmosphere, we leave everything uninitialized,
     * since this ray just goes out into space. */
  }

  return toRet;
}

/******************************************************************************/
/**************************** END GEOMETRY TESTS ******************************/
/******************************************************************************/



/******************************************************************************/
/********************************* SAMPLING ***********************************/
/******************************************************************************/

/* Given an index and total number of points, generates corresponding
 * point on fibonacci hemi-sphere. */
float3 fibonacciHemisphere(int i, int n) {
  float i_mid = i + 0.5;
  float cos_phi = 1 - i/float(n);
  float sin_phi = safeSqrt(1 - cos_phi * cos_phi);
  float theta = 2 * PI * i / GOLDEN_RATIO;
  float cos_theta = cos(theta);
  float sin_theta = safeSqrt(1 - cos_theta * cos_theta);
  return float3(cos_theta * sin_phi, cos_phi, sin_theta * sin_phi);
}

/* Given an index and total number of points, generates corresponding
 * point on fibonacci sphere. */
float3 fibonacciSphere(int i, int n) {
  float i_mid = i + 0.5;
  float cos_phi = 1 - 2 * i/float(n);
  float sin_phi = safeSqrt(1 - cos_phi * cos_phi);
  float theta = 2 * PI * i / GOLDEN_RATIO;
  float cos_theta = cos(theta);
  float sin_theta = safeSqrt(1 - cos_theta * cos_theta);
  return float3(cos_theta * sin_phi, cos_phi, sin_theta * sin_phi);
}

/* Generates linear location from a sample index.
 * Returns (sample, ds). */
float2 generateLinearSampleFromIndex(int i, int numberOfSamples) {
  return float2((float(i) + 0.5) / float(numberOfSamples),
    1.0 / ((float) numberOfSamples));
}

/* Generates cubed "importance sample" location from a sample index.
 * Returns (sample, ds). */
float2 generateCubicSampleFromIndex(int i, int numberOfSamples) {
  float t_left = float(i) / float(numberOfSamples);
  float t_middle = (float(i) + 0.5) / float(numberOfSamples);
  float t_right = (float(i) + 1.0) / float(numberOfSamples);
  t_left *= t_left * t_left;
  t_middle *= t_middle * t_middle;
  t_right *= t_right * t_right;
  return float2(t_middle, t_right - t_left);
}

/******************************************************************************/
/******************************* END SAMPLING *********************************/
/******************************************************************************/



/******************************************************************************/
/************************** DENSITY DISTRIBUTIONS *****************************/
/******************************************************************************/

/* Computes density at a point for exponentially distributed atmosphere.
 * Assumes the planet is centered at the origin. */
float computeDensityExponential(float3 p, float thickness, float density) {
  return density * exp((_planetRadius - length(p))/thickness);
}

/* Computes density at a point for tent distributed atmosphere.
 * Assumes the planet is centered at the origin. */
float computeDensityTent(float3 p, float height, float thickness, float density) {
  return density * max(0.0,
    1.0 - abs(length(p) - _planetRadius - height) / (0.5 * thickness));
}

float computeDensity(int densityDistribution, float3 p, float height,
  float thickness, float density, bool useAtten, float dist, float attenBias, float attenDistance) {
  float atten = 1.0;
  if (useAtten) {
    atten = saturate(exp(-(dist - attenBias)/attenDistance));
  }
  switch (densityDistribution) {
    case 0:
      return atten * computeDensityExponential(p, thickness, density);
    case 1:
      return atten * computeDensityTent(p, height, thickness, density);
    default:
      return 0;
  }
}

float computeOpticalDepth(int densityDistribution, float3 O, float3 endPoint,
  float height, float thickness, float density, float attenBias,
  float attenDistance, bool useAtten, int samples) {
  /* Only use importance sampling if we use exponential distribution. */
  bool importanceSample = _useImportanceSampling && (densityDistribution == 0);
  // Evaluate integral over curved planet with a midpoint integrator.
  float3 d = endPoint - O;
  float length_d = length(d);
  float acc = 0.0;
  for (int i = 0; i < samples; i++) {
    /* Compute where along the ray we're going to sample. */
    float2 t_ds = importanceSample ?
       (generateCubicSampleFromIndex(i, samples)) :
       (generateLinearSampleFromIndex(i, samples));

    /* Compute the point we're going to sample at. */
    float3 pt = O + (d * t_ds.x);

    /* Accumulate the density at that point. */
    acc += computeDensity(densityDistribution, pt, height, thickness,
      density, useAtten, length_d * t_ds.x, attenBias, attenDistance)
      * t_ds.y * length_d;
  }
  return acc;
}

/******************************************************************************/
/************************** DENSITY DISTRIBUTIONS *****************************/
/******************************************************************************/



/******************************************************************************/
/***************************** PHASE FUNCTIONS ********************************/
/******************************************************************************/

float isotropicPhase() {
  return 0.25 / PI;
}

float rayleighPhase(float dot_L_d) {
  return 3.0 / (16.0 * PI) * (1.0 + dot_L_d * dot_L_d);
}

float miePhase(float dot_L_d, float g) {
  return 3.0 / (8.0 * PI) * ((1.0 - g * g) * (1.0 + dot_L_d * dot_L_d))
    / ((2.0 + g * g) * pow(abs(1.0 + g * g - 2.0 * g * dot_L_d), 1.5));
}

float computePhase(float dot_L_d, float anisotropy, int type) {
  switch (type) {
    case 0: /* Isotropic. */
      return isotropicPhase();
    case 1: /* Rayleigh. */
      return rayleighPhase(dot_L_d);
    case 2: /* Mie. */
      return miePhase(dot_L_d, anisotropy);
    default:
      return isotropicPhase();
  }
}

/******************************************************************************/
/*************************** END PHASE FUNCTIONS ******************************/
/******************************************************************************/



/******************************************************************************/
/***************************** TEXTURE SAMPLERS *******************************/
/******************************************************************************/

/* The following implements the strategy used in physically based sky
 * to lerp between 2 4D texture lookups to solve the issue of uv-mapping for
 * a deep texture. */
struct TexCoord4D {
  float x, y, z, w, a;
};

float3 sampleSSTexture(TexCoord4D uv, int i) {
  float3 uvw0 = float3(uv.x, uv.y, uv.z);
  float3 uvw1 = float3(uv.x, uv.y, uv.w);
  switch(i) {
    case 0: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS0, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS0, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 1: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS1, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS1, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 2: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS2, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS2, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 3: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS3, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS3, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 4: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS4, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS4, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 5: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS5, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS5, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 6: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS6, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS6, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 7: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_SS7, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_SS7, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    default:
      return float3(0, 0, 0);
  }
}

float3 sampleMSAccTexture(TexCoord4D uv, int i) {
  float3 uvw0 = float3(uv.x, uv.y, uv.z);
  float3 uvw1 = float3(uv.x, uv.y, uv.w);
  switch(i) {
    case 0: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc0, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc0, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 1: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc1, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc1, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 2: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc2, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc2, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 3: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc3, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc3, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 4: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc4, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc4, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 5: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc5, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc5, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 6: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc6, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc6, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    case 7: {
      float3 contrib0 = SAMPLE_TEXTURE3D_LOD(_MSAcc7, s_linear_clamp_sampler, uvw0, 0).rgb;
      float3 contrib1 = SAMPLE_TEXTURE3D_LOD(_MSAcc7, s_linear_clamp_sampler, uvw1, 0).rgb;
      return lerp(contrib0, contrib1, uv.a);
    }
    default:
      return float3(0, 0, 0);
  }
}

float3 sampleLPTexture(float2 uv, int i) {
  switch(i) {
    case 0:
      return SAMPLE_TEXTURE2D_LOD(_LP0, s_linear_clamp_sampler, uv, 0).rgb;
    case 1:
      return SAMPLE_TEXTURE2D_LOD(_LP1, s_linear_clamp_sampler, uv, 0).rgb;
    case 2:
      return SAMPLE_TEXTURE2D_LOD(_LP2, s_linear_clamp_sampler, uv, 0).rgb;
    case 3:
      return SAMPLE_TEXTURE2D_LOD(_LP3, s_linear_clamp_sampler, uv, 0).rgb;
    case 4:
      return SAMPLE_TEXTURE2D_LOD(_LP4, s_linear_clamp_sampler, uv, 0).rgb;
    case 5:
      return SAMPLE_TEXTURE2D_LOD(_LP5, s_linear_clamp_sampler, uv, 0).rgb;
    case 6:
      return SAMPLE_TEXTURE2D_LOD(_LP6, s_linear_clamp_sampler, uv, 0).rgb;
    case 7:
      return SAMPLE_TEXTURE2D_LOD(_LP7, s_linear_clamp_sampler, uv, 0).rgb;
    default:
      return float3(0, 0, 0);
  }
}

/******************************************************************************/
/*************************** END TEXTURE SAMPLERS *****************************/
/******************************************************************************/

#endif // EXPANSE_SKY_COMMON_INCLUDED
