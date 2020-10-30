using UnityEngine;
using UnityEngine.Rendering;

/* Class for common global variables shared between classes. */
namespace ExpanseCommonNamespace {

public class ExpanseCommon {
/* Enum for atmosphere layers. Currently we support up to 8 different
 * atmosphere layers. */
public enum AtmosphereLayer {
  Layer0 = 0,
  Layer1,
  Layer2,
  Layer3,
  Layer4,
  Layer5,
  Layer6,
  Layer7
};
public const uint kMaxAtmosphereLayers = 8;

/* Enum for phase function types. */
public enum PhaseFunction {
  Isotropic = 0,
  Rayleigh,
  Mie
};
public const uint kMaxPhaseFunctions = 3;

/* Enum for atmosphere layer density distribution types. */
public enum DensityDistribution {
  Exponential = 0,
  Tent
};
public const uint kMaxDensityDistributions = 2;

/* Enum for celestial bodies. Currently we support up to 8 different
 * celestial bodies. */
public enum CelestialBody {
  Body0 = 0,
  Body1,
  Body2,
  Body3,
  Body4,
  Body5,
  Body6,
  Body7
};
public const uint kMaxCelestialBodies = 8;

/* Enum for texture quality of stored tables. */
public enum SkyTextureQuality {
  Potato = 0,
  Low,
  Medium,
  High,
  Ultra,
  RippingThroughTheMetaverse
}
public const uint kMaxSkyTextureQuality = 6;

/* Struct specifying texture resolutions for sky tables. */
public struct SkyTextureResolution {
  public SkyTextureQuality quality;
  public Vector2 T;   // Transmittance LUT.
  public Vector2 MS;  // Multiple scattering LUT.
  public int GI;      // Ground Irradiance LUT.
  public Vector2 LP;  // Light Pollution LUT.
  public Vector2 SS;  // Single scattering full sky texture.
  public Vector2 MSAccumulation; // Multiple scattering full sky texture.
  public Vector3 AP;  // Aerial perspective frustum texture.
}

/* Given a sky texture quality, returns the corresonding resolution. */
public static SkyTextureResolution skyQualityToSkyTextureResolution(SkyTextureQuality quality) {
  switch (quality) {
    case SkyTextureQuality.Potato:
      return new SkyTextureResolution() {
        /* Each atmosphere layer of the entire sky fits in a 512x512 texture.
         * I don't think you can get more optimized than this without looking
         * so bad it would be impossible to put in the game. */
        quality = quality,
        T = new Vector2(128, 512),
        MS = new Vector2(8, 8),
        GI = 32,
        LP = new Vector2(8, 8),
        SS = new Vector2(64, 32),
        MSAccumulation = new Vector2(64, 32),
        AP = new Vector3(32, 32, 32)
      };
    case SkyTextureQuality.Low:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        GI = 32,
        LP = new Vector2(32, 32),
        SS = new Vector2(128, 32),
        MSAccumulation = new Vector2(128, 32),
        AP = new Vector3(32, 32, 32)
      };
    case SkyTextureQuality.Medium:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        GI = 32,
        LP = new Vector2(32, 32),
        SS = new Vector2(128, 64),
        MSAccumulation = new Vector2(128, 64),
        AP = new Vector3(32, 32, 32)
      };
    case SkyTextureQuality.High:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(128, 256),
        MS = new Vector2(32, 32),
        GI = 32,
        LP = new Vector2(32, 32),
        SS = new Vector2(256, 64),
        MSAccumulation = new Vector2(256, 64),
        AP = new Vector3(32, 32, 64)
      };
    case SkyTextureQuality.Ultra:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        GI = 32,
        LP = new Vector2(32, 32),
        SS = new Vector2(64, 256),
        MSAccumulation = new Vector2(64, 256),
        AP = new Vector3(32, 32, 32)
      };
    case SkyTextureQuality.RippingThroughTheMetaverse:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        GI = 32,
        LP = new Vector2(32, 32),
        SS = new Vector2(64, 256),
        MSAccumulation = new Vector2(64, 256),
        AP = new Vector3(32, 32, 32)
      };
    default:
      /* To be safe, default case. Returns potato quality. */
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        GI = 32,
        LP = new Vector2(32, 32),
        SS = new Vector2(64, 256),
        MSAccumulation = new Vector2(64, 256),
        AP = new Vector3(32, 32, 32)
      };
  }
}

/* Enum for texture quality of stored star tables. */
public enum StarTextureQuality {
  Low = 0,
  Medium,
  High,
  Ultra,
  RippingThroughTheMetaverse
}
public const uint kMaxStarTextureQuality = 6;

/* Struct specifying texture resolutions for star tables. */
public struct StarTextureResolution {
  public StarTextureQuality quality;
  public Vector2 Star;
}

