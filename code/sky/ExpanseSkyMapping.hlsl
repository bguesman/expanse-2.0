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

DeepTexCoord uvwToDeepTexCoord(float u, float v, float w, int numRows, int numCols, int numStacks) {
  DeepTexCoord result; // final result

  float i_id = ((u) * (numRows)); // fractional row number
  float j_id = ((v) * (numCols)); // fractional column number
  float k_id = ((w) * (numStacks)); // fractional stack number

  // Clamp j and k to avoid unwanted interpolation across row boundaries.
  if (j_id < 0.5) {
    j_id = 0.5;
  } else if (j_id > numCols - 0.5) {
    j_id = numCols - 0.5;
  }

  if (k_id < 0.5) {
    k_id = 0.5;
  } else if (k_id > numStacks - 0.5) {
    k_id = numStacks - 0.5;
  }

  result.ax = frac(i_id); // 0-1 along row
  result.ay = frac(j_id); // 0-1 along column

  float i_size = 1.0/(numRows);
  float j_size = 1.0/(numRows * numCols);
  float k_size = 1.0/(numRows * numCols * numStacks);

  result.coord_00 = (floor(i_id) * i_size) + (floor(j_id) * j_size) + k_id * k_size;
  result.coord_01 = (floor(i_id) * i_size) + (ceil(j_id) * j_size) + k_id * k_size;
  result.coord_10 = (ceil(i_id) * i_size) + (floor(j_id) * j_size) + k_id * k_size;
  result.coord_11 = (ceil(i_id) * i_size) + (ceil(j_id) * j_size) + k_id * k_size;
  return result;
}

// TODO: I'm like 90% certain something is wrong here, based on the artifacts
// you see when you're higher in the sky in the aerial perspective. Looks like
// if you go really high, you see this weird artifact at the edges of the
// planet.

// TODO: this could be it: at the outer edge boundaries of the deep tex UV,
// you're gonna wrap around if you use a linear clamp sampler (it won't clamp
// because the texture is 1d). So you've gotta point sample there. And that
// could easily be what's causing the visible lines---the lack of clamping
// at the boundaries, and instead interpolating, possibly into some part
// of the texture that's not meant to be interpolated into.

/* Converts deep texture index in range zTexSize * zTexCount to the
 * uv coordinate in unit range that represents the 2D table index for a
 * table with zTexSize rows and zTexCount columns.
 *
 * Returns (u, v).
 */
float3 deepTexIndexToUV(uint deepTexIndex, int numRows, int numCols, int numStacks) {
  int i_id = deepTexIndex / (numCols * numStacks);
  int j_id = (deepTexIndex / numStacks) % numCols;
  int k_id = deepTexIndex % numStacks;
  float u = saturate((i_id + 0.5) / (numRows));
  float v = saturate((j_id + 0.5) / (numCols));
  float w = saturate((k_id + 0.5) / (numStacks));
  return float3(u, v, w);
}

/* The following are the map/unmap functions for each individual parameter that
 * the higher-level table coordinate mapping functions rely on. */

/* Maps r, the distance from the planet origin, into range 0-1. Uses
 * mapping from bruneton and neyer. */
