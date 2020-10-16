using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Expanse;

[ExecuteInEditMode]
public class ExpanseLightControl : MonoBehaviour
{

  GameObject skyFogVolume;
  UnityEngine.Rendering.Volume volume;
  ExpanseSky sky;
  UnityEngine.Light light;
  float t;

  // Start is called before the first frame update
  void Start() {
    /* Get the light. */
    light = gameObject.GetComponent(typeof(UnityEngine.Light)) as UnityEngine.Light;

    /* Get the sky. */
    skyFogVolume = GameObject.FindWithTag("ExpanseSkyAndFogVolume");
    volume = skyFogVolume.GetComponent<UnityEngine.Rendering.Volume>();
    ExpanseSky tmp;
    if( volume.profile.TryGet<ExpanseSky>( out tmp ) ) {
      sky = tmp;
      Debug.Log("success");
    } else {
      Debug.Log("failed");
    }

    t = 0;
  }

  // Update is called once per frame
  void Update() {
    gameObject.transform.eulerAngles = new Vector3(sky.bodyDirection0.value.x, sky.bodyDirection0.value.y, 0);

    /* Compute and set light color. */
    bool useTemperature = sky.bodyUseTemperature0.value;
    float lightIntensity = sky.bodyLightIntensity0.value;
    Vector4 lightColor = sky.bodyLightColor0.value;
    if (useTemperature) {
      float temperature = sky.bodyLightTemperature0.value;
      Vector4 temperatureColor = ExpanseCommon.blackbodyTempToColor(temperature);
      light.color = (new Vector4(temperatureColor.x * lightColor.x,
        temperatureColor.y * lightColor.y,
        temperatureColor.z * lightColor.z,
        temperatureColor.w * lightColor.w));
    } else {
      light.color = lightColor;
    }

    light.intensity = lightIntensity;

    t += 0.1f;
  }
}