/* Given a sky texture quality, returns the corresonding resolution. */
public static StarTextureResolution StarQualityToStarTextureResolution(StarTextureQuality quality) {
  switch (quality) {
    case StarTextureQuality.Low:
      return new StarTextureResolution() {
        quality = quality,
        Star = new Vector2(512, 512)
      };
    case StarTextureQuality.Medium:
      return new StarTextureResolution() {
        quality = quality,
        Star = new Vector2(1024, 1024)
      };
    case StarTextureQuality.High:
      return new StarTextureResolution() {
        quality = quality,
        Star = new Vector2(2048, 2048)
      };
    case StarTextureQuality.Ultra:
      return new StarTextureResolution() {
        quality = quality,
        Star = new Vector2(4096, 4096)
      };
    case StarTextureQuality.RippingThroughTheMetaverse:
      return new StarTextureResolution() {
        quality = quality,
        Star = new Vector2(8192, 8192)
      };
    default:
      /* To be safe, default case. Returns potato quality. */
      return new StarTextureResolution() {
        quality = quality,
        Star = new Vector2(64, 64)
      };
  }
}

public static Vector3 degreesToRadians(Vector3 angles) {
  return (angles / 180.0f) * Mathf.PI;
}

public static Vector3 anglesToDirectionVector(Vector2 angles) {
  return new Vector3(Mathf.Sin(angles.y) * Mathf.Cos(angles.x),
    Mathf.Sin(angles.y) * Mathf.Sin(angles.x), Mathf.Cos(angles.y));
}

public static Vector3 intersectSphere(Vector3 p, Vector3 d, float r) {
  float A = Vector3.Dot(d, d);
  float B = 2.0f * Vector3.Dot(d, p);
  float C = Vector3.Dot(p, p) - (r * r);
  float det = (B * B) - 4.0f * A * C;
  if (det >= 0) {
    det = Mathf.Sqrt(det);
    return new Vector3((-B + det) / (2.0f * A), (-B - det) / (2.0f * A), 1);
  }
  return new Vector3(0, 0, -1);
}

/* Maps r and mu to uv. For sampling transmittance table to set
 * directional light color. */
public static Vector2 map_r_mu(float r, float mu, float atmosphereRadius,
  float planetRadius, float d, bool groundHit) {
  float planetRadiusSq = planetRadius * planetRadius;
  float rSq = r * r;
  float rho = Mathf.Sqrt(rSq - planetRadiusSq);
  float H = Mathf.Sqrt(atmosphereRadius * atmosphereRadius - planetRadiusSq);

  float u_mu = 0.0f;
  float discriminant = rSq * mu * mu - rSq + planetRadiusSq;
  if (groundHit) {
    float d_min = r - planetRadius;
    float d_max = rho;
    /* Use lower half of [0, 1] range. */
    u_mu = 0.4f - 0.4f * (d_max == d_min ? 0.0f : (d - d_min) / (d_max - d_min));
  } else {
    float d_min = atmosphereRadius - r;
    float d_max = rho + H;
    /* Use upper half of [0, 1] range. */
    u_mu = 0.6f + 0.4f * (d_max == d_min ? 0.0f : (d - d_min) / (d_max - d_min));
  }

  float u_r = rho / H;

  return new Vector2(u_r, u_mu);
}



/******************************************************************************/
/************************* PHYSICAL PROPERTY FUNCTIONS ************************/
/******************************************************************************/

public static Vector4 blackbodyTempToColor(float t) {
  t = t / 100;
  float r = 0;
  float g = 0;
  float b = 0;

  /* Red. */
  if (t <= 66) {
    r = 255;
  } else {
    r = t - 60;
    r = 329.698727446f * (Mathf.Pow(r, -0.1332047592f));
  }

  /* Green. */
  if (t <= 66) {
    g = t;
    g = 99.4708025861f * Mathf.Log(t) - 161.1195681661f;
  } else {
    g = 288.1221695283f * (Mathf.Pow((t-60), -0.0755148492f));
  }

  /* Blue. */
  if (t >= 66) {
    b = 255;
  } else {
    if (t <= 19) {
      b = 0;
    } else {
      b = 138.5177312231f * Mathf.Log(t-b) - 305.0447927307f;
    }
  }

  r = Mathf.Clamp(r, 0, 255) / 255;
  g = Mathf.Clamp(g, 0, 255) / 255;
  b = Mathf.Clamp(b, 0, 255) / 255;
  return new Vector4(r, g, b, 0);
}

/******************************************************************************/
/*********************** END PHYSICAL PROPERTY FUNCTIONS **********************/
/******************************************************************************/



/******************************************************************************/
/******************************* LIGHTING STATE *******************************/
/******************************************************************************/

/* Sadly, this seemed to be the only reasonable way to do this. */
public static Vector3[] bodyTransmittances = new Vector3[kMaxCelestialBodies];

/******************************************************************************/
/***************************** END LIGHTING STATE *****************************/
/******************************************************************************/

} /* class ExpanseCommonNamespace */

} /* namespace Expanse */
