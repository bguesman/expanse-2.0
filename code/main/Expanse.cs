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
[Tooltip("The planet origin, in meters, but specified as an offset from the position (0, -radius, 0), since that origin is much more convenient.")]
public Vector3Parameter planetOriginOffset = new Vector3Parameter(new Vector3(0, 0, 0));
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
[Tooltip("Whether to use the camera position as the origin for density attenuation, as opposed to specific point in world space.")]
public BoolParameter layerDensityAttenuationPlayerOrigin0, layerDensityAttenuationPlayerOrigin1, layerDensityAttenuationPlayerOrigin2,
  layerDensityAttenuationPlayerOrigin3, layerDensityAttenuationPlayerOrigin4, layerDensityAttenuationPlayerOrigin5, layerDensityAttenuationPlayerOrigin6, layerDensityAttenuationPlayerOrigin7;
[Tooltip("World space position to use as origing for density attenuation. Density will attenuate with distance away from this point.")]
public Vector3Parameter layerDensityAttenuationOrigin0, layerDensityAttenuationOrigin1, layerDensityAttenuationOrigin2,
  layerDensityAttenuationOrigin3, layerDensityAttenuationOrigin4, layerDensityAttenuationOrigin5, layerDensityAttenuationOrigin6, layerDensityAttenuationOrigin7;
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
[Tooltip("Whether or not to use date/time mode to control this body's direction.")]
public BoolParameter bodyUseDateTime0, bodyUseDateTime1, bodyUseDateTime2,
  bodyUseDateTime3, bodyUseDateTime4, bodyUseDateTime5, bodyUseDateTime6, bodyUseDateTime7;
[Tooltip("Celestial body's direction.")]
public Vector3Parameter bodyDirection0, bodyDirection1, bodyDirection2,
  bodyDirection3, bodyDirection4, bodyDirection5, bodyDirection6, bodyDirection7;
[Tooltip("Date and time for this celestial body, in universal time (UTC), used to compute direction. Only accurate from 1 March 1900 to 28 February 2100.")]
public DateTimeParameter bodyDateTime0, bodyDateTime1, bodyDateTime2,
  bodyDateTime3, bodyDateTime4, bodyDateTime5, bodyDateTime6, bodyDateTime7;
[Tooltip("Latitude (first) and longitude (second) of the player, in degrees. Used for calculating this body's position from the date and time.")]
public Vector2Parameter bodyPlayerLatitudeLongitude0, bodyPlayerLatitudeLongitude1, bodyPlayerLatitudeLongitude2,
  bodyPlayerLatitudeLongitude3, bodyPlayerLatitudeLongitude4, bodyPlayerLatitudeLongitude5, bodyPlayerLatitudeLongitude6, bodyPlayerLatitudeLongitude7;
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
[Tooltip("Tint to star texture.")]
public ColorParameter starTint = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);

/* Nebulae parameters. */
[Tooltip("Whether to use a procedural nebulae texture, or a static pre-authored one.")]
public BoolParameter useProceduralNebulae = new BoolParameter(true);

/* Procedural nebulae. */
[Tooltip("When checked, shows seed values for procedural nebulae parameters. Tweaking random seeds can help you get the right flavor of randomness you want.")]
public BoolParameter showNebulaeSeeds = new BoolParameter(false);
[Tooltip("Quality of procedural nebulae texture.")]
public EnumParameter<ExpanseCommon.StarTextureQuality> nebulaeTextureQuality = new EnumParameter<ExpanseCommon.StarTextureQuality>(ExpanseCommon.StarTextureQuality.Medium);
[Tooltip("Global definition control for the whole nebula texture. This increases saturation and contrast. It's useful to use in tandem with the global intensity control.")]
public MinFloatParameter nebulaOverallDefinition = new MinFloatParameter(1, 0);
[Tooltip("Global intensity control for the whole nebula texture.")]
public MinFloatParameter nebulaOverallIntensity = new MinFloatParameter(1, 0);
[Tooltip("Scale of noise used for determining nebula coverage. If this value is high, there will be lots of little nebulae scattered across the sky. If this value is low, there will be a few huge nebulae.")]
public MinFloatParameter nebulaCoverageScale = new MinFloatParameter(3, 1);
[Tooltip("The seed for the nebula coverage texture.")]
public Vector3Parameter nebulaCoverageSeed = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));

