#include "../common/shaders/ExpanseSkyCommon.hlsl"

/* Converts u, v in unit range to a deep texture coordinate (w0, w1, a) with
 * zTexSize rows and zTexCount columns.
 *
 * Returns (w0, w1, a), where w0 and w1 are the locations to sample the
 * texture at and a is the blend amount to use when interpolating between
 * them. Mathematically:
 *
 *         sample(u, v) = a * sample(w0) + (1 - a) * sample(w1)
 *
 */
float3 uvToDeepTexCoord(float u, float v, int numRows, int numCols) {
  float w_id = ((u) * (numRows-2)) + 1; // fractional row number
  float k_id = ((v) * (numCols-2)) + 1; // fractional column number
  float a = frac(w_id); // 0-1 along row
  float w_size = 1.0/(numRows); // bucket size for each row
  float k_size = 1.0/((numRows) * (numCols)); // bucket size for each column
  float coord_0 = floor(w_id) * w_size + k_id * k_size;
  float coord_1 = ceil(w_id) * w_size + k_id * k_size;
  return float3(coord_0, coord_1, a);
}

/* Converts deep texture index in range zTexSize * zTexCount to the
 * uv coordinate in unit range that represents the 2D table index for a
 * table with zTexSize rows and zTexCount columns.
 *
 * Returns (u, v).
 */
float2 deepTexIndexToUV(uint deepTexCoord, uint numRows, int numCols) {
  int w_id = deepTexCoord / numCols;
  int k_id = deepTexCoord % numCols;
  float u = saturate((w_id - 0.5) / (numRows-2));
  float v = saturate((k_id - 0.5) / (numCols-2));
  return float2(u, v);
}


/* The following are the map/unmap functions for each individual parameter that
 * the higher-level table coordinate mapping functions rely on. */

/* Maps r, the distance from the planet origin, into range 0-1. Uses
 * mapping from bruneton and neyer. */
float map_r(float r, float atmosphereRadius, float planetRadius) {
  float planetRadiusSq = planetRadius * planetRadius;
  float rho = safeSqrt(r * r - planetRadiusSq);
  float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
  return rho / H;
}

/* Unmaps r, the distance from the planet origin, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer. */
float unmap_r(float u_r, float atmosphereRadius, float planetRadius) {
  float planetRadiusSq = planetRadius * planetRadius;
  float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
  float rho = u_r * H;
  return safeSqrt(rho * rho + planetRadiusSq);
}


/* Maps mu, the cosine of the viewing angle, into range 0-1. Uses
 * mapping from bruneton and neyer. */
float map_mu(float r, float mu, float atmosphereRadius, float planetRadius,
  float d, bool groundHit) {
  float planetRadiusSq = planetRadius * planetRadius;
  float rSq = r * r;
  float rho = safeSqrt(r * r - planetRadiusSq);
  float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);

  float u_mu = 0.0;
  float discriminant = rSq * mu * mu - rSq + planetRadiusSq;
  if (groundHit) {
    float d_min = r - planetRadius;
    float d_max = rho;
    /* Use lower half of [0, 1] range. */
    u_mu = 0.4 - 0.4 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
  } else {
    float d_min = atmosphereRadius - r;
    float d_max = rho + H;
    /* Use upper half of [0, 1] range. */
    u_mu = 0.6 + 0.4 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
  }

  return u_mu;
}

/* Unmaps mu, the cosine of the viewing angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer.  */
float unmap_mu(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius) {
  float planetRadiusSq = planetRadius * planetRadius;
  float H = safeSqrt(atmosphereRadius * atmosphereRadius
    - planetRadiusSq);
  float rho = u_r * H;
  float r = safeSqrt(rho * rho + planetRadiusSq);

  /* Clamp u_mu to valid range. */
  if (floatLT(u_mu, 0.6) && floatGT(u_mu, 0.5)) {
    u_mu = 0.6;
  } else if (floatGT(u_mu, 0.4) && floatLT(u_mu, 0.5)) {
    u_mu = 0.4;
  }

  float mu = 0.0;
  if (floatLT(u_mu, 0.5)) {
    float d_min = r - planetRadius;
    float d_max = rho;
    float d = d_min + (((0.4 - u_mu) / 0.4) * (d_max - d_min));
    mu = (d == 0.0) ? -1.0 : clampCosine(-(rho * rho + d * d) / (2 * r * d));
  } else {
    float d_min = atmosphereRadius - r;
    float d_max = rho + H;
    float d = d_min + (((u_mu - 0.6) / 0.4) * (d_max - d_min));
    mu = (d == 0.0) ? 1.0 : clampCosine((H * H - rho * rho - d * d) / (2 * r * d));
  }

  return mu;
}


/* Maps r and mu together---slightly more efficient than mapping them
 * individually, since they share calculations. */
float2 map_r_mu(float r, float mu, float atmosphereRadius, float planetRadius,
  float d, bool groundHit) {
  float planetRadiusSq = planetRadius * planetRadius;
  float rSq = r * r;
  float rho = safeSqrt(rSq - planetRadiusSq);
  float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);

  float u_mu = 0.0;
  float discriminant = rSq * mu * mu - rSq + planetRadiusSq;
  if (groundHit) {
    float d_min = r - planetRadius;
    float d_max = rho;
    /* Use lower half of [0, 1] range. */
    u_mu = 0.4 - 0.4 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
  } else {
    float d_min = atmosphereRadius - r;
    float d_max = rho + H;
    /* Use upper half of [0, 1] range. */
    u_mu = 0.6 + 0.4 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
  }

  float u_r = rho / H;

  return float2(u_r, u_mu);
}

