using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.AnimatedValues;
using Expanse;

// [CanEditMultipleObjects]
[VolumeComponentEditor(typeof(ExpanseSky))]
class ExpanseSkyEditor : SkySettingsEditor
{

/******************************************************************************/
/******************************* UI VARIABLES *********************************/
/******************************************************************************/

/* Specifies if an atmosphere layer ui should be shown. */
AnimBool[] m_showAtmosphereLayer =
  new AnimBool[ExpanseCommon.kMaxAtmosphereLayers];
/* Which atmosphere layer is currently shown for editing. */
ExpanseCommon.AtmosphereLayer m_atmosphereLayerSelect = ExpanseCommon.AtmosphereLayer.Layer0;

/* Specifies if a celestial body ui should be shown. */
AnimBool[] m_showCelestialBody =
  new AnimBool[ExpanseCommon.kMaxCelestialBodies];
/* Which atmosphere layer is currently shown for editing. */
ExpanseCommon.CelestialBody m_celestialBodySelect = ExpanseCommon.CelestialBody.Body0;

/******************************************************************************/
/****************************** END UI VARIABLES ******************************/
/******************************************************************************/



/******************************************************************************/
/*************************** SERIALIZED PARAMETERS ****************************/
/******************************************************************************/

/***********************/
/********* Sky *********/
/***********************/
/* Planet parameters. */
SerializedDataParameter atmosphereThickness;
SerializedDataParameter planetRadius;
SerializedDataParameter planetRotation;
SerializedDataParameter groundAlbedoTexture;
SerializedDataParameter groundTint;
SerializedDataParameter groundEmissionTexture;
SerializedDataParameter groundEmissionTint;
SerializedDataParameter groundEmissionMultiplier;

/* Atmosphere layers. TODO */
/* TODO: artistic overrides go here. Every layer has an individual tint
 * and MS multiplier. */
/* TODO: what's the best way to indicate
 *  A) what kind of density distribution we use?
 *  B) whether or not we use distance attenuation?
 *  C) what kind of phase function we use?
 *
 * The answer: probably use a clamped int parameter linked to an enum
 * in common, then have a UI element that allows for selection somehow. */
SerializedDataParameter[] layerEnabled
 = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerCoefficients
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerDensityDistribution
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerHeight
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
/* Only used for tent layers. */
SerializedDataParameter[] layerThickness
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerPhaseFunction
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerAnisotropy
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerUseDensityAttenuation
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerDensity
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerAttenuationDistance
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerAttenuationBias
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerTint
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerMultipleScatteringMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];

/* Celestial bodies. TODO */

/* Celestial Bodies. TODO */
SerializedDataParameter[] bodyEnabled
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyDirection
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyAngularRadius
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyDistance
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyReceivesLight
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyAlbedoTexture
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
/* Displayed on null check of body albedo texture. */
SerializedDataParameter[] bodyAlbedoTextureRotation
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyAlbedoTint
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissive
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyLightIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyUseTemperature
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
/* Display as "filter" in temperature mode. */
SerializedDataParameter[] bodyLightColor
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyLightTemperature
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyLimbDarkening
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissionTexture
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
/* Displayed on null check of body albedo texture. */
SerializedDataParameter[] bodyEmissionTextureRotation
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissionTint
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissionMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];


/* Night Sky. TODO */

/* Quality parameters. */
SerializedDataParameter skyTextureQuality;
SerializedDataParameter numberOfTransmittanceSamples;
SerializedDataParameter numberOfLightPollutionSamples;
SerializedDataParameter numberOfSingleScatteringSamples;
SerializedDataParameter numberOfGroundIrradianceSamples;
SerializedDataParameter numberOfMultipleScatteringSamples;
SerializedDataParameter numberOfMultipleScatteringAccumulationSamples;
SerializedDataParameter useImportanceSampling;
SerializedDataParameter useAntiAliasing;
SerializedDataParameter ditherAmount;

/***********************/
/******* Clouds ********/
/***********************/
/* Lighting. TODO */
/* TODO: density control goes here. */

/* Noise generation. TODO */

/* Movement---sampling offsets primarily. TODO */

/* Geometry. TODO */

/* Sampling. TODO */
/* TODO: debug goes here. */

/******************************************************************************/
/************************** END SERIALIZED PARAMETERS *************************/
/******************************************************************************/