[Tooltip("Intensity of nebula haze.")]
public MinFloatParameter nebulaHazeBrightness = new MinFloatParameter(1, 0);
[Tooltip("Color of nebula haze.")]
public ColorParameter nebulaHazeColor = new ColorParameter(Color.red, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Scale of base octave of noise used for nebula haze. Smaller values give bigger more global features, larger values give smaller more detailed features.")]
public MinFloatParameter nebulaHazeScale = new MinFloatParameter(5, 5);
[Tooltip("Scale multiplier applied to additional octaves of noise used for nebula haze. As an example, if this value is 2, each octave will be twice as small as the last octave.")]
public ClampedFloatParameter nebulaHazeScaleFactor = new ClampedFloatParameter(2, 0.01f, 4.0f);
[Tooltip("Intensity multiplier applied to additional octaves of noise used for nebula haze. As an example, if this value is 0.5, each octave will be half as intense as the last octave.")]
public ClampedFloatParameter nebulaHazeDetailBalance = new ClampedFloatParameter(0.5f, 0.01f, 4.0f);
[Tooltip("Number of noise octaves to use to generate nebula haze. Increasing the number of octaves can dim the overall noise texture, so it is useful to adjust the intensity control in tandem with this parameter.")]
public ClampedIntParameter nebulaHazeOctaves = new ClampedIntParameter(5, 1, 8);
[Tooltip("Bias of zero value for nebula haze.")]
public ClampedFloatParameter nebulaHazeBias = new ClampedFloatParameter(0, -1, 1);
[Tooltip("Spread of the nebula haze. This parameter allows the nebula to bleed across the coverage boundary, and is useful for softening edges.")]
public MinFloatParameter nebulaHazeSpread = new MinFloatParameter(1, 0);
[Tooltip("Coverage of the nebula haze. A higher value will result in less nebula coverage. A lower value will result in more nebula coverage.")]
public ClampedFloatParameter nebulaHazeCoverage = new ClampedFloatParameter(0.5f, 0, 1);
[Tooltip("Strength of the nebula haze. This is meant to be used in tandem with the coverage value. Higher strength values will allow more features to push through the coverage map. The best way to see what this parameter does is to play around with it.")]
public MinFloatParameter nebulaHazeStrength = new MinFloatParameter(1, 0);
[Tooltip("The x seed for the nebula haze texture.")]
public Vector3Parameter nebulaHazeSeedX = new Vector3Parameter(new Vector3(1.81f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula haze texture.")]
public Vector3Parameter nebulaHazeSeedY = new Vector3Parameter(new Vector3(0.5932f, 0.95382f, 6.32532f));
[Tooltip("The z seed for the nebula haze texture.")]
public Vector3Parameter nebulaHazeSeedZ = new Vector3Parameter(new Vector3(1.777723f, 1.05320f, 4.7983f));

[Tooltip("Intensity of nebula clouds.")]
public MinFloatParameter nebulaCloudBrightness = new MinFloatParameter(1, 0);
[Tooltip("Color of nebula clouds.")]
public ColorParameter nebulaCloudColor = new ColorParameter(Color.green, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Scale of base octave of noise used for nebula clouds. Smaller values give bigger more global features, larger values give smaller more detailed features.")]
public MinFloatParameter nebulaCloudScale = new MinFloatParameter(5, 5);
[Tooltip("Scale multiplier applied to additional octaves of noise used for nebula clouds. As an example, if this value is 2, each octave will be twice as small as the last octave.")]
public ClampedFloatParameter nebulaCloudScaleFactor = new ClampedFloatParameter(2, 0.01f, 4.0f);
[Tooltip("Intensity multiplier applied to additional octaves of noise used for nebula clouds. As an example, if this value is 0.5, each octave will be half as intense as the last octave.")]
public ClampedFloatParameter nebulaCloudDetailBalance = new ClampedFloatParameter(0.5f, 0.01f, 4.0f);
[Tooltip("Number of noise octaves to use to generate nebula clouds. Increasing the number of octaves can dim the overall noise texture, so it is useful to adjust the intensity control in tandem with this parameter.")]
public ClampedIntParameter nebulaCloudOctaves = new ClampedIntParameter(5, 1, 8);
[Tooltip("Bias of zero value for nebula clouds.")]
public ClampedFloatParameter nebulaCloudBias = new ClampedFloatParameter(0, -1, 1);
[Tooltip("Spread of the nebula clouds. This parameter allows the nebula to bleed across the coverage boundary, and is useful for softening edges.")]
public MinFloatParameter nebulaCloudSpread = new MinFloatParameter(1, 0);
[Tooltip("Coverage of the nebula clouds. A higher value will result in less nebula coverage. A lower value will result in more nebula coverage.")]
public ClampedFloatParameter nebulaCloudCoverage = new ClampedFloatParameter(0.5f, 0, 1);
[Tooltip("Strength of the nebula clouds. This is meant to be used in tandem with the coverage value. Higher strength values will allow more features to push through the coverage map. The best way to see what this parameter does is to play around with it.")]
public MinFloatParameter nebulaCloudStrength = new MinFloatParameter(1, 0);
[Tooltip("The x seed for the nebula cloud texture.")]
public Vector3Parameter nebulaCloudSeedX = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula cloud texture.")]
public Vector3Parameter nebulaCloudSeedY = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The z seed for the nebula cloud texture.")]
public Vector3Parameter nebulaCloudSeedZ = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));

[Tooltip("Intensity of nebula big strands.")]
public MinFloatParameter nebulaCoarseStrandBrightness = new MinFloatParameter(1, 0);
[Tooltip("Color of nebula big strands.")]
public ColorParameter nebulaCoarseStrandColor = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Scale of base octave of noise used for nebula big strands. Smaller values give bigger more global features, larger values give smaller more detailed features.")]
public MinFloatParameter nebulaCoarseStrandScale = new MinFloatParameter(5, 5);
[Tooltip("Scale multiplier applied to additional octaves of noise used for nebula big strands. As an example, if this value is 2, each octave will be twice as small as the last octave.")]
public ClampedFloatParameter nebulaCoarseStrandScaleFactor = new ClampedFloatParameter(2, 0.01f, 4.0f);
[Tooltip("Intensity multiplier applied to additional octaves of noise used for nebula big strands. As an example, if this value is 0.5, each octave will be half as intense as the last octave.")]
public ClampedFloatParameter nebulaCoarseStrandDetailBalance = new ClampedFloatParameter(0.5f, 0.01f, 4.0f);
[Tooltip("Number of noise octaves to use to generate nebula big strands. Increasing the number of octaves can dim the overall noise texture, so it is useful to adjust the intensity control in tandem with this parameter.")]
public ClampedIntParameter nebulaCoarseStrandOctaves = new ClampedIntParameter(5, 1, 8);
[Tooltip("Bias of zero value for nebula big strands.")]
public ClampedFloatParameter nebulaCoarseStrandBias = new ClampedFloatParameter(0, -1, 1);
[Tooltip("Definition of nebula big strands. This is useful for making the strands look more like long striations, as opposed to softer cells. Increasing the definition usually requires also increasing the strength parameter to ensure that the strands can still get through the coverage map.")]
public MinFloatParameter nebulaCoarseStrandDefinition = new MinFloatParameter(1, 0);
[Tooltip("Spread of the nebula big strands. This parameter allows the nebula to bleed across the coverage boundary, and is useful for softening edges.")]
public MinFloatParameter nebulaCoarseStrandSpread = new MinFloatParameter(1, 0);
[Tooltip("Coverage of the nebula big strands. A higher value will result in less nebula coverage. A lower value will result in more nebula coverage.")]
public ClampedFloatParameter nebulaCoarseStrandCoverage = new ClampedFloatParameter(0.5f, 0, 1);
[Tooltip("Strength of the nebula big strands. This is meant to be used in tandem with the coverage value. Higher strength values will allow more features to push through the coverage map. The best way to see what this parameter does is to play around with it.")]
public MinFloatParameter nebulaCoarseStrandStrength = new MinFloatParameter(1, 0);
[Tooltip("Scale of the noise used to warp the big strands. A higher value gives smaller vortices and tendrils. A lower value gives bigger swirls and arcs.")]
public MinFloatParameter nebulaCoarseStrandWarpScale = new MinFloatParameter(16, 0);
[Tooltip("Intensity of warping of the big strands. Nebulae are big bodies of interstellar gas, and so they obey the laws of fluid mechanics. It's important to capture some of the resulting swirly fluid features. This warp value helps to do that.")]
public ClampedFloatParameter nebulaCoarseStrandWarp = new ClampedFloatParameter(0.003f, 0, 1);
[Tooltip("The x seed for the nebula big strand texture.")]
public Vector3Parameter nebulaCoarseStrandSeedX = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula big strand texture.")]
public Vector3Parameter nebulaCoarseStrandSeedY = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The z seed for the nebula big strand texture.")]
public Vector3Parameter nebulaCoarseStrandSeedZ = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The x seed for the nebula big strand warp texture.")]
public Vector3Parameter nebulaCoarseStrandWarpSeedX = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula big strand warp texture.")]
public Vector3Parameter nebulaCoarseStrandWarpSeedY = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The z seed for the nebula big strand warp texture.")]
public Vector3Parameter nebulaCoarseStrandWarpSeedZ = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));

