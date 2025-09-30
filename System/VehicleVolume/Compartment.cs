using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// This script no longer has any UI dependencies and focuses solely on the simulation logic.
public class Compartment : MonoBehaviour
{
    public string label = "Fighting";

    // --- Public Properties (Read-Only) to access the state from other scripts ---
    public float Temperature => temperature;
    public float Pressure => pressure;
    public float CleanAirPercentage => cleanAirPercentage;
    public float ToxicGasPercentage => toxicGasPercentage;
    public List<float> BreachDiameters => breachDiameters; // Expose the list of breaches

    // --- Compartment State Variables ---
    // These private variables are now visible in the Inspector for debugging and monitoring.
    [SerializeField]
    private float temperature; // in °C
    [SerializeField]
    private float pressure; // in kPa
    [SerializeField]
    private float cleanAirPercentage; // 0-100%
    [SerializeField]
    private float toxicGasPercentage; // 0-100%

    // --- Simulation Parameters (Tunable in Inspector) ---
    [Header("Simulation Parameters")]
    [Tooltip("Outside temperature in °C. Can be changed via other scripts.")]
    public float outsideTemperature = 20.0f;
    [Tooltip("Rate at which the heater increases temperature per second.")]
    public float heaterRate = 0.1f;
    [Tooltip("Rate at which the ventilation system purges toxic gas per second.")]
    public float ventilationRate = 1.5f;
    [Tooltip("Rate at which clean air is consumed per second.")]
    public float airConsumptionRate = 0.01f;
    [Tooltip("Target pressure when the ventilation system is active.")]
    public float ventilationTargetPressure = 105.0f;

    // --- Explosion Parameters ---
    [Header("Explosion Simulation")]
    [Tooltip("The time in seconds it takes for a full dissipation after an explosion.")]
    public float dissipationTime = 30.0f;
    private float currentDissipationTime = 0.0f;
    [Tooltip("A multiplier (0-1) for how much of the explosive's thermal energy is transferred to the compartment's air.")]
    public float explosionHeatTransferEfficiency = 0.1f;
    
    // This list now stores the diameter of all breaches.
    [SerializeField]
    private List<float> breachDiameters = new List<float>();

    [Header("Breach Management")]
    [Tooltip("If checked, breaches will be sealed after the dissipation period ends.")]
    public bool canSealBreaches = true;

    // --- Internal System Toggles ---
    [Header("Internal Systems")]
    public bool isHeaterOn = false;
    public bool isVentilationOn = false;

    // --- Event Logging ---
    private bool warningLogged = false;

    void Start()
    {
        // Set initial conditions
        temperature = 20.0f;
        pressure = 101.3f; // Standard atmospheric pressure
        cleanAirPercentage = 100.0f;
        toxicGasPercentage = 0.0f;
        
        Debug.Log("Compartment simulation initialized.");
    }

    // FixedUpdate is great for physics and consistent simulation updates
    void FixedUpdate()
    {
        // Handle dissipation after an explosion
        if (currentDissipationTime > 0)
        {
            DissipateExplosionEffects();
        }
        else
        {
            // Regular updates if no explosion is active
            UpdateTemperature();
            UpdatePressure();
        }

        UpdateAirMixture();
        CheckForWarnings();
    }

    // --- Core Simulation Logic ---
    private void UpdateTemperature()
    {
        // Simple heat transfer based on temperature difference
        float tempDiff = outsideTemperature - temperature;
        float heatTransfer = tempDiff * 0.05f * Time.fixedDeltaTime;
        
        // Add heater effect if active
        if (isHeaterOn)
        {
            heatTransfer += heaterRate * Time.fixedDeltaTime;
        }

        temperature += heatTransfer;
    }

