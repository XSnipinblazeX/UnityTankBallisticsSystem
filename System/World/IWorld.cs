using System.Collections.Generic;
using UnityEngine;


public interface IWorld
{
    public float AirDensity { get; }
    public float TempCelsius { get; }
    public float AmmoTempFahrenheit { get; }
    public float AirCoefficient { get; }

    public float MaxWindSpeed { get; }

    public bool Night { get; }
    public bool Rain { get; }
    public bool Snow { get; }

    /*  public static float AirDensity { get; private set; } = 1.225f;

                    public static float AmmoTempFahrenheit { get; private set; } = 70f;

                    public static float TempCelsius { get; private set; } = 20f;

                    public static float AirCoefficient { get; private set; } = 331.3f + 0.606f * TempCelsius; */


}
