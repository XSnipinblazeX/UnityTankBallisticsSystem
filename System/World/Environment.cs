using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Environment : MonoBehaviour, IWorld
{

    public float AirDensity => _AirDensity;
    public float TempCelsius => _TempCelsius;
    public float AmmoTempFahrenheit => _AmmoTempFahrenheit;
    public float AirCoefficient => _AirCoefficient;

    public float MaxWindSpeed  => 3f;

    public bool Night => false;
    public bool Rain  => false;
    public bool Snow  => false;

    public static float _AirDensity = 1.225f;

    public static float _AmmoTempFahrenheit = 70f;

    public static float _TempCelsius = 20f;

    public static float _AirCoefficient = 331.3f + 0.606f * _TempCelsius; 

    public float GetSoundDelay(Vector3 emitter, Vector3 listener)
    {
        float delay = Vector3.Distance(emitter, listener) / AirCoefficient;
        return delay;
    }
}
