using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ExpanseCommonNamespace;

[ExecuteInEditMode]
public class ExpanseLightControl : MonoBehaviour
{

  GameObject skyFogVolume;
  UnityEngine.Rendering.Volume volume;
  Expanse sky;
  UnityEngine.Light light;
  public int bodyIndex;

  // Start is called before the first frame update
  void Start() {
    Debug.Log("Starting light control.");
    /* Get the light. */
    light = gameObject.GetComponent(typeof(UnityEngine.Light)) as UnityEngine.Light;

    /* Get the sky. */
    skyFogVolume = GameObject.FindWithTag("ExpanseSkyAndFogVolume");
    volume = skyFogVolume.GetComponent<UnityEngine.Rendering.Volume>();
    Expanse tmp;
    if( volume.profile.TryGet<Expanse>( out tmp ) ) {
      sky = tmp;
      Debug.Log("success");
    } else {
      Debug.Log("failed"); // lal
    }
  }

  // Update is called once per fram
  void Update() {
    /* For editor. */
    if (sky == null) {
      Start();
    }

    int i = bodyIndex;
    Vector3 bodyDirection = ((Vector3Parameter) sky.GetType().GetField("bodyDirection" + i).GetValue(sky)).value;
    gameObject.transform.eulerAngles = new Vector3(bodyDirection.x, bodyDirection.y, bodyDirection.z);

    /* Compute and set light color. */
    bool useTemperature = ((BoolParameter) sky.GetType().GetField("bodyUseTemperature" + i).GetValue(sky)).value;
    float lightIntensity = ((FloatParameter) sky.GetType().GetField("bodyLightIntensity" + i).GetValue(sky)).value;
    Vector4 lightColor = ((ColorParameter) sky.GetType().GetField("bodyLightColor" + i).GetValue(sky)).value;
    if (useTemperature) {
      float temperature = ((FloatParameter) sky.GetType().GetField("bodyLightTemperature" + i).GetValue(sky)).value;
      Vector4 temperatureColor = ExpanseCommon.blackbodyTempToColor(temperature);
      light.color = (new Vector4(temperatureColor.x * lightColor.x,
        temperatureColor.y * lightColor.y,
        temperatureColor.z * lightColor.z,
        temperatureColor.w * lightColor.w));
    } else {
      light.color = lightColor;
    }

    Vector3 transmittance = ExpanseCommon.bodyTransmittances[bodyIndex];
    Vector4 transmittanceV4 = new Vector4(transmittance.x, transmittance.y, transmittance.z, 1);
    light.color = light.color * transmittanceV4;

    light.intensity = lightIntensity;
  }
}
