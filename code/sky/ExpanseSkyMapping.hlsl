#ifndef EXPANSE_SKY_MAPPING_INCLUDED
#define EXPANSE_SKY_MAPPING_INCLUDED

#include "../common/shaders/ExpanseSkyCommon.hlsl"

float fromUnitToSubUVs(float u, float resolution) {
  float j = 0.5;
  return (j + u * (resolution + 1 - 2 * j)) / (resolution + 1);
}

float fromSubUVsToUnit(float u, float resolution) {
  float j = 0.5;
  return (u * (resolution + 1) - j) / (resolution + 1 - 2 * j);
}

float map_r_naive(float r, float atmosphereRadius, float planetRadius) {
  return (r - planetRadius) / (atmosphereRadius - planetRadius);
}

float unmap_r_naive(float u_r, float atmosphereRadius, float planetRadius) {
  return planetRadius + u_r * (atmosphereRadius - planetRadius);
}

float map_r(float r, float atmosphereRadius, float planetRadius) {
  return safeSqrt((r - planetRadius) / (atmosphereRadius - planetRadius));
}

/* Unmaps r, the distance from the planet origin, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer. */
float unmap_r(float u_r, float atmosphereRadius, float planetRadius) {
  return planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));
}

float2 map_r_mu_transmittance(float r, float mu, float atmosphereRadius, float planetRadius,
  float d, bool groundHit, float resMu) {
  float u_mu = 0.0;
  float muStep = 1/resMu;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (groundHit) {
    mu = clamp(mu, -1, cos_h);
    u_mu = 0.5 * pow((cos_h - mu) / (1 + cos_h), 0.2);
    // KEEP FOR GROUND.
    if (floatGT(u_mu, 0.5-muStep/2)) {
      u_mu = 0.5 - muStep/2;
    }
    // if (floatLT(u_mu, muStep/2)) {
    //   u_mu = muStep/2;
    // }
  } else {
    mu = clamp(mu, cos_h, 1);
    u_mu = 0.5 * pow((mu - cos_h) / (1 - cos_h), 0.2) + 0.5;
    // KEEP FOR SKY.
    if (floatLT(u_mu, 0.5 + muStep/2)) {
      u_mu = 0.5 + muStep/2;
    }
    // if (floatGT(u_mu, 1 - muStep/2)) {
    //   u_mu = 1 - muStep/2;
    // }
  }

  float u_r = safeSqrt((r - planetRadius) / (atmosphereRadius - planetRadius));

  return float2(u_r, fromUnitToSubUVs(u_mu, resMu));
}

float2 unmap_r_mu_transmittance(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius, float resMu) {
  u_mu = fromSubUVsToUnit(u_mu, resMu);
  float r = planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));

  float mu = 0.0;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (floatLT(u_mu, 0.5)) {
    mu = clampCosine(cos_h - pow(u_mu * 2, 5) * (1 + cos_h));
  } else {
    mu = clampCosine(pow(2 * (u_mu - 0.5), 5) * (1 - cos_h) + cos_h);
  }

  return float2(r, mu);
}

/* Maps mu, the cosine of the viewing angle, into range 0-1. Uses
 * mapping from bruneton and neyer. */
float map_mu(float r, float mu, float atmosphereRadius, float planetRadius,
  float d, bool groundHit, float resMu) {

  float u_mu = 0.0;
  float muStep = 1/resMu;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (groundHit) {
    mu = min(mu, cos_h);
    u_mu = 0.5 * pow((cos_h - mu) / (1 + cos_h), 0.5);
    // KEEP FOR GROUND.
    if (floatGT(u_mu, 0.5-muStep/2)) {
      u_mu = 0.5 - muStep/2;
    }
    // if (floatLT(u_mu, muStep/2)) {
    //   u_mu = muStep/2;
    // }
  } else {
    mu = max(mu, cos_h);
    u_mu = 0.5 * pow((mu - cos_h) / (1 - cos_h), 0.5) + 0.5;
    // KEEP FOR SKY.
    if (floatLT(u_mu, 0.5 + muStep/2)) {
      u_mu = 0.5 + muStep/2;
    }
    // if (floatGT(u_mu, 1 - muStep/2)) {
    //   u_mu = 1 - muStep/2;
    // }
  }

  return fromUnitToSubUVs(u_mu, resMu);
}

/* Unmaps mu, the cosine of the viewing angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer.  */
float unmap_mu(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius, float resMu) {
  u_mu = fromSubUVsToUnit(u_mu, resMu);

  float r = planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));

  float mu = 0.0;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (floatLT(u_mu, 0.5)) {
    mu = clampCosine(cos_h - pow(u_mu * 2, 2) * (1 + cos_h));
  } else {
    mu = clampCosine(pow(2 * (u_mu - 0.5), 2) * (1 - cos_h) + cos_h);
  }

  return mu;
}