// float map_r(float r, float atmosphereRadius, float planetRadius) {
//   float planetRadiusSq = planetRadius * planetRadius;
//   float rho = safeSqrt(r * r - planetRadiusSq);
//   float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
//   return rho / H;
// }
//
// /* Unmaps r, the distance from the planet origin, from its mapped uv
//  * coordinate. Uses mapping from bruneton and neyer. */
// float unmap_r(float u_r, float atmosphereRadius, float planetRadius) {
//   float planetRadiusSq = planetRadius * planetRadius;
//   float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
//   float rho = u_r * H;
//   return safeSqrt(rho * rho + planetRadiusSq);
// }
//
//
// /* Maps mu, the cosine of the viewing angle, into range 0-1. Uses
//  * mapping from bruneton and neyer. */
// float map_mu(float r, float mu, float atmosphereRadius, float planetRadius,
//   float d, bool groundHit, float resMu) {
//   float planetRadiusSq = planetRadius * planetRadius;
//   float rSq = r * r;
//   float rho = safeSqrt(r * r - planetRadiusSq);
//   float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
//
//   float u_mu = 0.0;
//   float muStep = 1/resMu;
//   float discriminant = rSq * mu * mu - rSq + planetRadiusSq;
//   if (groundHit) {
//     float d_min = r - planetRadius;
//     float d_max = rho;
//     d = clamp(d, d_min, d_max);
//     /* Use lower half of [0, 1] range. */
//     u_mu = 0.5 - 0.5 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
//     if (floatGT(u_mu, 0.5-muStep/2)) {
//       u_mu = 0.5 - muStep/2;
//     }
//     if (floatLT(u_mu, muStep/2)) {
//       u_mu = muStep/2;
//     }
//   } else {
//     float d_min = atmosphereRadius - r;
//     float d_max = rho + H;
//     d = clamp(d, d_min, d_max);
//     /* Use upper half of [0, 1] range. */
//     u_mu = 0.5 + 0.5 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
//     if (floatLT(u_mu, 0.5+muStep/2)) {
//       u_mu = 0.5 + muStep/2;
//     }
//     if (floatGT(u_mu, 1-muStep/2)) {
//       u_mu = 1-muStep/2;
//     }
//   }
//
//   // TODO: not right place, but smoothstep at horizon have to do with mu
//   // artifact??? not sure. bruneton neyer implementation has smoothstep
//   // should take a look at that.
//
//   return u_mu;
// }
//
// /* Unmaps mu, the cosine of the viewing angle, from its mapped uv
//  * coordinate. Uses mapping from bruneton and neyer.  */
// float unmap_mu(float u_r, float u_mu, float atmosphereRadius,
//   float planetRadius) {
//   float planetRadiusSq = planetRadius * planetRadius;
//   float H = safeSqrt(atmosphereRadius * atmosphereRadius
//     - planetRadiusSq);
//   float rho = u_r * H;
//   float r = safeSqrt(rho * rho + planetRadiusSq);
//
//   float mu = 0.0;
//   if (floatLT(u_mu, 0.5)) {
//     float d_min = r - planetRadius;
//     float d_max = rho;
//     float d = d_min + (((0.5 - u_mu) / 0.5) * (d_max - d_min));
//     mu = (d == 0.0) ? -1.0 : clampCosine(-(rho * rho + d * d) / (2 * r * d));
//   } else {
//     float d_min = atmosphereRadius - r;
//     float d_max = rho + H;
//     float d = d_min + (((u_mu - 0.5) / 0.5) * (d_max - d_min));
//     mu = (d == 0.0) ? 1.0 : clampCosine((H * H - rho * rho - d * d) / (2 * r * d));
//   }
//
//   return mu;
// }
//
// /* Maps r and mu together---slightly more efficient than mapping them
//  * individually, since they share calculations. */
// float2 map_r_mu(float r, float mu, float atmosphereRadius, float planetRadius,
//   float d, bool groundHit, float resMu) {
//   float planetRadiusSq = planetRadius * planetRadius;
//   float rSq = r * r;
//   float rho = safeSqrt(rSq - planetRadiusSq);
//   float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
//
//   float u_mu = 0.0;
//   float muStep = 1/resMu;
//   float discriminant = rSq * mu * mu - rSq + planetRadiusSq;
//   if (groundHit) {
//     float d_min = r - planetRadius;
//     float d_max = rho;
//     d = clamp(d, d_min, d_max);
//     /* Use lower half of [0, 1] range. */
//     u_mu = 0.5 - 0.5 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
//     if (floatGT(u_mu, 0.5-muStep/2)) {
//       u_mu = 0.5 - muStep/2;
//     }
//     if (floatLT(u_mu, muStep/2)) {
//       u_mu = muStep/2;
//     }
//   } else {
//     float d_min = atmosphereRadius - r;
//     float d_max = rho + H;
//     d = clamp(d, d_min, d_max);
//     /* Use upper half of [0, 1] range. */
//     u_mu = 0.5 + 0.5 * (d_max == d_min ? 0.0 : (d - d_min) / (d_max - d_min));
//     if (floatLT(u_mu, 0.5+muStep/2)) {
//       u_mu = 0.5 + muStep/2;
//     }
//     if (floatGT(u_mu, 1-muStep/2)) {
//       u_mu = 1-muStep/2;
//     }
//   }
//
//   float u_r = rho / H;
//
//   return float2(u_r, u_mu);
// }
//
// /* Unmaps mu and r together---slightly more efficient than unmapping them
//  * individually, since they share calculations.  */
// float2 unmap_r_mu(float u_r, float u_mu, float atmosphereRadius,
//   float planetRadius) {
//   float planetRadiusSq = planetRadius * planetRadius;
//   float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
//   float rho = u_r * H;
//   float r = safeSqrt(rho * rho + planetRadiusSq);
//
//   float mu = 0.0;
//   if (floatLT(u_mu, 0.5)) {
//     float d_min = r - planetRadius;
//     float d_max = rho;
//     float d = d_min + (((0.5 - u_mu) / 0.5) * (d_max - d_min));
//     mu = (d == 0.0) ? -1.0 : clampCosine(-(rho * rho + d * d) / (2 * r * d));
//   } else {
//     float d_min = atmosphereRadius - r;
//     float d_max = rho + H;
//     float d = d_min + (((u_mu - 0.5) / 0.5) * (d_max - d_min));
//     mu = (d == 0.0) ? 1.0 : clampCosine((H * H - rho * rho - d * d) / (2 * r * d));
//   }
//
//   return float2(r, mu);
// }

