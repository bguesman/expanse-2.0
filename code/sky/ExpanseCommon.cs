using UnityEngine;
using UnityEngine.Rendering;

/* Class for common global variables shared between classes. */
namespace Expanse {

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
    [GenerateHLSL]
    public enum PhaseFunction {
      Isotropic = 0,
      Rayleigh,
      Mie
    };
    public const uint kMaxPhaseFunctions = 3;

    /* Enum for atmosphere layer density distribution types. */
    [GenerateHLSL]
    public enum DensityDistribution {
      Exponential = 0,
      Tent
    };
    public const uint kMaxDensityDistributions = 3;

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
      public Vector2 T;
      public Vector4 SS;
      public Vector2 MS;
      public Vector4 MSAccumulation;
      public Vector2 LP;
      public int GI;
    }

    /* Given a sky texture quality, returns the corresonding resolution. */
    public static SkyTextureResolution skyQualityToSkyTextureResolution(SkyTextureQuality quality) {
      switch (quality) {
        case SkyTextureQuality.Potato:
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(16, 16),
            SS = new Vector4(16, 16, 16, 16),
            MS = new Vector2(16, 16),
            MSAccumulation = new Vector4(16, 16, 16, 16),
            LP = new Vector2(16, 16),
            GI = 16
          };
        case SkyTextureQuality.Low:
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(16, 16),
            SS = new Vector4(16, 16, 16, 16),
            MS = new Vector2(16, 16),
            MSAccumulation = new Vector4(16, 16, 16, 16),
            LP = new Vector2(16, 16),
            GI = 16
          };
        case SkyTextureQuality.Medium:
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(16, 16),
            SS = new Vector4(16, 16, 16, 16),
            MS = new Vector2(16, 16),
            MSAccumulation = new Vector4(16, 16, 16, 16),
            LP = new Vector2(16, 16),
            GI = 16
          };
        case SkyTextureQuality.High:
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(32, 128),
            SS = new Vector4(32, 128, 32, 32),
            MS = new Vector2(32, 32),
            MSAccumulation = new Vector4(32, 128, 32, 32),
            LP = new Vector2(32, 128),
            GI = 128
          };
        case SkyTextureQuality.Ultra:
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(32, 128),
            SS = new Vector4(32, 128, 32, 32),
            MS = new Vector2(32, 32),
            MSAccumulation = new Vector4(32, 128, 32, 32),
            LP = new Vector2(32, 128),
            GI = 128
          };
        case SkyTextureQuality.RippingThroughTheMetaverse:
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(32, 128),
            SS = new Vector4(32, 128, 32, 32),
            MS = new Vector2(32, 32),
            MSAccumulation = new Vector4(32, 128, 32, 32),
            LP = new Vector2(32, 128),
            GI = 128
          };
        default:
          /* To be safe, default case. Returns potato quality. */
          return new SkyTextureResolution() {
            quality = quality,
            T = new Vector2(16, 16),
            SS = new Vector4(16, 16, 16, 16),
            MS = new Vector2(16, 16),
            MSAccumulation = new Vector4(16, 16, 16, 16),
            LP = new Vector2(16, 16),
            GI = 16
          };
      }
    }

    public static Vector2 degreesToRadians(Vector2 angles) {
      return (angles / 180.0f) * Mathf.PI;
    }

    public static Vector3 anglesToDirectionVector(Vector2 angles) {
      return new Vector3(Mathf.Sin(angles.x) * Mathf.Cos(angles.y),
        Mathf.Sin(angles.x) * Mathf.Sin(angles.y), Mathf.Cos(angles.x));
    }

  }

} /* namespace Expanse */