    private void UpdatePressure()
    {
        // The target pressure is ambient pressure unless the ventilation system is on.
        float targetPressure = 101.3f; 
        float normalizationRate = 0.01f;

        // If ventilation is on, we're actively pressurizing the compartment.
        if (isVentilationOn)
        {
            targetPressure = ventilationTargetPressure;
            normalizationRate = 0.05f; // A faster normalization rate when system is active.
        }

        // Pressure slowly normalizes to the target pressure
        float pressureDiff = targetPressure - pressure;
        pressure += pressureDiff * normalizationRate * Time.fixedDeltaTime;

        // Clamp pressure to a reasonable range
        pressure = Mathf.Clamp(pressure, 90.0f, ventilationTargetPressure + 5.0f);
    }

    private void UpdateAirMixture()
    {
        // Simulate slow consumption of clean air
        cleanAirPercentage = Mathf.Max(0, cleanAirPercentage - airConsumptionRate * Time.fixedDeltaTime);
        toxicGasPercentage = 100 - cleanAirPercentage;

        // The ventilation system now also filters toxic gas if it is on.
        if (isVentilationOn)
        {
            float purgeAmount = ventilationRate * Time.fixedDeltaTime;
            // Purge toxic gas and add clean air
            if (toxicGasPercentage > 0)
            {
                toxicGasPercentage = Mathf.Max(0, toxicGasPercentage - purgeAmount);
                cleanAirPercentage = 100 - toxicGasPercentage;
            }
        }
    }

    /// <summary>
    /// Simulates an explosion event inside the compartment.
    /// This function will drastically change the temperature, pressure, and air mixture.
    /// It then initiates the dissipation process.
    /// </summary>
    /// <param name="explosiveMassKG">The mass of the explosive in kilograms.</param>
    /// <param name="projectileDiameterCM">The diameter of the projectile in centimeters, used to calculate the breach size.</param>
    public void Explosion(float explosiveMassKG, float projectileDiameterCM)
    {
        // Calculate initial temperature and pressure spike (simple linear relationship for simulation purposes)
        float tempSpike = explosiveMassKG * 150.0f; // 1kg explosive causes ~150°C spike
        float pressureSpike = explosiveMassKG * 50.0f; // 1kg explosive causes ~50kPa spike

        // Apply a fraction of the temperature spike as an impulse.
        temperature += tempSpike * explosionHeatTransferEfficiency;
        pressure += pressureSpike;

        // Simulate toxic gas from the explosion
        toxicGasPercentage = Mathf.Min(100, toxicGasPercentage + (explosiveMassKG * 100.0f));
        cleanAirPercentage = 100 - toxicGasPercentage;

        // Add a new breach
        breachDiameters.Add(projectileDiameterCM);
        currentDissipationTime = dissipationTime;
        
        Debug.Log($"Explosion detected! Mass: {explosiveMassKG}kg. New Breach Diameter: {projectileDiameterCM}cm. Total breaches: {breachDiameters.Count}.");
    }
    
    /// <summary>
    /// Handles the dissipation of explosion effects over time.
    /// This includes cooling, pressure normalization, and air mixture exchange.
    /// </summary>
    private void DissipateExplosionEffects()
    {
        if (currentDissipationTime <= 0) return;

        // Calculate the total breach area from all holes
        float totalBreachArea = CalculateTotalBreachArea();

        // --- Temperature Dissipation (Newton's Law of Cooling) ---
        // The cooling constant 'k' is now influenced by the total breach area and pressure.
        float k = (0.01f + totalBreachArea * 0.1f) * (pressure / 101.3f);
        float coolingRate = k * (temperature - outsideTemperature);
        temperature -= coolingRate * Time.fixedDeltaTime;

        // --- Pressure Dissipation ---
        // Pressure normalizes based on the size of the total breach area.
        float pressureDissipationRate = totalBreachArea * 500f; // Tunable value
        float pressureDiff = pressure - 101.3f;
        pressure -= pressureDissipationRate * pressureDiff * Time.fixedDeltaTime;

        // --- Air Mixture Dissipation ---
        // Toxic gas is exchanged for clean air through the total breach area, but only if the ventilation is off.
        if (!isVentilationOn)
        {
            // The rate of exchange is now also proportional to the amount of toxic gas.
            float airExchangeRate = (totalBreachArea / 0.01f) * Mathf.Abs(pressureDiff / 101.3f);
            float toxicGasDissipation = airExchangeRate * (toxicGasPercentage / 100f) * Time.fixedDeltaTime;
            toxicGasPercentage = Mathf.Max(0, toxicGasPercentage - toxicGasDissipation);
            cleanAirPercentage = 100 - toxicGasPercentage;
        }

        // Update dissipation timer
        currentDissipationTime -= Time.fixedDeltaTime;
        if (currentDissipationTime <= 0)
        {
            currentDissipationTime = 0;
            // Clear the list of breaches only if canSealBreaches is true.
            if (canSealBreaches)
            {
                breachDiameters.Clear();
                Debug.Log("Explosion effects have dissipated. Normal operations resumed. All breaches are now sealed.");
            }
            else
            {
                Debug.Log("Explosion effects have dissipated. Breaches remain open.");
            }
        }
    }
    