/******************************************************************************/
/****************************** MAIN FUNCTIONS ********************************/
/******************************************************************************/

public override void OnEnable()
{
  /* Boilerplate. */
  base.OnEnable();
  m_CommonUIElementsMask = (uint) SkySettingsUIElement.UpdateMode;

  /* Initialize all show atmosphere layers to false. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    m_showAtmosphereLayer[i] = new AnimBool(i == (int) m_atmosphereLayerSelect);
    m_showAtmosphereLayer[i].valueChanged.AddListener(Repaint);
  }

  /* Initialize all show celestial bodies to false. */
  for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
    m_showCelestialBody[i] = new AnimBool(i == (int) m_celestialBodySelect);
    m_showCelestialBody[i].valueChanged.AddListener(Repaint);
  }

  /* Get the serialized properties from the ExpanseSky class to
   * attach to the editor. */
  var o = new PropertyFetcher<ExpanseSky>(serializedObject);

  /* Unpack all the serialized properties into our variables. */
  unpackSerializedProperties(o);
}

public override void OnInspectorGUI()
{
  /* Styles for the GUI. */
  UnityEngine.GUIStyle mainHeaderStyle = new UnityEngine.GUIStyle();
  mainHeaderStyle.fontSize = 18;
  mainHeaderStyle.normal.textColor = UnityEngine.Color.white;
  UnityEngine.GUIStyle titleStyle = new UnityEngine.GUIStyle();
  titleStyle.fontSize = 14;
  titleStyle.normal.textColor = UnityEngine.Color.white;
  UnityEngine.GUIStyle subtitleStyle = new UnityEngine.GUIStyle();
  subtitleStyle.fontSize = 12;
  subtitleStyle.normal.textColor = UnityEngine.Color.white;

  /***********************/
  /********* Sky *********/
  /***********************/
  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Sky", mainHeaderStyle);
  EditorGUILayout.Space();

  /* Planet. */
  EditorGUILayout.Space();
  planet(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Atmosphere layers. */
  /* TODO: artistic overrides go here. Every layer has an individual tint
   * and MS multiplier. */
  EditorGUILayout.Space();
  atmosphereLayer(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Celestial bodies. */
  EditorGUILayout.Space();
  celestialBody(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Night Sky. */
  EditorGUILayout.Space();
  nightSky(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Quality. */
  EditorGUILayout.Space();
  quality(titleStyle, subtitleStyle);
  base.CommonSkySettingsGUI();
  EditorGUILayout.Space();

  EditorGUILayout.Space();
  EditorGUILayout.Space();

  /***********************/
  /******* Clouds ********/
  /***********************/
  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Clouds", mainHeaderStyle);
  EditorGUILayout.Space();

  /* Lighting. */
  /* TODO: density control goes here. */
  EditorGUILayout.Space();
  cloudLighting(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Noise generation. */
  EditorGUILayout.Space();
  cloudNoise(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Movement---sampling offsets primarily. */
  EditorGUILayout.Space();
  cloudMovement(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Geometry. */
  EditorGUILayout.Space();
  cloudGeometry(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Sampling. */
  /* TODO: debug goes here. */
  EditorGUILayout.Space();
  cloudSampling(titleStyle, subtitleStyle);
  EditorGUILayout.Space();
}

/******************************************************************************/
/**************************** END MAIN FUNCTIONS ******************************/
/******************************************************************************/



/******************************************************************************/
/**************************** INDIVIDUAL ELEMENTS *****************************/
/******************************************************************************/

private void planet(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  /* Planet. */
  EditorGUILayout.LabelField("Planet", titleStyle);
  PropertyField(atmosphereThickness);
  PropertyField(planetRadius);
  PropertyField(groundAlbedoTexture);
  PropertyField(groundTint);
  PropertyField(groundEmissionTexture);
  PropertyField(groundEmissionMultiplier);
  PropertyField(planetRotation);
}

private void atmosphereLayer(UnityEngine.GUIStyle titleStyle,
  UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Atmosphere", titleStyle);
  m_atmosphereLayerSelect = (ExpanseCommon.AtmosphereLayer)
    EditorGUILayout.EnumPopup("Layer", m_atmosphereLayerSelect);

  /* Set the atmosphere layer select. */
  int atmosphereSelectIndex = setEnumSelect(m_showAtmosphereLayer,
    (int) m_atmosphereLayerSelect);

  /* Display atmosphere params for it. */
  if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showAtmosphereLayer[atmosphereSelectIndex].faded))
  {

    PropertyField(layerEnabled[atmosphereSelectIndex], new UnityEngine.GUIContent("Enabled"));
    PropertyField(layerCoefficients[atmosphereSelectIndex], new UnityEngine.GUIContent("Coefficients"));
    PropertyField(layerDensity[atmosphereSelectIndex], new UnityEngine.GUIContent("Density"));

    /* Density distribution selection dropdown. */
    PropertyField(layerDensityDistribution[atmosphereSelectIndex], new UnityEngine.GUIContent("Layer Density Distribution"));
    PropertyField(layerThickness[atmosphereSelectIndex], new UnityEngine.GUIContent("Thickness"));
    if ((ExpanseCommon.DensityDistribution) layerDensityDistribution[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.DensityDistribution.Tent) {
      /* Only display height control if tent distribution is enabled. */
      PropertyField(layerHeight[atmosphereSelectIndex], new UnityEngine.GUIContent("Height"));
    }

    /* Phase function selection dropdown. */
    PropertyField(layerPhaseFunction[atmosphereSelectIndex], new UnityEngine.GUIContent("Layer Phase Function"));
    if ((ExpanseCommon.PhaseFunction) layerPhaseFunction[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.PhaseFunction.Mie) {
      /* Only display anisotropy control if Mie scattering is enabled. */
      PropertyField(layerAnisotropy[atmosphereSelectIndex], new UnityEngine.GUIContent("Anisotropy"));
    }

    PropertyField(layerUseDensityAttenuation[atmosphereSelectIndex], new UnityEngine.GUIContent("Density Attenuation"));
    if (layerUseDensityAttenuation[atmosphereSelectIndex].value.boolValue) {
      /* Only display density attenuation parameters if we use density attenuation. */
      PropertyField(layerAttenuationDistance[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Distance"));
      PropertyField(layerAttenuationBias[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Bias"));
    }

    PropertyField(layerTint[atmosphereSelectIndex], new UnityEngine.GUIContent("Tint"));
    PropertyField(layerMultipleScatteringMultiplier[atmosphereSelectIndex], new UnityEngine.GUIContent("Multiple Scattering Multiplier"));
  }

  UnityEditor.EditorGUILayout.EndFadeGroup();
}

private void celestialBody(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Celestial Bodies", titleStyle);
  m_celestialBodySelect = (ExpanseCommon.CelestialBody) EditorGUILayout.EnumPopup("Body:", m_celestialBodySelect);

  /* Set the celestial body select. */
  int bodySelectIndex = setEnumSelect(m_showCelestialBody, (int) m_celestialBodySelect);

  /* Display celestial body params for it. */
  if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showCelestialBody[bodySelectIndex].faded))
  {
    PropertyField(bodyEnabled[bodySelectIndex], new UnityEngine.GUIContent("Enabled"));
    PropertyField(bodyDirection[bodySelectIndex], new UnityEngine.GUIContent("Direction"));
    PropertyField(bodyAngularRadius[bodySelectIndex], new UnityEngine.GUIContent("Angular Radius"));
    PropertyField(bodyDistance[bodySelectIndex], new UnityEngine.GUIContent("Distance"));

    PropertyField(bodyLightIntensity[bodySelectIndex], new UnityEngine.GUIContent("Light Intensity"));
    PropertyField(bodyUseTemperature[bodySelectIndex], new UnityEngine.GUIContent("Color Temperature"));
    if (bodyUseTemperature[bodySelectIndex].value.boolValue) {
      PropertyField(bodyLightColor[bodySelectIndex], new UnityEngine.GUIContent("Filter"));
      PropertyField(bodyLightTemperature[bodySelectIndex], new UnityEngine.GUIContent("Temperature"));
    } else {
      PropertyField(bodyLightColor[bodySelectIndex], new UnityEngine.GUIContent("Color"));
    }

    PropertyField(bodyReceivesLight[bodySelectIndex], new UnityEngine.GUIContent("Receives Light"));
    if (bodyReceivesLight[bodySelectIndex].value.boolValue) {
      PropertyField(bodyAlbedoTexture[bodySelectIndex], new UnityEngine.GUIContent("Albedo Texture"));
      if (bodyAlbedoTexture[bodySelectIndex].value.objectReferenceValue != null) {
        PropertyField(bodyAlbedoTextureRotation[bodySelectIndex], new UnityEngine.GUIContent("Albedo Texture Rotation"));
      }
      PropertyField(bodyAlbedoTint[bodySelectIndex], new UnityEngine.GUIContent("Albedo Tint"));
    }

    PropertyField(bodyEmissive[bodySelectIndex], new UnityEngine.GUIContent("Emissive"));
    if (bodyEmissive[bodySelectIndex].value.boolValue) {
      PropertyField(bodyLimbDarkening[bodySelectIndex], new UnityEngine.GUIContent("Limb Darkening"));
      PropertyField(bodyEmissionTexture[bodySelectIndex], new UnityEngine.GUIContent("Emission Texture"));
      if (bodyEmissionTexture[bodySelectIndex].value.objectReferenceValue != null) {
        PropertyField(bodyEmissionTextureRotation[bodySelectIndex], new UnityEngine.GUIContent("Emission Texture Rotation"));
      }
      PropertyField(bodyEmissionTint[bodySelectIndex], new UnityEngine.GUIContent("Emission Tint"));
      PropertyField(bodyEmissionMultiplier[bodySelectIndex], new UnityEngine.GUIContent("Emission Multiplier"));
    }
  }

  UnityEditor.EditorGUILayout.EndFadeGroup();
}

private void nightSky(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Night Sky", titleStyle);
}

private void quality(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Quality", titleStyle);
  /* Texture quality selection dropdown. */
  PropertyField(skyTextureQuality, new UnityEngine.GUIContent("Texture Quality"));
  PropertyField(numberOfTransmittanceSamples, new UnityEngine.GUIContent("Transmittance Samples"));
  PropertyField(numberOfLightPollutionSamples, new UnityEngine.GUIContent("Light Pollution Samples"));
  PropertyField(numberOfSingleScatteringSamples, new UnityEngine.GUIContent("Single Scattering Samples"));
  PropertyField(numberOfGroundIrradianceSamples, new UnityEngine.GUIContent("Ground Irradiance Samples"));
  PropertyField(numberOfMultipleScatteringSamples, new UnityEngine.GUIContent("Multiple Scattering Samples"));
  PropertyField(numberOfMultipleScatteringAccumulationSamples, new UnityEngine.GUIContent("Multiple Scattering Accumulation Samples"));
  PropertyField(useImportanceSampling, new UnityEngine.GUIContent("Importance Sampling"));
  PropertyField(useAntiAliasing, new UnityEngine.GUIContent("Anti-Aliasing"));
  PropertyField(ditherAmount);
}

private void cloudLighting(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Lighting", titleStyle);
}

private void cloudNoise(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Noise", titleStyle);
}

private void cloudMovement(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Movement", titleStyle);
}

private void cloudGeometry(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Geometry", titleStyle);
}

private void cloudSampling(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Sampling", titleStyle);
}

/******************************************************************************/
/************************** END INDIVIDUAL ELEMENTS ***************************/
/******************************************************************************/



/******************************************************************************/
/********************************** HELPERS ***********************************/
/******************************************************************************/

private void unpackSerializedProperties(PropertyFetcher<ExpanseSky> o) {
  /***********************/
  /********* Sky *********/
  /***********************/
  /* Planet. */
  atmosphereThickness = Unpack(o.Find(x => x.atmosphereThickness));
  planetRadius = Unpack(o.Find(x => x.planetRadius));
  groundAlbedoTexture = Unpack(o.Find(x => x.groundAlbedoTexture));
  groundTint = Unpack(o.Find(x => x.groundTint));
  groundEmissionTexture = Unpack(o.Find(x => x.groundEmissionTexture));
  groundEmissionMultiplier = Unpack(o.Find(x => x.groundEmissionMultiplier));
  planetRotation = Unpack(o.Find(x => x.planetRotation));

  /* Atmosphere layers. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    layerEnabled[i] = Unpack(o.Find("layerEnabled" + i));
    layerCoefficients[i] = Unpack(o.Find("layerCoefficients" + i));
    layerDensityDistribution[i] = Unpack(o.Find("layerDensityDistribution" + i));
    layerHeight[i] = Unpack(o.Find("layerHeight" + i));
    layerThickness[i] = Unpack(o.Find("layerThickness" + i));
    layerPhaseFunction[i] = Unpack(o.Find("layerPhaseFunction" + i));
    layerAnisotropy[i] = Unpack(o.Find("layerAnisotropy" + i));
    layerDensity[i] = Unpack(o.Find("layerDensity" + i));
    layerUseDensityAttenuation[i] = Unpack(o.Find("layerUseDensityAttenuation" + i));
    layerAttenuationDistance[i] = Unpack(o.Find("layerAttenuationDistance" + i));
    layerAttenuationBias[i] = Unpack(o.Find("layerAttenuationBias" + i));
    layerTint[i] = Unpack(o.Find("layerTint" + i));
    layerMultipleScatteringMultiplier[i] = Unpack(o.Find("layerMultipleScatteringMultiplier" + i));
  }

  /* Celestial bodies. TODO */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    bodyEnabled[i] = Unpack(o.Find("bodyEnabled" + i));
    bodyDirection[i] = Unpack(o.Find("bodyDirection" + i));
    bodyAngularRadius[i] = Unpack(o.Find("bodyAngularRadius" + i));
    bodyDistance[i] = Unpack(o.Find("bodyDistance" + i));
    bodyReceivesLight[i] = Unpack(o.Find("bodyReceivesLight" + i));
    bodyAlbedoTexture[i] = Unpack(o.Find("bodyAlbedoTexture" + i));
    bodyAlbedoTextureRotation[i] = Unpack(o.Find("bodyAlbedoTextureRotation" + i));
    bodyAlbedoTint[i] = Unpack(o.Find("bodyAlbedoTint" + i));
    bodyEmissive[i] = Unpack(o.Find("bodyEmissive" + i));
    bodyUseTemperature[i] = Unpack(o.Find("bodyUseTemperature" + i));
    bodyLightIntensity[i] = Unpack(o.Find("bodyLightIntensity" + i));
    bodyLightColor[i] = Unpack(o.Find("bodyLightColor" + i));
    bodyLightTemperature[i] = Unpack(o.Find("bodyLightTemperature" + i));
    bodyLimbDarkening[i] = Unpack(o.Find("bodyLimbDarkening" + i));
    bodyEmissionTexture[i] = Unpack(o.Find("bodyEmissionTexture" + i));
    bodyEmissionTextureRotation[i] = Unpack(o.Find("bodyEmissionTextureRotation" + i));
    bodyEmissionTint[i] = Unpack(o.Find("bodyEmissionTint" + i));
    bodyEmissionMultiplier[i] = Unpack(o.Find("bodyEmissionMultiplier" + i));
  }

  /* Night Sky. TODO */

  /* Quality. */
  skyTextureQuality = Unpack(o.Find(x => x.skyTextureQuality));
  numberOfTransmittanceSamples = Unpack(o.Find(x => x.numberOfTransmittanceSamples));
  numberOfLightPollutionSamples = Unpack(o.Find(x => x.numberOfLightPollutionSamples));
  numberOfSingleScatteringSamples = Unpack(o.Find(x => x.numberOfSingleScatteringSamples));
  numberOfGroundIrradianceSamples = Unpack(o.Find(x => x.numberOfGroundIrradianceSamples));
  numberOfMultipleScatteringSamples = Unpack(o.Find(x => x.numberOfMultipleScatteringSamples));
  numberOfMultipleScatteringAccumulationSamples = Unpack(o.Find(x => x.numberOfMultipleScatteringAccumulationSamples));
  useImportanceSampling = Unpack(o.Find(x => x.useImportanceSampling));
  useAntiAliasing = Unpack(o.Find(x => x.useAntiAliasing));
  ditherAmount = Unpack(o.Find(x => x.ditherAmount));

  /***********************/
  /******* Clouds ********/
  /***********************/
  /* Lighting. TODO */
  /* TODO: density control goes here. */

  /* Noise generation. TODO */

  /* Movement---sampling offsets primarily. TODO */

  /* Geometry. TODO */

  /* Sampling. TODO */
  /* TODO: debug goes here. */
}

private int setEnumSelect(AnimBool[] showEnum, int selected) {
  /* Initialize all show atmosphere layers to false. */
  for (int i = 0; i < showEnum.Length; i++) {
    showEnum[i] = new AnimBool(i == selected);
  }
  return selected;
}

/******************************************************************************/
/******************************** END HELPERS *********************************/
/******************************************************************************/

}