/* Unmaps mu and r together---slightly more efficient than unmapping them
 * individually, since they share calculations.  */
float2 unmap_r_mu(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius) {
  float planetRadiusSq = planetRadius * planetRadius;
  float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
  float rho = u_r * H;
  float r = safeSqrt(rho * rho + planetRadiusSq);

  /* Clamp u_mu to valid range. */
  if (floatLT(u_mu, 0.6) && floatGT(u_mu, 0.5)) {
    u_mu = 0.6;
  } else if (floatGT(u_mu, 0.4) && floatLT(u_mu, 0.5)) {
    u_mu = 0.4;
  }

  float mu = 0.0;
  if (floatLT(u_mu, 0.5)) {
    float d_min = r - planetRadius;
    float d_max = rho;
    float d = d_min + (((0.4 - u_mu) / 0.4) * (d_max - d_min));
    mu = (d == 0.0) ? -1.0 : clampCosine(-(rho * rho + d * d) / (2 * r * d));
  } else {
    float d_min = atmosphereRadius - r;
    float d_max = rho + H;
    float d = d_min + (((u_mu - 0.6) / 0.4) * (d_max - d_min));
    mu = (d == 0.0) ? 1.0 : clampCosine((H * H - rho * rho - d * d) / (2 * r * d));
  }

  return float2(r, mu);
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
  return saturate((1.0 + nu) / 2.0);
}

/* Unmaps nu, the cosine of the sun azimuth angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer. */
float unmap_nu(float u_nu) {
  return clampCosine((u_nu * 2.0) - 1.0);
}

/* Returns u_r, u_mu. */
float2 mapSky2DCoord(float r, float mu, float atmosphereRadius,
  float planetRadius, float d, bool groundHit) {
  return map_r_mu(r, mu, atmosphereRadius, planetRadius, d, groundHit);
}

/* Returns r, mu. */
float2 unmapSky2DCoord(float u_r, float u_mu,
  float atmosphereRadius, float planetRadius) {
  return unmap_r_mu(u_r, u_mu, atmosphereRadius, planetRadius);
}

/* Returns u_r, u_mu, u_mu_l/u_nu bundled into z. */
TexCoord4D mapSky4DCoord(float r, float mu, float mu_l,
  float nu, float atmosphereRadius, float planetRadius, float d,
  bool groundHit, uint yTexSize, uint yTexCount, uint zTexSize, int zTexCount) {
  float2 u_r_mu = map_r_mu(r, mu, atmosphereRadius, planetRadius,
    d, groundHit);
  float3 deepYTexCoord = uvToDeepTexCoord(u_r_mu.x, u_r_mu.y,
    yTexSize, yTexCount);
  float3 deepZTexCoord = uvToDeepTexCoord(map_mu_l(mu_l), map_nu(nu),
    zTexSize, zTexCount);
  TexCoord4D toRet = {deepYTexCoord.x, deepYTexCoord.y, deepZTexCoord.x,
    deepZTexCoord.y, deepYTexCoord.z, deepZTexCoord.z};
  return toRet;
}

/* Returns u_r, u_mu, u_mu_l/u_nu bundled into z. Overloaded to
 * handle the case where we don't want to recompute r/mu's uv's. */
TexCoord4D mapSky4DCoord(float2 r_mu_uv, float mu_l,
  float nu, float atmosphereRadius, float planetRadius, float d,
  bool groundHit, uint yTexSize, uint yTexCount, uint zTexSize, int zTexCount) {
  float3 deepYTexCoord = uvToDeepTexCoord(r_mu_uv.x, r_mu_uv.y,
    yTexSize, yTexCount);
  float3 deepZTexCoord = uvToDeepTexCoord(map_mu_l(mu_l), map_nu(nu),
    zTexSize, zTexCount);
  TexCoord4D toRet = {deepYTexCoord.x, deepYTexCoord.y, deepZTexCoord.x,
    deepZTexCoord.y, deepYTexCoord.z, deepZTexCoord.z};
  return toRet;
}

/* Returns r, mu, mu_l, and nu. */
float4 unmapSky4DCoord(float u_r, float u_mu, float u_mu_l,
  float u_nu, float atmosphereRadius, float planetRadius) {
  float2 r_mu = unmap_r_mu(u_r, u_mu, atmosphereRadius,
    planetRadius);
  return float4(r_mu.x, r_mu.y, unmap_mu_l(u_mu_l), unmap_nu(u_nu));
}

/* Returns u_mu_l, v. V is always zero, since the texture is effectively 1D. */
float2 mapSky1DCoord(float mu_l) {
  return float2(map_mu_l(mu_l), 0);
}

/* Returns r, mu_l. R is always just above the ground. */
float2 unmapSky1DCoord(float u_mu_l, float _planetRadius) {
  return float2(_planetRadius + 0.01, unmap_mu_l(u_mu_l));
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
