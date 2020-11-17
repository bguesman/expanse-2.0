using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/* Class for common global variables shared between classes. */
namespace ExpanseCommonNamespace {

public class ExpanseCommon {

/******************************************************************************/
/************************************ SKY *************************************/
/******************************************************************************/

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
  Tent,
  ExponentialAttenuated
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
        T = new Vector2(32, 64),
        MS = new Vector2(8, 8),
        SS = new Vector2(64, 32),
        MSAccumulation = new Vector2(64, 32),
        AP = new Vector3(8, 8, 8)
      };
    case SkyTextureQuality.Low:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 128),
        MS = new Vector2(16, 16),
        SS = new Vector2(128, 64),
        MSAccumulation = new Vector2(128, 64),
        AP = new Vector3(16, 16, 16)
      };
    case SkyTextureQuality.Medium:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        SS = new Vector2(192, 64),
        MSAccumulation = new Vector2(128, 64),
        AP = new Vector3(16, 16, 32)
      };
    case SkyTextureQuality.High:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(64, 256),
        MS = new Vector2(32, 32),
        SS = new Vector2(256, 64),
        MSAccumulation = new Vector2(128, 64),
        AP = new Vector3(16, 16, 32)
      };
    case SkyTextureQuality.Ultra:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(128, 512),
        MS = new Vector2(64, 64),
        SS = new Vector2(512, 64),
        MSAccumulation = new Vector2(128, 64),
        AP = new Vector3(32, 32, 32)
      };
    case SkyTextureQuality.RippingThroughTheMetaverse:
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(256, 1024),
        MS = new Vector2(64, 64),
        SS = new Vector2(512, 128),
        MSAccumulation = new Vector2(256, 128),
        AP = new Vector3(32, 32, 32)
      };
    default:
      /* To be safe, default case. Returns potato quality. */
      return new SkyTextureResolution() {
        quality = quality,
        T = new Vector2(32, 64),
        MS = new Vector2(8, 8),
        SS = new Vector2(64, 32),
        MSAccumulation = new Vector2(64, 32),
        AP = new Vector3(8, 8, 8)
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

/******************************************************************************/
/********************************** END SKY ***********************************/
/******************************************************************************/



/******************************************************************************/
/*********************************** CLOUDS ***********************************/
/******************************************************************************/

/* Enum for cloud layers. Currently we support up to 8 different
 * cloud layers. */
public enum CloudLayer {
  Layer0 = 0,
  Layer1,
  Layer2,
  Layer3,
  Layer4,
  Layer5,
  Layer6,
  Layer7
};
public const uint kMaxCloudLayers = 8;

public enum CloudGeometryType {
  Plane,      /* Clouds are 2D, on a flat plane at some altitude. */
  Sphere,     /* Clouds surround the planet in a sphere at some altitude. */
  BoxVolume   /* Clouds are volumetric and distributed throughout a rectangular box. */
};
public const uint kMaxCloudGeometryTypes = 3;

/* Enum for different cloud noise dimension types. */
public enum CloudNoiseDimension {
  TwoD = 0,
  ThreeD
};
public const uint kMaxCloudNoiseDimensionTypes = 2;

/* Map of geometry types to dimensions. */
public static Dictionary<CloudGeometryType, CloudNoiseDimension> cloudGeometryTypeToDimension = new Dictionary<CloudGeometryType, CloudNoiseDimension>(){
	{CloudGeometryType.Plane, CloudNoiseDimension.TwoD},
	{CloudGeometryType.Sphere, CloudNoiseDimension.TwoD},
	{CloudGeometryType.BoxVolume, CloudNoiseDimension.ThreeD}
};

/* Enum for noise types. */
public enum CloudNoiseType {
  Value = 0,
  Perlin,
  Voronoi,
  Worley,
  Curl,
  PerlinWorley,
  Constant
};
public const uint kCloudNoiseTypes = 7;

/* Map of noise types to compute shader kernel names. */
public static Dictionary<CloudNoiseType, string> cloudNoiseTypeToKernelName = new Dictionary<CloudNoiseType, string>(){
	{CloudNoiseType.Value, "VALUE"},
	{CloudNoiseType.Perlin, "PERLIN"},
	{CloudNoiseType.Voronoi, "VORONOI"},
  {CloudNoiseType.Worley, "WORLEY"},
  {CloudNoiseType.Curl, "CURL"},
  {CloudNoiseType.PerlinWorley, "PERLINWORLEY"},
  {CloudNoiseType.Constant, "CONSTANT"}
};

/* Enum for noise layers. */
public enum CloudNoiseLayer {
  Coverage = 0,
  Base,
  Structure,
  Detail,
  BaseWarp,
  DetailWarp
}
public const uint kCloudNoiseLayers = 6;

/* Enum for texture quality of stored tables. */
public enum CloudTextureQuality {
  Potato = 0,
  Low,
  Medium,
  High,
  Ultra,
  RippingThroughTheMetaverse
}
public const uint kMaxCloudTextureQuality = 6;

/* Resolution for cloud textures. The same for all dimensions. */
public struct CloudTextureResolution {
  public CloudNoiseDimension dimension;
  public CloudTextureQuality quality;
  public int Coverage;
  public int Base;
  public int Structure;
  public int Detail;
  public int BaseWarp;
  public int DetailWarp;
};

/* Given a 2D cloud texture quality, returns the corresonding resolution. */
public static CloudTextureResolution cloudQualityToCloudTextureResolution(CloudTextureQuality quality, CloudNoiseDimension dimension) {
  /* 2D. */
  if (dimension == CloudNoiseDimension.TwoD) {
    switch (quality) {
      case CloudTextureQuality.Potato:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 128,
          Structure = 128,
          Detail = 64,
          BaseWarp = 128,
          DetailWarp = 64
        };
      case CloudTextureQuality.Low:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 256,
          Structure = 128,
          Detail = 64,
          BaseWarp = 128,
          DetailWarp = 64
        };
      case CloudTextureQuality.Medium:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 512,
          Structure = 256,
          Detail = 64,
          BaseWarp = 128,
          DetailWarp = 64
        };
      case CloudTextureQuality.High:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 512,
          Structure = 256,
          Detail = 64,
          BaseWarp = 128,
          DetailWarp = 64
        };
      case CloudTextureQuality.Ultra:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 1024,
          Structure = 256,
          Detail = 64,
          BaseWarp = 256,
          DetailWarp = 64
        };
      case CloudTextureQuality.RippingThroughTheMetaverse:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 1024,
          Structure = 256,
          Detail = 64,
          BaseWarp = 256,
          DetailWarp = 64
        };
      default:
        return new CloudTextureResolution() {
          dimension = dimension,
          quality = quality,
          Coverage = 256,
          Base = 256,
          Structure = 64,
          Detail = 64,
          BaseWarp = 128,
          DetailWarp = 64
        };
    }
  }
  /* 3D. */
  else {

      switch (quality) {
        case CloudTextureQuality.Potato:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 256,
            Base = 128,
            Structure = 128,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
        case CloudTextureQuality.Low:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 256,
            Base = 128,
            Structure = 128,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
        case CloudTextureQuality.Medium:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 256,
            Base = 128,
            Structure = 128,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
        case CloudTextureQuality.High:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 256,
            Base = 128,
            Structure = 128,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
        case CloudTextureQuality.Ultra:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 256,
            Base = 128,
            Structure = 128,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
        case CloudTextureQuality.RippingThroughTheMetaverse:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 128,
            Base = 256,
            Structure = 256,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
        default:
          return new CloudTextureResolution() {
            dimension = dimension,
            quality = quality,
            Coverage = 512,
            Base = 128,
            Structure = 128,
            Detail = 64,
            BaseWarp = 128,
            DetailWarp = 64
          };
      }
  }
}