    // Helper function to calculate the total breach area from all diameters.
    private float CalculateTotalBreachArea()
    {
        float totalArea = 0.0f;
        foreach (float diameter in breachDiameters)
        {
            float radius = diameter / 200.0f; // Convert cm to meters
            totalArea += Mathf.PI * radius * radius;
        }
        return totalArea;
    }

    // --- Method to check for critical conditions and log a warning ---
    private void CheckForWarnings()
    {
        if (cleanAirPercentage < 20 && !warningLogged)
        {
            Debug.LogWarning("WARNING: Air quality is critically low! Immediate action required.");
            warningLogged = true;
        }
        else if (cleanAirPercentage > 20 && warningLogged)
        {
            warningLogged = false;
        }
    }

    /// <summary>
    /// Calculates the equivalent mass of TNT (in kg) based on kinetic energy.
    /// This can be used to simulate the "explosion" effect of a non-explosive projectile.
    /// </summary>
    /// <param name="massKG">The mass of the projectile in kilograms.</param>
    /// <param name="velocityMPS">The velocity of the projectile in meters per second.</param>
    /// <returns>The equivalent mass of TNT in kilograms.</returns>
    public float CalculateTNTEquivalent(float massKG, float velocityMPS)
    {
        // Arbitrary constant for the energy density of TNT in Joules per kilogram.
        // A commonly cited value is 4.184 x 10^6 J/kg.
        const float tntEnergyDensity = 4.184e6f; 

        // Calculate the kinetic energy of the projectile.
        float kineticEnergy = 0.5f * massKG * Mathf.Pow(velocityMPS, 2);

        // Correlate kinetic energy to an equivalent mass of TNT.
        float tntEquivalent = kineticEnergy / tntEnergyDensity;

        return tntEquivalent;
    }

    // --- Public Methods to Call from other scripts ---
    public void ToggleHeater()
    {
        isHeaterOn = !isHeaterOn;
        Debug.Log($"Heater turned {(isHeaterOn ? "ON" : "OFF")}.");
    }

    public void ToggleVentilation()
    {
        isVentilationOn = !isVentilationOn;
        Debug.Log($"Ventilation turned {(isVentilationOn ? "ON" : "OFF")}.");
    }

    public void AddToxicGas(float amount = 20.0f)
    {
        cleanAirPercentage = Mathf.Max(0, cleanAirPercentage - amount);
        Debug.Log($"Toxic gas introduced. Clean air is now at {cleanAirPercentage:F0}%.");
    }

    public void PurgeWithCleanAir(float amount = 40.0f)
    {
        if (isVentilationOn)
        {
            cleanAirPercentage = Mathf.Min(100, cleanAirPercentage + amount);
            Debug.Log($"Purging with clean air. Clean air is now at {cleanAirPercentage:F0}%.");
        }
        else
        {
            Debug.LogWarning("Purge system failed: Ventilation is off.");
        }
    }

    // --- Method to set outside temperature from a slider UI ---
    public void SetOutsideTemperature(float newTemp)
    {
        outsideTemperature = newTemp;
    }
}