[Tooltip("Intensity of nebula small strands.")]
public MinFloatParameter nebulaFineStrandBrightness = new MinFloatParameter(1, 0);
[Tooltip("Color of nebula small strands.")]
public ColorParameter nebulaFineStrandColor = new ColorParameter(Color.blue, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Scale of base octave of noise used for nebula small strands. Smaller values give bigger more global features, larger values give smaller more detailed features.")]
public MinFloatParameter nebulaFineStrandScale = new MinFloatParameter(5, 5);
[Tooltip("Scale multiplier applied to additional octaves of noise used for nebula small strands. As an example, if this value is 2, each octave will be twice as small as the last octave.")]
public ClampedFloatParameter nebulaFineStrandScaleFactor = new ClampedFloatParameter(2, 0.01f, 4.0f);
[Tooltip("Intensity multiplier applied to additional octaves of noise used for nebula small strands. As an example, if this value is 0.5, each octave will be half as intense as the last octave.")]
public ClampedFloatParameter nebulaFineStrandDetailBalance = new ClampedFloatParameter(0.5f, 0.01f, 4.0f);
[Tooltip("Number of noise octaves to use to generate nebula small strands. Increasing the number of octaves can dim the overall noise texture, so it is useful to adjust the intensity control in tandem with this parameter.")]
public ClampedIntParameter nebulaFineStrandOctaves = new ClampedIntParameter(5, 1, 8);
[Tooltip("Bias of zero value for nebula small strands.")]
public ClampedFloatParameter nebulaFineStrandBias = new ClampedFloatParameter(0, -1, 1);
[Tooltip("Definition of nebula small strands. This is useful for making the strands look more like long striations, as opposed to softer cells. Increasing the definition usually requires also increasing the strength parameter to ensure that the strands can still get through the coverage map.")]
public MinFloatParameter nebulaFineStrandDefinition = new MinFloatParameter(1, 0);
[Tooltip("Spread of the nebula small strands. This parameter allows the nebula to bleed across the coverage boundary, and is useful for softening edges.")]
public MinFloatParameter nebulaFineStrandSpread = new MinFloatParameter(1, 0);
[Tooltip("Coverage of the nebula small strands. A higher value will result in less nebula coverage. A lower value will result in more nebula coverage.")]
public ClampedFloatParameter nebulaFineStrandCoverage = new ClampedFloatParameter(0.5f, 0, 1);
[Tooltip("Strength of the nebula small strands. This is meant to be used in tandem with the coverage value. Higher strength values will allow more features to push through the coverage map. The best way to see what this parameter does is to play around with it.")]
public MinFloatParameter nebulaFineStrandStrength = new MinFloatParameter(1, 0);
[Tooltip("Scale of the noise used to warp the small strands. A higher value gives smaller vortices and tendrils. A lower value gives bigger swirls and arcs.")]
public MinFloatParameter nebulaFineStrandWarpScale = new MinFloatParameter(16, 0);
[Tooltip("Intensity of warping of the small strands. Nebulae are big bodies of interstellar gas, and so they obey the laws of fluid mechanics. It's important to capture some of the resulting swirly fluid features. This warp value helps to do that.")]
public ClampedFloatParameter nebulaFineStrandWarp = new ClampedFloatParameter(0.003f, 0, 1);
[Tooltip("The x seed for the nebula small strand texture.")]
public Vector3Parameter nebulaFineStrandSeedX = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula small strand texture.")]
public Vector3Parameter nebulaFineStrandSeedY = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The z seed for the nebula small strand texture.")]
public Vector3Parameter nebulaFineStrandSeedZ = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The x seed for the nebula small strand warp texture.")]
public Vector3Parameter nebulaFineStrandWarpSeedX = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula small strand warp texture.")]
public Vector3Parameter nebulaFineStrandWarpSeedY = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The z seed for the nebula small strand warp texture.")]
public Vector3Parameter nebulaFineStrandWarpSeedZ = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));