// float map_r(float r, float atmosphereRadius, float planetRadius) {
//   return (r - planetRadius) / (atmosphereRadius - planetRadius);
// }
//
// float unmap_r(float u_r, float atmosphereRadius, float planetRadius) {
//   return planetRadius + u_r * (atmosphereRadius - planetRadius);
// }
//
// float map_mu(float r, float mu, float atmosphereRadius, float planetRadius,
//   float d, bool groundHit) {
//   return (mu + 1) / 2;
// }
//
// float unmap_mu(float u_r, float u_mu, float atmosphereRadius,
//   float planetRadius) {
//   return u_mu * 2 - 1;
// }
//
// float2 map_r_mu(float r, float mu, float atmosphereRadius, float planetRadius,
//   float d, bool groundHit) {
//   return float2((r - planetRadius) / (atmosphereRadius - planetRadius), (mu + 1) / 2);
// }
//
// float2 unmap_r_mu(float u_r, float u_mu, float atmosphereRadius,
//   float planetRadius) {
//   return float2(planetRadius + u_r * (atmosphereRadius - planetRadius), u_mu * 2 - 1);
// }

/* Maps mu_l, the cosine of the sun zenith angle, into range 0-1. Uses
 * mapping from bruneton and neyer. */
// float map_mu_l(float mu_l) {
//   return saturate((1.0 - exp(-3 * mu_l - 0.6)) / (1 - exp(-3.6)));
// }
//
// /* Unmaps mu_l, the cosine of the sun zenith angle, from its mapped uv
//  * coordinate. Uses mapping from bruneton and neyer. */
// float unmap_mu_l(float u_mu_l) {
//   return clampCosine((log(1.0 - (u_mu_l * (1 - exp(-3.6)))) + 0.6) / -3.0);
// }
//
//
// /* Maps nu, the cosine of the sun azimuth angle, into range 0-1. Uses
//  * mapping from bruneton and neyer. */
// float map_nu(float nu) {
//   return saturate((1.0 + nu) / 2.0);
// }
//
// /* Unmaps nu, the cosine of the sun azimuth angle, from its mapped uv
//  * coordinate. Uses mapping from bruneton and neyer. */
// float unmap_nu(float u_nu) {
//   return clampCosine((u_nu * 2.0) - 1.0);
// }










float map_r(float r, float atmosphereRadius, float planetRadius) {
  // float planetRadiusSq = planetRadius * planetRadius;
  // float rho = safeSqrt(r * r - planetRadiusSq);
  // float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
  // return rho / H;
  return safeSqrt((r - planetRadius) / (atmosphereRadius - planetRadius));
}

/* Unmaps r, the distance from the planet origin, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer. */
float unmap_r(float u_r, float atmosphereRadius, float planetRadius) {
  // float planetRadiusSq = planetRadius * planetRadius;
  // float H = safeSqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);
  // float rho = u_r * H;
  // return safeSqrt(rho * rho + planetRadiusSq);
  return planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));
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
    u_mu = 0.5 * pow((cos_h - mu) / (1 + cos_h), 0.2);
    if (floatGT(u_mu, 0.5-muStep/2)) {
      u_mu = 0.5 - muStep/2;
    }
    if (floatLT(u_mu, muStep/2)) {
      u_mu = muStep/2;
    }
  } else {
    mu = max(mu, cos_h);
    u_mu = 0.5 * pow((mu - cos_h) / (1 - cos_h), 0.2) + 0.5;
    if (floatLT(u_mu, 0.5+muStep/2)) {
      u_mu = 0.5 + muStep/2;
    }
    if (floatGT(u_mu, 1-muStep/2)) {
      u_mu = 1-muStep/2;
    }
  }

  // TODO: not right place, but smoothstep at horizon have to do with mu
  // artifact??? not sure. bruneton neyer implementation has smoothstep
  // should take a look at that.

  return u_mu;
}