/* Unmaps mu, the cosine of the viewing angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer.  */
float unmap_mu_with_r(float r, float u_mu, float atmosphereRadius,
  float planetRadius, float resMu) {
  u_mu = fromSubUVsToUnit(u_mu, resMu);

  float mu = 0.0;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (floatLT(u_mu, 0.5)) {
    mu = clampCosine(cos_h - pow(u_mu * 2, 2) * (1 + cos_h));
  } else {
    mu = clampCosine(pow(2 * (u_mu - 0.5), 2) * (1 - cos_h) + cos_h);
  }

  return mu;
}

/* Maps r and mu together---slightly more efficient than mapping them
 * individually, since they share calculations. */
float2 map_r_mu(float r, float mu, float atmosphereRadius, float planetRadius,
  float d, bool groundHit, float resMu) {

  float u_mu = 0.0;
  float muStep = 1/resMu;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (groundHit) {
    mu = clamp(mu, -1, cos_h);
    u_mu = 0.5 * pow((cos_h - mu) / (1 + cos_h), 0.5);
    // KEEP FOR GROUND.
    if (floatGT(u_mu, 0.5-muStep/2)) {
      u_mu = 0.5 - muStep/2;
    }
    // if (floatLT(u_mu, muStep/2)) {
    //   u_mu = muStep/2;
    // }
  } else {
    mu = clamp(mu, cos_h, 1);
    u_mu = 0.5 * pow((mu - cos_h) / (1 - cos_h), 0.5) + 0.5;
    // KEEP FOR SKY.
    if (floatLT(u_mu, 0.5 + muStep/2)) {
      u_mu = 0.5 + muStep/2;
    }
    // if (floatGT(u_mu, 1 - muStep/2)) {
    //   u_mu = 1 - muStep/2;
    // }
  }

  float u_r = safeSqrt((r - planetRadius) / (atmosphereRadius - planetRadius));

  return float2(u_r, fromUnitToSubUVs(u_mu, resMu));
}

/* Unmaps mu and r together---slightly more efficient than unmapping them
 * individually, since they share calculations.  */
float2 unmap_r_mu(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius, float resMu) {
  u_mu = fromSubUVsToUnit(u_mu, resMu);
  float r = planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));

  float mu = 0.0;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (floatLT(u_mu, 0.5)) {
    mu = clampCosine(cos_h - pow(u_mu * 2, 2) * (1 + cos_h));
  } else {
    mu = clampCosine(pow(2 * (u_mu - 0.5), 2) * (1 - cos_h) + cos_h);
  }

  return float2(r, mu);
}

float map_mu_naive(float mu, float resMu) {
  return fromUnitToSubUVs((mu + 1) / 2, resMu);
}

float unmap_mu_naive(float u_mu, float resMu) {
  return (fromSubUVsToUnit(u_mu, resMu) * 2) - 1;
}

/* Maps mu_l, the cosine of the sun zenith angle, into range 0-1. Uses
 * mapping from bruneton and neyer. */
float map_mu_l(float mu_l) {
  return saturate((1.0 - exp(-3 * mu_l - 0.6)) / (1 - exp(-3.6)));
}

/* Unmaps mu_l, the cosine of the sun zenith angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer. */
float unmap_mu_l(float u_mu_l) {
  return clampCosine((log(1.0 - (u_mu_l * (1 - exp(-3.6)))) + 0.6) / -3.0);
}

/* Maps nu, the cosine of the sun azimuth angle, into range 0-1. Uses
 * mapping from bruneton and neyer. */
float map_nu(float nu) {
  float gamma = acos(nu) / PI;
  if (gamma >= 0.5) {
    return 0.5 + 0.5 * pow(2 * (gamma - 0.5), 1.5);
  } else {
    return 0.5 - 0.5 * pow(2 * (0.5 - gamma), 1.5);
  }
}

/* Unmaps nu, the cosine of the sun azimuth angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer. */
float unmap_nu(float u_nu) {
  if (u_nu > 0.5) {
    float gamma = (pow(((u_nu - 0.5) / 0.5), 2/3) / 2) + 0.5;
    return cos(gamma * PI);
  } else {
    float gamma = 0.5 - (pow(((0.5 - u_nu) / 0.5), 2/3) / 2);
    return cos(gamma * PI);
  }
}

float map_theta(float theta, float resTheta) {
  return fromUnitToSubUVs(theta / (2 * PI), resTheta);
}

float unmap_theta(float u_theta, float resTheta) {
  return fromSubUVsToUnit(u_theta, resTheta) * 2 * PI;
}




























/* Returns u_mu_l, v. V is always zero, since the texture is effectively 1D. */
float2 mapSky1DCoord(float mu_l) {
  return float2(map_mu_l(mu_l), 0);
}

