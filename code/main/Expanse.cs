using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using ExpanseCommonNamespace;

[VolumeComponentMenu("Sky/Expanse")]
[SkyUniqueID(EXPANSE_SKY_UNIQUE_ID)]
[Serializable]
public class Expanse : SkySettings {

const int EXPANSE_SKY_UNIQUE_ID = 95837281;

/******************************************************************************/
/*************************** SERIALIZED PARAMETERS ****************************/
/******************************************************************************/

/***********************/
/********* Sky *********/
/***********************/
/* Planet parameters. */
[Tooltip("The total thickness of the atmosphere, in meters.")]
public MinFloatParameter atmosphereThickness = new MinFloatParameter(40000, 10);
[Tooltip("The radius of the planet, in meters.")]
public MinFloatParameter planetRadius = new MinFloatParameter(6360000, 10);
[Tooltip("The ground albedo as a cubemap texture. The ground is modeled as a Lambertian (completely diffuse) reflector. If no texture is specified, the color of the ground will just be the ground tint.")]
public CubemapParameter groundAlbedoTexture = new CubemapParameter(null);
[Tooltip("A color tint to the ground texture. Perfect grey, (128, 128, 128), specifies no tint. If there is no ground texture specified, this is just the color of the ground.")]
public ColorParameter groundTint = new ColorParameter(Color.grey, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("The ground emission as a cubemap texture. Useful for modeling things like city lights. Has no effect on the sky. See \"Light Pollution\" for a way of modeling an emissive ground's effect on the atmosphere.")]
public CubemapParameter groundEmissionTexture = new CubemapParameter(null);
[Tooltip("An intensity multiplier on the ground emission texture.")]
public MinFloatParameter groundEmissionMultiplier = new MinFloatParameter(1.0f, 0.0f);
[Tooltip("The rotation of the planet textures as euler angles. This won't do anything to light directions, star rotations, etc. It is purely for rotating the planet's albedo and emissive textures.")]
public Vector3Parameter planetRotation = new Vector3Parameter(new Vector3(0.0f, 0.0f, 0.0f));

/* Unfortunately, Unity cannot serialize arrays of type System.object.
 * So we need individual member variables for each layer. */
[Tooltip("Whether or not this atmosphere layer is enabled.")]
public BoolParameter layerEnabled0, layerEnabled1, layerEnabled2,
  layerEnabled3, layerEnabled4, layerEnabled5, layerEnabled6, layerEnabled7;
[Tooltip("Absorption coefficients for this atmosphere layer. For wavelength-independent absorption, set all coefficients to the same value.")]
public Vector3Parameter layerCoefficientsA0, layerCoefficientsA1, layerCoefficientsA2,
  layerCoefficientsA3, layerCoefficientsA4, layerCoefficientsA5, layerCoefficientsA6, layerCoefficientsA7;
[Tooltip("Scattering coefficients for this atmosphere layer. For wavelength-independent scattering, set all coefficients to the same value.")]
public Vector3Parameter layerCoefficientsS0, layerCoefficientsS1, layerCoefficientsS2,
  layerCoefficientsS3, layerCoefficientsS4, layerCoefficientsS5, layerCoefficientsS6, layerCoefficientsS7;
[Tooltip("Density distribution type for this atmosphere layer.")]
public EnumParameter<ExpanseCommon.DensityDistribution> layerDensityDistribution0, layerDensityDistribution1, layerDensityDistribution2,
  layerDensityDistribution3, layerDensityDistribution4, layerDensityDistribution5, layerDensityDistribution6, layerDensityDistribution7;
[Tooltip("Height of this atmosphere layer.")]
public MinFloatParameter layerHeight0, layerHeight1, layerHeight2,
  layerHeight3, layerHeight4, layerHeight5, layerHeight6, layerHeight7;
[Tooltip("Thickness of this atmosphere layer.")]
public MinFloatParameter layerThickness0, layerThickness1, layerThickness2,
  layerThickness3, layerThickness4, layerThickness5, layerThickness6, layerThickness7;
[Tooltip("Phase function to use for this atmosphere layer. Isotropic phase functions are useful for modeling simple non-directional scattering. The Rayleigh phase function is useful for modeling air and gases. The Mie phase function is good for modeling smoke, fog, and aerosols.")]
public EnumParameter<ExpanseCommon.PhaseFunction> layerPhaseFunction0, layerPhaseFunction1, layerPhaseFunction2,
  layerPhaseFunction3, layerPhaseFunction4, layerPhaseFunction5, layerPhaseFunction6, layerPhaseFunction7;
[Tooltip("Anisotropy of this atmosphere layer. Higher values will give more forward scattering. Lower values will give more backward scattering. A value of zero is neutral.")]
public ClampedFloatParameter layerAnisotropy0, layerAnisotropy1, layerAnisotropy2,
  layerAnisotropy3, layerAnisotropy4, layerAnisotropy5, layerAnisotropy6, layerAnisotropy7;
[Tooltip("Density of this atmosphere layer.")]
public MinFloatParameter layerDensity0, layerDensity1, layerDensity2,
  layerDensity3, layerDensity4, layerDensity5, layerDensity6, layerDensity7;
[Tooltip("Whether or not to use density attenuation for this layer. This will attenuate the density of the layer as the distance to the player increases. It is particularly useful for effects like fog.")]
public BoolParameter layerUseDensityAttenuation0, layerUseDensityAttenuation1, layerUseDensityAttenuation2,
  layerUseDensityAttenuation3, layerUseDensityAttenuation4, layerUseDensityAttenuation5, layerUseDensityAttenuation6, layerUseDensityAttenuation7;
[Tooltip("Attenuation distance of this atmosphere layer. Higher values will result in more gradual attenuation of density. Lower values will attenuate more aggressively.")]
public MinFloatParameter layerAttenuationDistance0, layerAttenuationDistance1, layerAttenuationDistance2,
  layerAttenuationDistance3, layerAttenuationDistance4, layerAttenuationDistance5, layerAttenuationDistance6, layerAttenuationDistance7;
[Tooltip("Attenuation bias of this atmosphere layer. This is the distance from the player at which density attenuation kicks in.")]
public MinFloatParameter layerAttenuationBias0, layerAttenuationBias1, layerAttenuationBias2,
  layerAttenuationBias3, layerAttenuationBias4, layerAttenuationBias5, layerAttenuationBias6, layerAttenuationBias7;
[Tooltip("Tint to this atmosphere layer. Artistic override. A tint of perfect grey (127, 127, 127) is neutral.")]
public ColorParameter layerTint0, layerTint1, layerTint2,
  layerTint3, layerTint4, layerTint5, layerTint6, layerTint7;
[Tooltip("Multiple scattering multipler for this atmosphere layer. Artistic override. 1 is a physically accurate value.")]
public MinFloatParameter layerMultipleScatteringMultiplier0, layerMultipleScatteringMultiplier1, layerMultipleScatteringMultiplier2,
  layerMultipleScatteringMultiplier3, layerMultipleScatteringMultiplier4, layerMultipleScatteringMultiplier5, layerMultipleScatteringMultiplier6, layerMultipleScatteringMultiplier7;

/* Celestial Bodies. */
[Tooltip("Whether or not this celestial body is enabled.")]
public BoolParameter bodyEnabled0, bodyEnabled1, bodyEnabled2,
  bodyEnabled3, bodyEnabled4, bodyEnabled5, bodyEnabled6, bodyEnabled7;
[Tooltip("Celestial body's direction.")]
public Vector3Parameter bodyDirection0, bodyDirection1, bodyDirection2,
  bodyDirection3, bodyDirection4, bodyDirection5, bodyDirection6, bodyDirection7;
[Tooltip("Celestial body's angular radius in the sky, specified in degrees.")]
public ClampedFloatParameter bodyAngularRadius0, bodyAngularRadius1, bodyAngularRadius2,
  bodyAngularRadius3, bodyAngularRadius4, bodyAngularRadius5, bodyAngularRadius6, bodyAngularRadius7;
[Tooltip("Celestial body's distance from the planet, in meters.")]
public MinFloatParameter bodyDistance0, bodyDistance1, bodyDistance2,
  bodyDistance3, bodyDistance4, bodyDistance5, bodyDistance6, bodyDistance7;
[Tooltip("Whether or not this celestial body receives light.")]
public BoolParameter bodyReceivesLight0, bodyReceivesLight1, bodyReceivesLight2,
  bodyReceivesLight3, bodyReceivesLight4, bodyReceivesLight5, bodyReceivesLight6, bodyReceivesLight7;
[Tooltip("Celestial body's albedo texture.")]
public CubemapParameter bodyAlbedoTexture0, bodyAlbedoTexture1, bodyAlbedoTexture2,
  bodyAlbedoTexture3, bodyAlbedoTexture4, bodyAlbedoTexture5, bodyAlbedoTexture6, bodyAlbedoTexture7;
/* Displayed on null check of body albedo texture. */
[Tooltip("Rotation of celestial body's albedo texture, specified by euler angles.")]
public Vector3Parameter bodyAlbedoTextureRotation0, bodyAlbedoTextureRotation1, bodyAlbedoTextureRotation2,
  bodyAlbedoTextureRotation3, bodyAlbedoTextureRotation4, bodyAlbedoTextureRotation5, bodyAlbedoTextureRotation6, bodyAlbedoTextureRotation7;
[Tooltip("Tint to celestial body's albedo texture, or just celestial body's color if no texture is selected. Perfect grey (127, 127, 127) specifies no tint.")]
public ColorParameter bodyAlbedoTint0, bodyAlbedoTint1, bodyAlbedoTint2,
  bodyAlbedoTint3, bodyAlbedoTint4, bodyAlbedoTint5, bodyAlbedoTint6, bodyAlbedoTint7;
[Tooltip("Whether or not celestial body is emissive.")]
public BoolParameter bodyEmissive0, bodyEmissive1, bodyEmissive2,
  bodyEmissive3, bodyEmissive4, bodyEmissive5, bodyEmissive6, bodyEmissive7;
[Tooltip("Light intensity of the celestial body, in lux. In particular this is the illuminance on the ground when the body is at the zenith position. A typical value for the sun is 120000, but this does not always integrate well with existing material workflows that are not physically-based.")]
public MinFloatParameter bodyLightIntensity0, bodyLightIntensity1, bodyLightIntensity2,
  bodyLightIntensity3, bodyLightIntensity4, bodyLightIntensity5, bodyLightIntensity6, bodyLightIntensity7;
[Tooltip("Specify color via celestial body temperature.")]
public BoolParameter bodyUseTemperature0, bodyUseTemperature1, bodyUseTemperature2,
  bodyUseTemperature3, bodyUseTemperature4, bodyUseTemperature5, bodyUseTemperature6, bodyUseTemperature7;
[Tooltip("Celestial body's light color, or if using in temperature mode, filter applied to chosen temperature (in this case an artistic override).")]
/* Display as "filter" in temperature mode. */
public ColorParameter bodyLightColor0, bodyLightColor1, bodyLightColor2,
  bodyLightColor3, bodyLightColor4, bodyLightColor5, bodyLightColor6, bodyLightColor7;
[Tooltip("Celestial body's temperature, used to set color in a physically-based way.")]
public ClampedFloatParameter bodyLightTemperature0, bodyLightTemperature1, bodyLightTemperature2,
  bodyLightTemperature3, bodyLightTemperature4, bodyLightTemperature5, bodyLightTemperature6, bodyLightTemperature7;
[Tooltip("Adjustable limb-darkening affect that darkens edges of celestial body. A physically-accurate value is 1, but higher values are often needed for the effect to be visible.")]
public MinFloatParameter bodyLimbDarkening0, bodyLimbDarkening1, bodyLimbDarkening2,
  bodyLimbDarkening3, bodyLimbDarkening4, bodyLimbDarkening5, bodyLimbDarkening6, bodyLimbDarkening7;
[Tooltip("Emission texture for celestial body. Will be multiplied by light intensity to get final displayed color.")]
public CubemapParameter bodyEmissionTexture0, bodyEmissionTexture1, bodyEmissionTexture2,
  bodyEmissionTexture3, bodyEmissionTexture4, bodyEmissionTexture5, bodyEmissionTexture6, bodyEmissionTexture7;
/* Displayed on null check of body albedo texture. */
[Tooltip("Rotation of celestial body's emission texture, specified by euler angles.")]
public Vector3Parameter bodyEmissionTextureRotation0, bodyEmissionTextureRotation1, bodyEmissionTextureRotation2,
  bodyEmissionTextureRotation3, bodyEmissionTextureRotation4, bodyEmissionTextureRotation5, bodyEmissionTextureRotation6, bodyEmissionTextureRotation7;
[Tooltip("Tint to celestial body. If an emission texture is present, will tint the texture, but otherwise will just tint the body's light color. A value of perfect white (255, 255, 255) specifies no tint.")]
public ColorParameter bodyEmissionTint0, bodyEmissionTint1, bodyEmissionTint2,
  bodyEmissionTint3, bodyEmissionTint4, bodyEmissionTint5, bodyEmissionTint6, bodyEmissionTint7;
[Tooltip("Multiplier on emissive color/texture. Often, emission textures will be too blown out if their actual physical light values are used. This is an artistic override to correct that.")]
public MinFloatParameter bodyEmissionMultiplier0, bodyEmissionMultiplier1, bodyEmissionMultiplier2,
  bodyEmissionMultiplier3, bodyEmissionMultiplier4, bodyEmissionMultiplier5, bodyEmissionMultiplier6, bodyEmissionMultiplier7;

/* Night Sky. */
[Tooltip("When checked, uses procedural sky. When unchecked, uses user-specified texture.")]
public BoolParameter useProceduralNightSky = new BoolParameter(true);
/* Procedural parameters.*/
[Tooltip("Quality of star texture.")]
public EnumParameter<ExpanseCommon.StarTextureQuality> starTextureQuality = new EnumParameter<ExpanseCommon.StarTextureQuality>(ExpanseCommon.StarTextureQuality.Medium);
[Tooltip("When checked, shows seed values for procedural star parameters. Tweaking random seeds can help you get the right flavor of randomness you want.")]
public BoolParameter showStarSeeds = new BoolParameter(false);
[Tooltip("Activates high density mode, which layers a second detail star texture on top of the primary one. Dense star fields are important for imparting a sense of realism in scenes with minimal light pollution, but can be too much for more stylized skies.")]
public BoolParameter useHighDensityMode = new BoolParameter(true);
[Tooltip("Density of stars.")]
public ClampedFloatParameter starDensity = new ClampedFloatParameter(0.25f, 0, 1);
[Tooltip("Seed for star density variation.")]
public Vector3Parameter starDensitySeed = new Vector3Parameter(new Vector3(3.473f, 5.253f, 0.532f));
[Tooltip("Range of random star sizes.")]
public FloatRangeParameter starSizeRange = new FloatRangeParameter(new Vector2(0.4f, 0.6f), 0.0001f, 1);
[Tooltip("Biases star sizes toward one end of the range. 0 is biased toward the minimum size. 1 is biased toward the maximum size.")]
public ClampedFloatParameter starSizeBias = new ClampedFloatParameter(0.5f, 0.0f, 1);
[Tooltip("Seed for star size variation.")]
public Vector3Parameter starSizeSeed = new Vector3Parameter(new Vector3(6.3234f, 1.253f, 0.3209f));
[Tooltip("Range of random star brightnesses.")]
public FloatRangeParameter starIntensityRange = new FloatRangeParameter(new Vector2(1, 6), 0.0001f, 100);
[Tooltip("Biases star intensity toward one end of the range. 0 is biased toward the minimum intensity. 1 is biased toward the maximum intensity.")]
public ClampedFloatParameter starIntensityBias = new ClampedFloatParameter(0.5f, 0.0f, 1);
[Tooltip("Seed for star brightness variation.")]
public Vector3Parameter starIntensitySeed = new Vector3Parameter(new Vector3(9.9532f, 7.7345f, 2.0532f));
[Tooltip("Range of random star temperatures, in Kelvin. The accuracy of the blackbody model diminishes for temperatures above 20000K, use at your own discretion.")]
public FloatRangeParameter starTemperatureRange = new FloatRangeParameter(new Vector2(2000, 20000), 1500, 30000);
[Tooltip("Biases star temperature toward one end of the range. 0 is biased toward the minimum temperature. 1 is biased toward the maximum temperature.")]
public ClampedFloatParameter starTemperatureBias = new ClampedFloatParameter(0.5f, 0.0f, 1);
[Tooltip("Seed for star temperature variation.")]
public Vector3Parameter starTemperatureSeed = new Vector3Parameter(new Vector3(0.2352f, 1.582f, 8.823f));
/* Nebulae parameters. */
[Tooltip("Whether to use a procedural nebulae texture, or a static pre-authored one.")]
public BoolParameter useProceduralNebulae = new BoolParameter(true);
[Tooltip("Quality of procedural nebulae texture.")]
public EnumParameter<ExpanseCommon.StarTextureQuality> nebulaeTextureQuality = new EnumParameter<ExpanseCommon.StarTextureQuality>(ExpanseCommon.StarTextureQuality.Medium);
[Tooltip("Nebulae texture.")]
public CubemapParameter nebulaeTexture = new CubemapParameter(null);
/* Regular parameters. */
[Tooltip("Color of light coming from the ground used for modeling light pollution.")]
public ColorParameter lightPollutionTint = new ColorParameter(new Color(255, 140, 66), hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Intensity of light scattered up from the ground used for modeling light pollution. Specified in lux.")]
public MinFloatParameter lightPollutionIntensity = new MinFloatParameter(10, 0);
[Tooltip("The night sky as a cubemap texture. If no texture is specified, the night sky tint color will be used.")]
public CubemapParameter nightSkyTexture = new CubemapParameter(null);
[Tooltip("The rotation of the night sky texture as euler angles.")]
public Vector3Parameter nightSkyRotation = new Vector3Parameter(new Vector3(0.0f, 0.0f, 0.0f));
[Tooltip("Tint to the night sky.")]
public MinFloatParameter nightSkyIntensity = new MinFloatParameter(0, 0);
[Tooltip("Tint to the night sky.")]
public ColorParameter nightSkyTint = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Expanse computes sky scattering using the average color of the sky texture. There are so many light sources in the night sky that this is really the only computationally tractable option. However, this usually results in scattering that's not nearly intense enough. This multiplier is an artistic override to mitigate that issue.")]
public MinFloatParameter nightSkyScatterIntensity = new MinFloatParameter(50, 0);
[Tooltip("An additional tint applied on top of the night sky tint, but only to the scattering. This is useful as an artistsic override for if the average color of your sky texture doesn't quite get you the scattering behavior you want. For instance, you may want the scattering to be bluer.")]
public ColorParameter nightSkyScatterTint = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
/* Star twinkle effect. */
[Tooltip("Whether or not to use star twinkle effect.")]
public BoolParameter useTwinkle = new BoolParameter(false);
[Tooltip("Threshold for night sky texture value for twinkle effect to be applied. Useful for avoiding noise-like artifactss on non-star features like nebulae.")]
public MinFloatParameter twinkleThreshold = new MinFloatParameter(0.001f, 0);
[Tooltip("Range of randomly generated twinkle frequencies. Higher values will make the stars twinkle faster. Lower values will make them twinkle slower. A value of zero will result in no twinkling at all.")]
public FloatRangeParameter twinkleFrequencyRange = new FloatRangeParameter(new Vector2(0.5f, 3), 0, 10);
[Tooltip("Bias to twinkle effect. Negative values increase the time when the star is not visible.")]
public FloatParameter twinkleBias = new FloatParameter(0);
[Tooltip("Intensity of more smooth twinkle effect.")]
public MinFloatParameter twinkleSmoothAmplitude = new MinFloatParameter(1, 0);
[Tooltip("Intensity of more chaotic twinkle effect.")]
public MinFloatParameter twinkleChaoticAmplitude = new MinFloatParameter(1, 0);

/* Aerial Perspective. */
[Tooltip("Expanse computes aerial perspective at 3 different LOD levels to reduce artifacts. This parameter controls the max distance used for each of the two closer LODs---the furthest LOD is unbounded. Typically a value of 1000-5000 is good for the closest LOD, and 10000-50000 for the furthest. If you are using a heavy fog layer and a lower texture quality setting, it may be necessary to tweak these values to avoid artifacts.")]
public FloatRangeParameter aerialPerspectiveTableDistances = new FloatRangeParameter(new Vector2(8000, 15000), 0, 100000);
[Tooltip("This parameter controls how aggressively aerial perspective due to Rayleigh and Isotropic (\"uniform\") layers is attenuated as a consequence of approximate volumetric shadowing. To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionPowerUniform = new MinFloatParameter(0.5f, 0);
[Tooltip("This parameter is a way of offsetting the attenuation of aerial perspective as a consequence of approximate volumetric shadowing (for Rayleigh and Isotropic (\"uniform\") layers). To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionBiasUniform = new MinFloatParameter(0.25f, 0);
[Tooltip("This parameter controls how aggressively aerial perspective due to Mie (\"directional\") layers is attenuated as a consequence of approximate volumetric shadowing. To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionPowerDirectional = new MinFloatParameter(1, 0);
[Tooltip("This parameter is a way of offsetting the attenuation of aerial perspective as a consequence of approximate volumetric shadowing (for Mie (\"directional\") layers). To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionBiasDirectional = new MinFloatParameter(0.02f, 0);

/* Quality. */
[Tooltip("Quality of sky texture.")]
public EnumParameter<ExpanseCommon.SkyTextureQuality> skyTextureQuality = new EnumParameter<ExpanseCommon.SkyTextureQuality>(ExpanseCommon.SkyTextureQuality.Medium);
[Tooltip("The number of samples used when computing transmittance lookup tables. With importance sampling turned on, a value of as low as 10 gives near-perfect results on the ground. A value as low as 4 is ok if some visible inaccuracy is tolerable. Without importantance sampling, a value of 32 or higher is recommended.")]
public ClampedIntParameter numberOfTransmittanceSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("The number of samples used when computing light pollution. With importance sampling turned on, a value of as low as 10 gives near-perfect results on the ground. A value as low as 8 is ok if some visible inaccuracy is tolerable. Without importantance sampling, a value of 64 or higher is recommended.")]
public ClampedIntParameter numberOfLightPollutionSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("The number of samples used when computing single scattering. With importance sampling turned on, a value of as low as 10 gives near-perfect results on the ground. A value as low as 5 is ok if some visible inaccuracy is tolerable. Without importantance sampling, a value of 32 or higher is recommended.")]
public ClampedIntParameter numberOfSingleScatteringSamples = new ClampedIntParameter(32, 1, 256);
[Tooltip("The number of samples used when sampling the ground irradiance. Importance sampling does not apply here. To get a near-perfect result, around 10 samples is necessary. But it is a fairly subtle effect, so as low as 6 samples gives a decent result.")]
public ClampedIntParameter numberOfGroundIrradianceSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("The number of samples to use when computing the initial isotropic estimate of multiple scattering. Importance sampling does not apply here. To get a near-perfect result, around 15 samples is necessary. But it is a fairly subtle effect, so as low as 6 samples gives a decent result.")]
public ClampedIntParameter numberOfMultipleScatteringSamples = new ClampedIntParameter(32, 1, 256);
[Tooltip("The number of samples to use when computing the actual accumulated estimate of multiple scattering from the isotropic estimate. The number of samples to use when computing the initial isotropic estimate of multiple scattering. With importance sample, 8 samples gives a near-perfect result. However, multiple scattering is a fairly subtle effect, so as low as 3 samples gives a decent result. Without importance sampling, a value of 32 or higher is necessary for near perfect results, but a value of 4 is sufficient for most needs.")]
public ClampedIntParameter numberOfMultipleScatteringAccumulationSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("Whether or not to use importance sampling. Importance sampling is a sample distribution strategy that increases fidelity given a limited budget of samples. It is recommended to turn it on, as it doesn't decrease fidelity, but does allow for fewer samples to be taken, boosting performance. However, for outer-space perspectives, it can sometimes introduce inaccuracies, so it can be useful to increase sample counts and turn off importance sampling in those cases.")]
public BoolParameter useImportanceSampling = new BoolParameter(false);
[Tooltip("Whether or not to use MSAA 8x anti-aliasing. This does negatively affect performance.")]
public BoolParameter useAntiAliasing = new BoolParameter(false);
[Tooltip("Amount of dithering used to reduce color banding. If this is too high, noise will be visible.")]
public ClampedFloatParameter ditherAmount = new ClampedFloatParameter(0.05f, 0.0f, 1.0f);

/***********************/
/******* Clouds ********/
/***********************/
/* Lighting. TODO */

/* Noise generation. TODO */

/* Movement---sampling offsets primarily. TODO */

/* Geometry. TODO */

/* Sampling. TODO */
/* TODO: debug goes here. */

/******************************************************************************/
/************************** END SERIALIZED PARAMETERS *************************/
/******************************************************************************/



/* Constructor to initialize defaults for array parameters. */
public Expanse() : base() {
  /* TODO: how can we initialize layers to be earthlike by default and still
   * have the code compact? The answer is to have a volume profile bundled
   * in with the download that has all the defaults set up. */
  /* Atmosphere layer initialization. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    /* Enable only the first layer by default. */
    this.GetType().GetField("layerEnabled" + i).SetValue(this, new BoolParameter(i==0));
    this.GetType().GetField("layerCoefficientsA" + i).SetValue(this, new Vector3Parameter(new Vector3(0.0000058f, 0.0000135f, 0.0000331f)));
    this.GetType().GetField("layerCoefficientsS" + i).SetValue(this, new Vector3Parameter(new Vector3(0.0000058f, 0.0000135f, 0.0000331f)));
    this.GetType().GetField("layerDensityDistribution" + i).SetValue(this, new EnumParameter<ExpanseCommon.DensityDistribution>(ExpanseCommon.DensityDistribution.Exponential));
    this.GetType().GetField("layerHeight" + i).SetValue(this, new MinFloatParameter(25000, 1));
    this.GetType().GetField("layerThickness" + i).SetValue(this, new MinFloatParameter(8000, 1));
    this.GetType().GetField("layerPhaseFunction" + i).SetValue(this, new EnumParameter<ExpanseCommon.PhaseFunction>(ExpanseCommon.PhaseFunction.Rayleigh));
    this.GetType().GetField("layerAnisotropy" + i).SetValue(this, new ClampedFloatParameter(0.7f, -1.0f, 1.0f));
    this.GetType().GetField("layerDensity" + i).SetValue(this, new MinFloatParameter(1, 0));
    this.GetType().GetField("layerUseDensityAttenuation" + i).SetValue(this, new BoolParameter(false));
    this.GetType().GetField("layerAttenuationDistance" + i).SetValue(this, new MinFloatParameter(1000, 1));
    this.GetType().GetField("layerAttenuationBias" + i).SetValue(this, new MinFloatParameter(0, 0));
    this.GetType().GetField("layerTint" + i).SetValue(this, new ColorParameter(Color.grey, hdr: false, showAlpha: false, showEyeDropper: true));
    this.GetType().GetField("layerMultipleScatteringMultiplier" + i).SetValue(this, new MinFloatParameter(1.0f, 0.0f));
  }

  /* Celestial body initialization. */
  for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
    /* Enable only the first celestial body by default. */
    this.GetType().GetField("bodyEnabled" + i).SetValue(this, new BoolParameter(i==0));
    this.GetType().GetField("bodyDirection" + i).SetValue(this, new Vector3Parameter(new Vector3(0, 0, 0)));
    this.GetType().GetField("bodyAngularRadius" + i).SetValue(this, new ClampedFloatParameter(0.5f, 0.001f, 90));
    this.GetType().GetField("bodyDistance" + i).SetValue(this, new MinFloatParameter(1.5e8f, 0));
    this.GetType().GetField("bodyReceivesLight" + i).SetValue(this, new BoolParameter(false));
    this.GetType().GetField("bodyAlbedoTexture" + i).SetValue(this, new CubemapParameter(null));
    this.GetType().GetField("bodyAlbedoTextureRotation" + i).SetValue(this, new Vector3Parameter(new Vector3(0, 0, 0)));
    this.GetType().GetField("bodyAlbedoTint" + i).SetValue(this, new ColorParameter(Color.grey, hdr: false, showAlpha: false, showEyeDropper: true));
    this.GetType().GetField("bodyEmissive" + i).SetValue(this, new BoolParameter(true));
    this.GetType().GetField("bodyUseTemperature" + i).SetValue(this, new BoolParameter(false));
    this.GetType().GetField("bodyLightIntensity" + i).SetValue(this, new MinFloatParameter(150000, 0));
    this.GetType().GetField("bodyLightColor" + i).SetValue(this, new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true));
    this.GetType().GetField("bodyLightTemperature" + i).SetValue(this, new ClampedFloatParameter(5778, 1000, 20000));
    this.GetType().GetField("bodyLimbDarkening" + i).SetValue(this, new MinFloatParameter(1, 0));
    this.GetType().GetField("bodyEmissionTexture" + i).SetValue(this, new CubemapParameter(null));
    this.GetType().GetField("bodyEmissionTextureRotation" + i).SetValue(this, new Vector3Parameter(new Vector3(0, 0, 0)));
    this.GetType().GetField("bodyEmissionTint" + i).SetValue(this, new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true));
    this.GetType().GetField("bodyEmissionMultiplier" + i).SetValue(this, new MinFloatParameter(1, 0));
  }
}

public override Type GetSkyRendererType() {
  return typeof(ExpanseRenderer);
}

public override int GetHashCode() {
  int hash = base.GetHashCode();
  unchecked {
  /***********************/
  /********* Sky *********/
  /***********************/
  /* Planet parameters. */
    hash = hash * 23 + atmosphereThickness.value.GetHashCode();
    hash = hash * 23 + planetRadius.value.GetHashCode();
    hash = groundAlbedoTexture.value != null ? hash * 23 + groundAlbedoTexture.value.GetHashCode() : hash;
    hash = hash * 23 + groundTint.value.GetHashCode();
    hash = groundEmissionTexture.value != null ? hash * 23 + groundEmissionTexture.value.GetHashCode() : hash;
    hash = hash * 23 + groundEmissionMultiplier.value.GetHashCode();
    hash = hash * 23 + planetRotation.value.GetHashCode();

    /* Atmosphere layers. */
    for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("layerEnabled" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("layerCoefficientsA" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("layerCoefficientsS" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((EnumParameter<ExpanseCommon.DensityDistribution>) this.GetType().GetField("layerDensityDistribution" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerHeight" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerThickness" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((EnumParameter<ExpanseCommon.PhaseFunction>) this.GetType().GetField("layerPhaseFunction" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ClampedFloatParameter) this.GetType().GetField("layerAnisotropy" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerDensity" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("layerUseDensityAttenuation" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationDistance" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationBias" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("layerTint" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerMultipleScatteringMultiplier" + i).GetValue(this)).value.GetHashCode();
    }

    /* Celestial bodies. */
    for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("bodyEnabled" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("bodyDirection" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ClampedFloatParameter) this.GetType().GetField("bodyAngularRadius" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("bodyDistance" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("bodyReceivesLight" + i).GetValue(this)).value.GetHashCode();
      var albTex = (CubemapParameter) this.GetType().GetField("bodyAlbedoTexture" + i).GetValue(this);
      hash = albTex.value != null ? hash * 23 + albTex.value.GetHashCode() : hash;
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("bodyAlbedoTextureRotation" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("bodyAlbedoTint" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("bodyEmissive" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("bodyUseTemperature" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("bodyLightIntensity" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("bodyLightColor" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ClampedFloatParameter) this.GetType().GetField("bodyLightTemperature" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("bodyLimbDarkening" + i).GetValue(this)).value.GetHashCode();
      var emTex = (CubemapParameter) this.GetType().GetField("bodyEmissionTexture" + i).GetValue(this);
      hash = emTex.value != null ? hash * 23 + emTex.value.GetHashCode() : hash;
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("bodyEmissionTextureRotation" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("bodyEmissionTint" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("bodyEmissionMultiplier" + i).GetValue(this)).value.GetHashCode();
    }

    /* Night Sky. */
    /* Procedural. */
    hash = hash * 23 + useProceduralNightSky.value.GetHashCode();
    hash = hash * 23 + starTextureQuality.value.GetHashCode();
    hash = hash * 23 + showStarSeeds.value.GetHashCode();
    hash = hash * 23 + useHighDensityMode.value.GetHashCode();
    hash = hash * 23 + starDensity.value.GetHashCode();
    hash = hash * 23 + starDensitySeed.value.GetHashCode();
    hash = hash * 23 + starSizeRange.value.GetHashCode();
    hash = hash * 23 + starSizeBias.value.GetHashCode();
    hash = hash * 23 + starSizeSeed.value.GetHashCode();
    hash = hash * 23 + starIntensityRange.value.GetHashCode();
    hash = hash * 23 + starIntensityBias.value.GetHashCode();
    hash = hash * 23 + starIntensitySeed.value.GetHashCode();
    hash = hash * 23 + starTemperatureRange.value.GetHashCode();
    hash = hash * 23 + starTemperatureBias.value.GetHashCode();
    hash = hash * 23 + useProceduralNebulae.value.GetHashCode();
    hash = hash * 23 + nebulaeTextureQuality.value.GetHashCode();
    hash = nebulaeTexture.value != null ? hash * 23 + nebulaeTexture.value.GetHashCode() : hash;

    /* Texture. */
    hash = hash * 23 + lightPollutionTint.value.GetHashCode();
    hash = hash * 23 + lightPollutionIntensity.value.GetHashCode();
    hash = nightSkyTexture.value != null ? hash * 23 + nightSkyTexture.value.GetHashCode() : hash;
    hash = hash * 23 + nightSkyRotation.value.GetHashCode();
    hash = hash * 23 + nightSkyTint.value.GetHashCode();
    hash = hash * 23 + nightSkyIntensity.value.GetHashCode();
    hash = hash * 23 + nightSkyScatterTint.value.GetHashCode();
    hash = hash * 23 + nightSkyScatterIntensity.value.GetHashCode();

    /* Aerial Perspective. */
    hash = hash * 23 + aerialPerspectiveTableDistances.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionPowerUniform.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionBiasUniform.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionPowerDirectional.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionBiasDirectional.value.GetHashCode();

    /* Quality. */
    hash = hash * 23 + skyTextureQuality.value.GetHashCode();
    hash = hash * 23 + numberOfTransmittanceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfLightPollutionSamples.value.GetHashCode();
    hash = hash * 23 + numberOfSingleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfGroundIrradianceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringAccumulationSamples.value.GetHashCode();
    hash = hash * 23 + useImportanceSampling.value.GetHashCode();
    hash = hash * 23 + useAntiAliasing.value.GetHashCode();
    hash = hash * 23 + ditherAmount.value.GetHashCode();

  }
  return hash;
}

public int GetSkyHashCode() {
  int hash = base.GetHashCode();
  unchecked {
  /***********************/
  /********* Sky *********/
  /***********************/
  /* Planet parameters. */
    hash = hash * 23 + atmosphereThickness.value.GetHashCode();
    hash = hash * 23 + planetRadius.value.GetHashCode();
    hash = groundAlbedoTexture.value != null ? hash * 23 + groundAlbedoTexture.value.GetHashCode() : hash;
    hash = hash * 23 + groundTint.value.GetHashCode();
    hash = groundEmissionTexture.value != null ? hash * 23 + groundEmissionTexture.value.GetHashCode() : hash;
    hash = hash * 23 + groundEmissionMultiplier.value.GetHashCode();
    hash = hash * 23 + planetRotation.value.GetHashCode();

    /* Atmosphere layers. */
    for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("layerEnabled" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("layerCoefficientsA" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("layerCoefficientsS" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((EnumParameter<ExpanseCommon.DensityDistribution>) this.GetType().GetField("layerDensityDistribution" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerHeight" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerThickness" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((EnumParameter<ExpanseCommon.PhaseFunction>) this.GetType().GetField("layerPhaseFunction" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ClampedFloatParameter) this.GetType().GetField("layerAnisotropy" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerDensity" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("layerUseDensityAttenuation" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationDistance" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationBias" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("layerTint" + i).GetValue(this)).value.GetHashCode();
    }

    /* Night Sky. TODO */

    /* Aerial Perspective. */
    hash = hash * 23 + aerialPerspectiveTableDistances.value.GetHashCode();

    /* Quality. */
    hash = hash * 23 + skyTextureQuality.value.GetHashCode();
    hash = hash * 23 + numberOfTransmittanceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfLightPollutionSamples.value.GetHashCode();
    hash = hash * 23 + numberOfSingleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfGroundIrradianceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringAccumulationSamples.value.GetHashCode();
    hash = hash * 23 + useImportanceSampling.value.GetHashCode();
  }
  return hash;
}

public int GetCloudHashCode() {
  /* TODO */
  int hash = base.GetHashCode();
  unchecked {

  }
  return hash;
}

public int GetNightSkyHashCode() {
  /* Used for checking if a recomputation of the night sky texture and
   * average color needs to take place. */
  /* TODO */
  int hash = base.GetHashCode();
  unchecked {
    hash = hash * 23 + useProceduralNightSky.value.GetHashCode();
    if (useProceduralNightSky.value) {
      /* TODO: procedural params in here. */
      hash = hash * 23 + starTextureQuality.value.GetHashCode();
      hash = hash * 23 + showStarSeeds.value.GetHashCode();
      hash = hash * 23 + useHighDensityMode.value.GetHashCode();
      hash = hash * 23 + starDensity.value.GetHashCode();
      hash = hash * 23 + starDensitySeed.value.GetHashCode();
      hash = hash * 23 + starSizeRange.value.GetHashCode();
      hash = hash * 23 + starSizeBias.value.GetHashCode();
      hash = hash * 23 + starSizeSeed.value.GetHashCode();
      hash = hash * 23 + starIntensityRange.value.GetHashCode();
      hash = hash * 23 + starIntensityBias.value.GetHashCode();
      hash = hash * 23 + starIntensitySeed.value.GetHashCode();
      hash = hash * 23 + starTemperatureRange.value.GetHashCode();
      hash = hash * 23 + starTemperatureBias.value.GetHashCode();
      hash = hash * 23 + starTemperatureSeed.value.GetHashCode();
      hash = hash * 23 + useProceduralNebulae.value.GetHashCode();
      hash = hash * 23 + nebulaeTextureQuality.value.GetHashCode();
      hash = nebulaeTexture.value != null ? hash * 23 + nebulaeTexture.value.GetHashCode() : hash;
    }
    hash = nightSkyTexture.value != null ? hash * 23 + nightSkyTexture.value.GetHashCode() : hash;
  }
  return hash;
}

}