[Tooltip("Range of transmittance values the nebula can have.")]
public FloatRangeParameter nebulaTransmittanceRange = new FloatRangeParameter(new Vector2(0, 1), 0, 1);
[Tooltip("Scale of noise used to randomize nebula transmittance.")]
public MinFloatParameter nebulaTransmittanceScale = new MinFloatParameter(5, 5);
[Tooltip("The x seed for the nebula transmittance texture.")]
public Vector3Parameter nebulaTransmittanceSeedX = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The y seed for the nebula transmittance texture.")]
public Vector3Parameter nebulaTransmittanceSeedY = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));
[Tooltip("The z seed for the nebula transmittance texture.")]
public Vector3Parameter nebulaTransmittanceSeedZ = new Vector3Parameter(new Vector3(0.19235f, 1.2359f, 3.993583f));

[Tooltip("Amount that the star density follows the nebula texture.")]
public MinFloatParameter starNebulaFollowAmount = new MinFloatParameter(0, 0);
[Tooltip("How strictly to have the star density follow the nebula density. At higher values, the star density change is very rapid across the nebula boundary. At lower values, the star density change is gradual from the center of the nebula to empty space.")]
public MinFloatParameter starNebulaFollowSpread = new MinFloatParameter(2, 0);

/* Regular nebulae. */
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
[Tooltip("Intensity of the night sky.")]
public MinFloatParameter nightSkyIntensity = new MinFloatParameter(0, 0);
[Tooltip("Tint to the night sky.")]
public ColorParameter nightSkyTint = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Expanse computes sky scattering using the average color of the sky texture. There are so many light sources in the night sky that this is really the only computationally tractable option. However, this can sometimes result in scattering that's too intense, or not intense enough, depending on your use case. This parameter is an artistic override to help mitigate that issue.")]
public MinFloatParameter nightSkyScatterIntensity = new MinFloatParameter(50, 0);
[Tooltip("An additional tint applied on top of the night sky tint, but only to the scattering. This is useful as an artistsic override for if the average color of your sky texture doesn't quite get you the scattering behavior you want. For instance, you may want the scattering to be bluer.")]
public ColorParameter nightSkyScatterTint = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
[Tooltip("Expanse factors in the average night sky color, affected by the scatter intensity and the sky/scatter tints, to the dynamic sky ambient lightmap. Sometimes it's useful to have the night sky cast more light than it actually emits though, for readability purposes. This is an artistic override that allows you to increase or decrease the night sky's contribution to ambient lighting. Don't be afraid to push this to extremely high values (tens or hundreds of thousands) to get the effect you want.")]
public MinFloatParameter nightSkyAmbientMultiplier = new MinFloatParameter(1, 0);

/* Star twinkle effect. */
[Tooltip("Whether or not to use star twinkle effect.")]
public BoolParameter useTwinkle = new BoolParameter(false);
[Tooltip("Threshold for night sky texture value for twinkle effect to be applied. Useful for avoiding noise-like artifactss on non-star features like nebulae.")]
public MinFloatParameter twinkleThreshold = new MinFloatParameter(0.001f, 0);
[Tooltip("Range of randomly generated twinkle frequencies. Higher values will make the stars twinkle faster. Lower values will make them twinkle slower. A value of zero will result in no twinkling at all.")]
public FloatRangeParameter twinkleFrequencyRange = new FloatRangeParameter(new Vector2(0.5f, 3), 0, 10);
[Tooltip("Bias to twinkle effect. Negative values increase the time when the star is not visible.")]
public FloatParameter twinkleBias = new FloatParameter(0);
[Tooltip("Intensity of smoother twinkle effect.")]
public MinFloatParameter twinkleSmoothAmplitude = new MinFloatParameter(1, 0);
[Tooltip("Intensity of more chaotic twinkle effect.")]
public MinFloatParameter twinkleChaoticAmplitude = new MinFloatParameter(1, 0);