/* Returns r, mu_l. R is always just above the ground. */
float2 unmapSky1DCoord(float u_mu_l, float _planetRadius) {
  return float2(_planetRadius + 0.001, unmap_mu_l(u_mu_l));
}

/* Returns u_r, u_mu. */
float2 mapSky2DCoord(float r, float mu, float atmosphereRadius,
  float planetRadius, float d, bool groundHit, float resMu) {
  return map_r_mu_transmittance(r, mu, atmosphereRadius, planetRadius, d, groundHit, resMu);
  // return float2(map_r_naive(r, atmosphereRadius, planetRadius), map_mu_naive(mu, resMu));
}

/* Returns r, mu. */
float2 unmapSky2DCoord(float u_r, float u_mu,
  float atmosphereRadius, float planetRadius, float resMu) {
  return unmap_r_mu_transmittance(u_r, u_mu, atmosphereRadius, planetRadius, resMu);
  // return float2(unmap_r_naive(u_r, atmosphereRadius, planetRadius), unmap_mu_naive(u_mu, resMu));
}

/* This parameterization was taken from Hillaire's 2020 model. */
/* Returns u_r, u_mu_l. */
float2 mapMSCoordinate(float r, float mu_l,
  float atmosphereRadius, float planetRadius) {
  return float2(map_r(r, atmosphereRadius, planetRadius), map_mu_l(mu_l));
}

/* Returns r, mu_l. */
float2 unmapMSCoordinate(float u_r, float u_mu_l, float atmosphereRadius,
  float planetRadius) {
  return float2(unmap_r(u_r, atmosphereRadius, planetRadius),
    unmap_mu_l(u_mu_l));
}

/* Returns u_mu, u_theta. */
float2 mapSkyRenderCoordinate(float r, float mu, float theta, float atmosphereRadius,
  float planetRadius, float d, bool groundHit, float resMu, float resTheta) {
  return float2(map_mu(r, mu, atmosphereRadius, planetRadius, d, groundHit, resMu),
    map_theta(theta, resTheta));
}

/* Returns mu, theta. */
float2 unmapSkyRenderCoordinate(float r, float u_mu, float u_theta,
  float atmosphereRadius, float planetRadius, float resMu, float resTheta) {
  return float2(unmap_mu_with_r(r, u_mu, atmosphereRadius, planetRadius, resMu),
    unmap_theta(u_theta, resTheta));
}

/* Returns d from mu and theta angles. */
float3 mu_theta_to_d(float cos_mu, float theta, float3 O) {
  /* Construct local frame. TODO: function? */
  float3 y = normalize(O);
  float3 k = float3(1, 0, 0);
  float3 z = cross(y, k);
  float3 x = cross(z, y);
  /* Recover d via projection onto local axes. */
  float3 dy = cos_mu * y;
  float sin_mu = safeSqrt(1 - cos_mu * cos_mu);
  float3 dx = sin_mu * cos(theta) * x;
  float3 dz = sin_mu * sin(theta) * z;
  /* The normalize shouldn't be necessary, but it is a good sanity check. */
  return normalize(dx + dy + dz);
}

float d_to_theta(float3 d, float3 O) {
  /* Construct local frame. */
  float3 y = normalize(O);
  float3 k = float3(1, 0, 0);
  float3 z = cross(y, k);
  float3 x = cross(z, y);
  /* Get cosine and sine of theta from projection onto local axes. */
  float3 dProj = normalize(d - dot(y, d) * y);
  float cosTheta = dot(dProj, x);
  float sinTheta = dot(dProj, z);
  float theta = acos(cosTheta);
  if (floatLT(sinTheta, 0)) {
    theta = 2 * PI - theta;
  }
  return theta;
}

/* Returns xyzw, where
 *  -xyz: world space direction.
 *  -z: depth, scaled according to clip space to ensure that we have a
 *  view-aligned plane. */
float4 unmapFrustumCoordinate(float3 uvw) {
  /* Clip space xy coordinate. */
  float2 xy = uvw.xy * _currentScreenSize.xy;
  float3 clipSpaceD = -normalize(mul(float4(xy.x, xy.y, 1, 1), _pCoordToViewDir).xyz);

  /* Get camera center, and angle between direction and center. */
    /* Depth. TODO: non-linearize if necessary. */
  float depth = uvw.z * _farClip;
  float3 cameraCenterD = -normalize(mul(float4(_currentScreenSize.xy/2.0, 1, 1), _pCoordToViewDir).xyz);
  float cosTheta = dot(cameraCenterD, clipSpaceD);
  /* Divide depth through by cos theta. */
  depth /= max(cosTheta, 0.00001);

  return float4(clipSpaceD, depth);
}

/* Maps linear depth to frustum coordinate. */
float3 mapFrustumCoordinate(float2 positionCS, float linearDepth) {
  return float3(positionCS/_currentScreenSize.xy, linearDepth / _farClip);
}

#endif // EXPANSE_SKY_MAPPING_INCLUDED