/* Since cloud layers are shorter than they are wide, we need less resolution
 * in the y direction. However, we still want to be able to specify them
 * with the same base resolution struct, so this is just a conversion
 * function to allow that. */
public static int cloudXZResolutionToYResolution(int xzResolution) {
  return Mathf.Max(8, xzResolution/8);
}

/******************************************************************************/
/********************************* END CLOUDS *********************************/
/******************************************************************************/



/******************************************************************************/
/********************************* UTILITIES **********************************/
/******************************************************************************/

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
public static float fromUnitToSubUVs(float u, float resolution) {
 const float j = 0.5f;
 return (j + u * (resolution + 1 - 2 * j)) / (resolution + 1);
}
public static Vector2 map_r_mu_transmittance(float r, float mu, float atmosphereRadius,
  float planetRadius, float d, bool groundHit, float resMu) {
  float u_mu = 0;
  float muStep = 1/resMu;
  float h = r - planetRadius;
  float cos_h = -Mathf.Sqrt(h * (2 * planetRadius + h)) / (planetRadius + h);
  if (groundHit) {
    mu = Mathf.Clamp(mu, -1, cos_h);
    u_mu = 0.5f * Mathf.Pow(Mathf.Abs((cos_h - mu) / (1 + cos_h)), 0.2f);
    if (u_mu > 0.5f-muStep/2) {
      u_mu = 0.5f - muStep/2;
    }
  } else {
    mu = Mathf.Clamp(mu, cos_h, 1);
    u_mu = 0.5f * Mathf.Pow(Mathf.Abs((mu - cos_h) / (1 - cos_h)), 0.2f) + 0.5f;
    if (u_mu < 0.5f + muStep/2) {
      u_mu = 0.5f + muStep/2;
    }
  }
  float u_r = Mathf.Sqrt((r - planetRadius) / (atmosphereRadius - planetRadius));
  return new Vector2(u_r, fromUnitToSubUVs(u_mu, resMu));
}

/******************************************************************************/
/******************************* END UTILITIES ********************************/
/******************************************************************************/



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

public static float erf(float x) {
  float sign_x = Mathf.Sign(x);
  x = Mathf.Abs(x);
  const float p = 0.3275911f;
  const float a1 = 0.254829592f;
  const float a2 = -0.284496736f;
  const float a3 = 1.421413741f;
  const float a4 = -1.453152027f;
  const float a5 = 1.061405429f;
  float t = 1 / (1 + p * x);
  float t2 = t * t;
  float t3 = t * t2;
  float t4 = t2 * t2;
  float t5 = t3 * t2;
  float prefactor = a5 * t5 + a4 * t4 + a3 * t3 + a2 * t2 + a1 * t;
  return sign_x * (1 - prefactor * Mathf.Exp(-(x * x)));
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