/* Unmaps mu, the cosine of the viewing angle, from its mapped uv
 * coordinate. Uses mapping from bruneton and neyer.  */
float unmap_mu(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius) {
  float r = planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));

  float mu = 0.0;
  float h = r - planetRadius;
  float cos_h = -safeSqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (u_mu < 0.5) {
    mu = clampCosine(cos_h - pow(u_mu * 2, 5) * (1 + cos_h));
  } else {
    mu = clampCosine(pow(2 * (u_mu - 0.5), 5) * (1 - cos_h) + cos_h);
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
  float cos_h = -sqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (groundHit) {
    mu = min(mu, cos_h);
    u_mu = 0.5 * pow((cos_h - mu) / (1 + cos_h), 0.2);
    if (floatGT(u_mu, 0.5-muStep/2)) {
      u_mu = 0.5 - muStep/2;
    }
    if (floatLT(u_mu, muStep/2)) {
      u_mu = muStep/2;
    }
  } else {
    mu = max(mu, cos_h);
    u_mu = 0.5 * pow((mu - cos_h) / (1 - cos_h), 0.2) + 0.5;
    if (floatLT(u_mu, 0.5+muStep/2)) {
      u_mu = 0.5 + muStep/2;
    }
    if (floatGT(u_mu, 1-muStep/2)) {
      u_mu = 1-muStep/2;
    }
  }

  float u_r = safeSqrt((r - planetRadius) / (atmosphereRadius - planetRadius));

  return float2(u_r, u_mu);
}

/* Unmaps mu and r together---slightly more efficient than unmapping them
 * individually, since they share calculations.  */
float2 unmap_r_mu(float u_r, float u_mu, float atmosphereRadius,
  float planetRadius) {
  float r = planetRadius + ((u_r * u_r) * (atmosphereRadius - planetRadius));

  float mu = 0.0;
  float h = r - planetRadius;
  float cos_h = -sqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (u_mu < 0.5) {
    mu = clampCosine(cos_h - pow(u_mu * 2, 5) * (1 + cos_h));
  } else {
    mu = clampCosine(pow(2 * (u_mu - 0.5), 5) * (1 - cos_h) + cos_h);
  }

  return float2(r, mu);
}

// float map_mu_l(float mu_l) {
//   return saturate(0.5 * ((atan(max(mu_l, -0.1975)*tan(1.26*1.1)) / 1.1) + (1-0.26)));
// }
//
// /* Unmaps mu_l, the cosine of the sun zenith angle, from its mapped uv
//  * coordinate. Uses mapping from bruneton and neyer. */
// float unmap_mu_l(float u_mu_l) {
//   return clampCosine( (tan(1.1 * ((2 * u_mu_l) - (1-0.26))) / tan(1.26*1.1)) );
// }


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




























/* Returns u_r, u_mu. */
float2 mapSky2DCoord(float r, float mu, float atmosphereRadius,
  float planetRadius, float d, bool groundHit, float resMu) {
  return map_r_mu(r, mu, atmosphereRadius, planetRadius, d, groundHit, resMu);
}

/* Returns r, mu. */
float2 unmapSky2DCoord(float u_r, float u_mu,
  float atmosphereRadius, float planetRadius) {
  return unmap_r_mu(u_r, u_mu, atmosphereRadius, planetRadius);
}

/* Returns u_r, u_mu, u_mu_l/u_nu bundled into z. */
TexCoord4D mapSky4DCoord(float r, float mu, float mu_l,
  float nu, float atmosphereRadius, float planetRadius, float d,
  bool groundHit, uint resR, uint resMu, uint resMuL, uint resNu) {
  float2 u_r_mu = map_r_mu(r, mu, atmosphereRadius, planetRadius,
    d, groundHit, resMu);
  DeepTexCoord deepYZW = uvwToDeepTexCoord(u_r_mu.x, map_mu_l(mu_l), map_nu(nu),
    resR, resMuL, resNu);
  TexCoord4D toRet;
  toRet.x = u_r_mu.y;
  toRet.d = deepYZW;
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