/* Aerial Perspective. */
[Tooltip("This parameter controls how aggressively aerial perspective due to Rayleigh and Isotropic (\"uniform\") layers is attenuated as a consequence of approximate volumetric shadowing. To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionPowerUniform = new MinFloatParameter(0.5f, 0);
[Tooltip("This parameter is a way of offsetting the attenuation of aerial perspective as a consequence of approximate volumetric shadowing (for Rayleigh and Isotropic (\"uniform\") layers). To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionBiasUniform = new MinFloatParameter(0.25f, 0);
[Tooltip("This parameter controls how aggressively aerial perspective due to Mie (\"directional\") layers is attenuated as a consequence of approximate volumetric shadowing. To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionPowerDirectional = new MinFloatParameter(1, 0);
[Tooltip("This parameter is a way of offsetting the attenuation of aerial perspective as a consequence of approximate volumetric shadowing (for Mie (\"directional\") layers). To see the effect, put the sun behind a big piece of geometry (like a mountain) and play around with this parameter. Expanse does not accurately model atmospheric volumetric shadows due to the performance cost, and instead uses this approximation to avoid visual artifacts.")]
public MinFloatParameter aerialPerspectiveOcclusionBiasDirectional = new MinFloatParameter(0.02f, 0);
[Tooltip("The night scattering effect can sometimes be either too intense or not intense enough for aerial perspective. This allows for attenuation of night scattering for aerial perspective only.")]
public MinFloatParameter aerialPerspectiveNightScatteringMultiplier = new MinFloatParameter(0.2f, 0.0f);

/* Quality. */
[Tooltip("Quality of sky texture.")]
public EnumParameter<ExpanseCommon.SkyTextureQuality> skyTextureQuality = new EnumParameter<ExpanseCommon.SkyTextureQuality>(ExpanseCommon.SkyTextureQuality.Medium);
[Tooltip("The number of samples used when computing transmittance lookup tables. With importance sampling turned on, a value of as low as 10 gives near-perfect results on the ground. A value as low as 4 is ok if some visible inaccuracy is tolerable. Without importantance sampling, a value of 32 or higher is recommended.")]
public ClampedIntParameter numberOfTransmittanceSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("The number of samples used when computing light pollution. With importance sampling turned on, a value of as low as 10 gives near-perfect results on the ground. A value as low as 8 is ok if some visible inaccuracy is tolerable. Without importantance sampling, a value of 64 or higher is recommended.")]
public ClampedIntParameter numberOfAerialPerspectiveSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("The number of samples used when computing single scattering. With importance sampling turned on, a value of as low as 10 gives near-perfect results on the ground. A value as low as 5 is ok if some visible inaccuracy is tolerable. Without importantance sampling, a value of 32 or higher is recommended.")]
public ClampedIntParameter numberOfSingleScatteringSamples = new ClampedIntParameter(32, 1, 256);
[Tooltip("The number of samples used when sampling the ground irradiance. Importance sampling does not apply here. To get a near-perfect result, around 10 samples is necessary. But it is a fairly subtle effect, so as low as 6 samples gives a decent result.")]
public ClampedIntParameter numberOfGroundIrradianceSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("The number of samples to use when computing the initial isotropic estimate of multiple scattering. Importance sampling does not apply here. To get a near-perfect result, around 15 samples is necessary. But it is a fairly subtle effect, so as low as 6 samples gives a decent result.")]
public ClampedIntParameter numberOfMultipleScatteringSamples = new ClampedIntParameter(32, 1, 256);
[Tooltip("The number of samples to use when computing the actual accumulated estimate of multiple scattering from the isotropic estimate. The number of samples to use when computing the initial isotropic estimate of multiple scattering. With importance sample, 8 samples gives a near-perfect result. However, multiple scattering is a fairly subtle effect, so as low as 3 samples gives a decent result. Without importance sampling, a value of 32 or higher is necessary for near perfect results, but a value of 4 is sufficient for most needs.")]
public ClampedIntParameter numberOfMultipleScatteringAccumulationSamples = new ClampedIntParameter(12, 1, 256);
[Tooltip("Whether or not to use importance sampling for all calculations except aerial perspective. Importance sampling is a sample distribution strategy that increases fidelity given a limited budget of samples. It is recommended to turn it on, as it doesn't decrease fidelity, but does allow for fewer samples to be taken, boosting performance. However, for outer-space perspectives, it can sometimes introduce inaccuracies, so it can be useful to increase sample counts and turn off importance sampling in those cases.")]
public BoolParameter useImportanceSampling = new BoolParameter(false);
[Tooltip("Whether or not to use importance sampling for aerial perspective. Importance sampling is a sample distribution strategy that increases fidelity given a limited budget of samples. However, it can sometimes cause artifacts or perform poorly when computing aerial perspective, so the option to turn it off for aerial perspective only is provided.")]
public BoolParameter aerialPerspectiveUseImportanceSampling = new BoolParameter(false);
[Tooltip("Skews precomputed aerial perspective samples to be further from the camera (if less than 1) or closer to the camera (if greater than 1). This is useful for environments with very heavy fog, where it can be more important to capture scattering close to the camera.")]
public ClampedFloatParameter aerialPerspectiveDepthSkew = new ClampedFloatParameter(1, 0.25f, 5);
[Tooltip("Whether or not to use MSAA 8x anti-aliasing. Expanse uses conditional MSAA, only multisampling on the edges of celestial bodies and the ground, so this should not be much of a performance hit.")]
public BoolParameter useAntiAliasing = new BoolParameter(false);
[Tooltip("Whether or not to use dithering, to reduce color banding. Since expanse computes everything in floating point HDR values, this is more of a de-band operation than a true dither, and you may be better off using a dither post-process step on your camera.")]
public BoolParameter useDither = new BoolParameter(true);

/***********************/
/******* Clouds ********/
/***********************/
/* General. */
[Tooltip("Whether or not this cloud layer is enabled.")]
public BoolParameter cloudLayerEnabled0, cloudLayerEnabled1, cloudLayerEnabled2,
  cloudLayerEnabled3, cloudLayerEnabled4, cloudLayerEnabled5, cloudLayerEnabled6, cloudLayerEnabled7;

/* Geometry. TODO */
[Tooltip("Type of geometry for this cloud layer.")]
public EnumParameter<ExpanseCommon.CloudGeometryType> cloudGeometryType0, cloudGeometryType1, cloudGeometryType2,
  cloudGeometryType3, cloudGeometryType4, cloudGeometryType5, cloudGeometryType6, cloudGeometryType7;
[Tooltip("X extent of this cloud layer's geometry.")]
public Vector2Parameter cloudGeometryXExtent0, cloudGeometryXExtent1, cloudGeometryXExtent2,
  cloudGeometryXExtent3, cloudGeometryXExtent4, cloudGeometryXExtent5, cloudGeometryXExtent6, cloudGeometryXExtent7;
[Tooltip("Z extent of this cloud layer's geometry.")]
public Vector2Parameter cloudGeometryZExtent0, cloudGeometryZExtent1, cloudGeometryZExtent2,
  cloudGeometryZExtent3, cloudGeometryZExtent4, cloudGeometryZExtent5, cloudGeometryZExtent6, cloudGeometryZExtent7;
/* For box volume. */
[Tooltip("Y extent of this cloud layer's geometry.")]
public Vector2Parameter cloudGeometryYExtent0, cloudGeometryYExtent1, cloudGeometryYExtent2,
  cloudGeometryYExtent3, cloudGeometryYExtent4, cloudGeometryYExtent5, cloudGeometryYExtent6, cloudGeometryYExtent7;
/* For plane and sphere. */
[Tooltip("Height of this cloud layer's geometry.")]
public FloatParameter cloudGeometryHeight0, cloudGeometryHeight1, cloudGeometryHeight2,
  cloudGeometryHeight3, cloudGeometryHeight4, cloudGeometryHeight5, cloudGeometryHeight6, cloudGeometryHeight7;

/* Noise generation. TODO */

/* Movement---sampling offsets primarily. TODO */

/* Lighting. TODO */

/* Sampling. TODO */
/* TODO: debug goes here. */

/******************************************************************************/
/************************** END SERIALIZED PARAMETERS *************************/
/******************************************************************************/



/* Constructor to initialize defaults for array parameters. */
public Expanse() : base() {
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
    this.GetType().GetField("layerDensityAttenuationPlayerOrigin" + i).SetValue(this, new BoolParameter(false));
    this.GetType().GetField("layerDensityAttenuationOrigin" + i).SetValue(this, new Vector3Parameter(new Vector3(0, 0, 0)));
    this.GetType().GetField("layerAttenuationDistance" + i).SetValue(this, new MinFloatParameter(1000, 1));
    this.GetType().GetField("layerAttenuationBias" + i).SetValue(this, new MinFloatParameter(0, 0));
    this.GetType().GetField("layerTint" + i).SetValue(this, new ColorParameter(Color.grey, hdr: false, showAlpha: false, showEyeDropper: true));
    this.GetType().GetField("layerMultipleScatteringMultiplier" + i).SetValue(this, new MinFloatParameter(1.0f, 0.0f));
  }

  /* Celestial body initialization. */
  for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
    /* Enable only the first celestial body by default. */
    this.GetType().GetField("bodyEnabled" + i).SetValue(this, new BoolParameter(i==0));
    this.GetType().GetField("bodyUseDateTime" + i).SetValue(this, new BoolParameter(false));
    this.GetType().GetField("bodyDateTime" + i).SetValue(this, new DateTimeParameter(DateTimeParameter.dateTimeToString(new DateTime(2020, 11, 6))));
    this.GetType().GetField("bodyPlayerLatitudeLongitude" + i).SetValue(this, new Vector2Parameter(new Vector2(39, 122)));
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

  /* Cloud layer initialization. */
  for (int i = 0; i < ExpanseCommon.kMaxCloudLayers; i++) {
    /* Enable only the first layer by default. */
    this.GetType().GetField("cloudLayerEnabled" + i).SetValue(this, new BoolParameter(i==0));
    this.GetType().GetField("cloudGeometryType" + i).SetValue(this, new EnumParameter<ExpanseCommon.CloudGeometryType>(ExpanseCommon.CloudGeometryType.BoxVolume));
    this.GetType().GetField("cloudGeometryXExtent" + i).SetValue(this, new Vector2Parameter(new Vector2(-1000, 1000)));
    this.GetType().GetField("cloudGeometryYExtent" + i).SetValue(this, new Vector2Parameter(new Vector2(2000, 3000)));
    this.GetType().GetField("cloudGeometryZExtent" + i).SetValue(this, new Vector2Parameter(new Vector2(-1000, 1000)));
    this.GetType().GetField("cloudGeometryHeight" + i).SetValue(this, new FloatParameter(10000));
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
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("layerDensityAttenuationPlayerOrigin" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("layerDensityAttenuationOrigin" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationDistance" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationBias" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("layerTint" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerMultipleScatteringMultiplier" + i).GetValue(this)).value.GetHashCode();
    }

    /* Celestial bodies. */
    for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("bodyEnabled" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("bodyUseDateTime" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("bodyDirection" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((DateTimeParameter) this.GetType().GetField("bodyDateTime" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("bodyPlayerLatitudeLongitude" + i).GetValue(this)).value.GetHashCode();
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
    hash = hash * 23 + starTint.value.GetHashCode();
    hash = hash * 23 + useProceduralNebulae.value.GetHashCode();
    hash = hash * 23 + nebulaeTextureQuality.value.GetHashCode();
    hash = nebulaeTexture.value != null ? hash * 23 + nebulaeTexture.value.GetHashCode() : hash;
    /* Procedural nebulae. */
    hash = hash * 23 + nebulaOverallDefinition.value.GetHashCode();
    hash = hash * 23 + nebulaOverallIntensity.value.GetHashCode();
    hash = hash * 23 + nebulaCoverageScale.value.GetHashCode();

    hash = hash * 23 + nebulaHazeBrightness.value.GetHashCode();
    hash = hash * 23 + nebulaHazeColor.value.GetHashCode();
    hash = hash * 23 + nebulaHazeScale.value.GetHashCode();
    hash = hash * 23 + nebulaHazeScaleFactor.value.GetHashCode();
    hash = hash * 23 + nebulaHazeDetailBalance.value.GetHashCode();
    hash = hash * 23 + nebulaHazeOctaves.value.GetHashCode();
    hash = hash * 23 + nebulaHazeBias.value.GetHashCode();
    hash = hash * 23 + nebulaHazeSpread.value.GetHashCode();
    hash = hash * 23 + nebulaHazeCoverage.value.GetHashCode();
    hash = hash * 23 + nebulaHazeStrength.value.GetHashCode();

    hash = hash * 23 + nebulaCloudBrightness.value.GetHashCode();
    hash = hash * 23 + nebulaCloudColor.value.GetHashCode();
    hash = hash * 23 + nebulaCloudScale.value.GetHashCode();
    hash = hash * 23 + nebulaCloudScaleFactor.value.GetHashCode();
    hash = hash * 23 + nebulaCloudDetailBalance.value.GetHashCode();
    hash = hash * 23 + nebulaCloudOctaves.value.GetHashCode();
    hash = hash * 23 + nebulaCloudBias.value.GetHashCode();
    hash = hash * 23 + nebulaCloudSpread.value.GetHashCode();
    hash = hash * 23 + nebulaCloudCoverage.value.GetHashCode();
    hash = hash * 23 + nebulaCloudStrength.value.GetHashCode();

    hash = hash * 23 + nebulaCoarseStrandBrightness.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandColor.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandScale.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandScaleFactor.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandDetailBalance.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandOctaves.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandBias.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandDefinition.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandSpread.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandCoverage.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandStrength.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandWarpScale.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandWarp.value.GetHashCode();

    hash = hash * 23 + nebulaFineStrandBrightness.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandColor.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandScale.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandScaleFactor.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandDetailBalance.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandOctaves.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandBias.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandDefinition.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandSpread.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandCoverage.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandStrength.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandWarpScale.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandWarp.value.GetHashCode();

    hash = hash * 23 + nebulaTransmittanceRange.value.GetHashCode();
    hash = hash * 23 + nebulaTransmittanceScale.value.GetHashCode();

    hash = hash * 23 + starNebulaFollowAmount.value.GetHashCode();
    hash = hash * 23 + starNebulaFollowSpread.value.GetHashCode();

    hash = hash * 23 + nebulaCoverageSeed.value.GetHashCode();
    hash = hash * 23 + nebulaHazeSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaHazeSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaHazeSeedZ.value.GetHashCode();
    hash = hash * 23 + nebulaCloudSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaCloudSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaCloudSeedZ.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandSeedZ.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandWarpSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandWarpSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaCoarseStrandWarpSeedZ.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandSeedZ.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandWarpSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandWarpSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaFineStrandWarpSeedZ.value.GetHashCode();
    hash = hash * 23 + nebulaTransmittanceSeedX.value.GetHashCode();
    hash = hash * 23 + nebulaTransmittanceSeedY.value.GetHashCode();
    hash = hash * 23 + nebulaTransmittanceSeedZ.value.GetHashCode();

    /* Texture. */
    hash = hash * 23 + lightPollutionTint.value.GetHashCode();
    hash = hash * 23 + lightPollutionIntensity.value.GetHashCode();
    hash = nightSkyTexture.value != null ? hash * 23 + nightSkyTexture.value.GetHashCode() : hash;
    hash = hash * 23 + nightSkyRotation.value.GetHashCode();
    hash = hash * 23 + nightSkyTint.value.GetHashCode();
    hash = hash * 23 + nightSkyIntensity.value.GetHashCode();
    hash = hash * 23 + nightSkyAmbientMultiplier.value.GetHashCode();
    hash = hash * 23 + nightSkyScatterTint.value.GetHashCode();
    hash = hash * 23 + nightSkyScatterIntensity.value.GetHashCode();

    /* Aerial Perspective. */
    hash = hash * 23 + aerialPerspectiveOcclusionPowerUniform.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionBiasUniform.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionPowerDirectional.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveOcclusionBiasDirectional.value.GetHashCode();

    /* Quality. */
    hash = hash * 23 + skyTextureQuality.value.GetHashCode();
    hash = hash * 23 + numberOfTransmittanceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfAerialPerspectiveSamples.value.GetHashCode();
    hash = hash * 23 + numberOfSingleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfGroundIrradianceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringAccumulationSamples.value.GetHashCode();
    hash = hash * 23 + useImportanceSampling.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveUseImportanceSampling.value.GetHashCode();
    hash = hash * 23 + useAntiAliasing.value.GetHashCode();
    hash = hash * 23 + useDither.value.GetHashCode();

    /* Cloud Layers. */
    for (int i = 0; i < ExpanseCommon.kMaxCloudLayers; i++) {
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("cloudLayerEnabled" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((EnumParameter<ExpanseCommon.CloudGeometryType>) this.GetType().GetField("cloudGeometryType" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("cloudGeometryXExtent" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("cloudGeometryYExtent" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("cloudGeometryZExtent" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((FloatParameter) this.GetType().GetField("cloudGeometryHeight" + i).GetValue(this)).value.GetHashCode();
    }
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
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("layerDensityAttenuationPlayerOrigin" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector3Parameter) this.GetType().GetField("layerDensityAttenuationOrigin" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationDistance" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((MinFloatParameter) this.GetType().GetField("layerAttenuationBias" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((ColorParameter) this.GetType().GetField("layerTint" + i).GetValue(this)).value.GetHashCode();
    }

    /* Aerial Perspective. */

    /* Quality. */
    hash = hash * 23 + skyTextureQuality.value.GetHashCode();
    hash = hash * 23 + numberOfTransmittanceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfAerialPerspectiveSamples.value.GetHashCode();
    hash = hash * 23 + numberOfSingleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfGroundIrradianceSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringSamples.value.GetHashCode();
    hash = hash * 23 + numberOfMultipleScatteringAccumulationSamples.value.GetHashCode();
    hash = hash * 23 + useImportanceSampling.value.GetHashCode();
    hash = hash * 23 + aerialPerspectiveDepthSkew.value.GetHashCode();
  }
  return hash;
}

public int GetCloudHashCode() {
  int hash = base.GetHashCode();
  unchecked {
    /* Cloud Layers. TODO: Unclear if all this is necessary. */
    for (int i = 0; i < ExpanseCommon.kMaxCloudLayers; i++) {
      hash = hash * 23 + ((BoolParameter) this.GetType().GetField("cloudLayerEnabled" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((EnumParameter<ExpanseCommon.CloudGeometryType>) this.GetType().GetField("cloudGeometryType" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("cloudGeometryXExtent" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("cloudGeometryYExtent" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((Vector2Parameter) this.GetType().GetField("cloudGeometryZExtent" + i).GetValue(this)).value.GetHashCode();
      hash = hash * 23 + ((FloatParameter) this.GetType().GetField("cloudGeometryHeight" + i).GetValue(this)).value.GetHashCode();
    }
  }
  return hash;
}

public int GetNightSkyHashCode() {
  /* Used for checking if a recomputation of the night sky texture and
   * average color needs to take place. */
  int hash = base.GetHashCode();
  unchecked {
    hash = hash * 23 + useProceduralNightSky.value.GetHashCode();
    if (useProceduralNightSky.value) {
      hash = hash * 23 + starTextureQuality.value.GetHashCode();
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
      /* Procedural nebulae. */
      hash = hash * 23 + nebulaCoverageScale.value.GetHashCode();

      hash = hash * 23 + nebulaHazeBrightness.value.GetHashCode();
      hash = hash * 23 + nebulaHazeColor.value.GetHashCode();
      hash = hash * 23 + nebulaHazeScale.value.GetHashCode();
      hash = hash * 23 + nebulaHazeScaleFactor.value.GetHashCode();
      hash = hash * 23 + nebulaHazeDetailBalance.value.GetHashCode();
      hash = hash * 23 + nebulaHazeOctaves.value.GetHashCode();
      hash = hash * 23 + nebulaHazeBias.value.GetHashCode();
      hash = hash * 23 + nebulaHazeSpread.value.GetHashCode();
      hash = hash * 23 + nebulaHazeCoverage.value.GetHashCode();
      hash = hash * 23 + nebulaHazeStrength.value.GetHashCode();

      hash = hash * 23 + nebulaCloudBrightness.value.GetHashCode();
      hash = hash * 23 + nebulaCloudColor.value.GetHashCode();
      hash = hash * 23 + nebulaCloudScale.value.GetHashCode();
      hash = hash * 23 + nebulaCloudScaleFactor.value.GetHashCode();
      hash = hash * 23 + nebulaCloudDetailBalance.value.GetHashCode();
      hash = hash * 23 + nebulaCloudOctaves.value.GetHashCode();
      hash = hash * 23 + nebulaCloudBias.value.GetHashCode();
      hash = hash * 23 + nebulaCloudSpread.value.GetHashCode();
      hash = hash * 23 + nebulaCloudCoverage.value.GetHashCode();
      hash = hash * 23 + nebulaCloudStrength.value.GetHashCode();

      hash = hash * 23 + nebulaCoarseStrandBrightness.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandColor.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandScale.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandScaleFactor.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandDetailBalance.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandOctaves.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandBias.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandDefinition.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandSpread.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandCoverage.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandStrength.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandWarpScale.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandWarp.value.GetHashCode();


      hash = hash * 23 + nebulaFineStrandBrightness.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandColor.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandScale.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandScaleFactor.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandDetailBalance.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandOctaves.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandBias.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandDefinition.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandSpread.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandCoverage.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandStrength.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandWarpScale.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandWarp.value.GetHashCode();

      hash = hash * 23 + nebulaTransmittanceRange.value.GetHashCode();
      hash = hash * 23 + nebulaTransmittanceScale.value.GetHashCode();

      hash = hash * 23 + starNebulaFollowAmount.value.GetHashCode();
      hash = hash * 23 + starNebulaFollowSpread.value.GetHashCode();

      hash = hash * 23 + nebulaCoverageSeed.value.GetHashCode();
      hash = hash * 23 + nebulaHazeSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaHazeSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaHazeSeedZ.value.GetHashCode();
      hash = hash * 23 + nebulaCloudSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaCloudSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaCloudSeedZ.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandSeedZ.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandWarpSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandWarpSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaCoarseStrandWarpSeedZ.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandSeedZ.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandWarpSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandWarpSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaFineStrandWarpSeedZ.value.GetHashCode();
      hash = hash * 23 + nebulaTransmittanceSeedX.value.GetHashCode();
      hash = hash * 23 + nebulaTransmittanceSeedY.value.GetHashCode();
      hash = hash * 23 + nebulaTransmittanceSeedZ.value.GetHashCode();
    }
    hash = nightSkyTexture.value != null ? hash * 23 + nightSkyTexture.value.GetHashCode() : hash;
  }
  return hash;
}

}
