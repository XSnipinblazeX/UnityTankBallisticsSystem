using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// A custom debugger that logs game events and saves them to a text file.
/// This uses the Singleton pattern to be easily accessible from any script.
/// </summary>
public class CustomDebugger : MonoBehaviour
{
    // The static instance of the class
    public static CustomDebugger Instance { get; private set; }

    // Struct to hold a single log entry
    private struct LogEntry
    {
        public float gameTime;
        public string eventType;
        public string message;
    }

    private List<LogEntry> logEntries = new List<LogEntry>();
    private string logFilePath;

    private void Awake()
    {
        // Singleton implementation
        if (Instance == null)
        {
            Instance = this;
            // Make sure this object isn't destroyed when loading new scenes
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // Set the path for the log file
        logFilePath = Path.Combine(Application.persistentDataPath, "game_log.txt");
        Debug.Log($"Log file path: {logFilePath}");
    }

    /// <summary>
    /// Logs a generic event with a message.
    /// </summary>
    public void LogEvent(string eventType, string message)
    {
        LogEntry newEntry = new LogEntry
        {
            gameTime = Time.time,
            eventType = eventType,
            message = message
        };
        logEntries.Add(newEntry);
    }

    /// <summary>
    /// Logs the initial properties of a shot when the bullet is spawned.
    /// This method expects a custom ShotInfo class to be defined in your project.
    /// </summary>
    public void LogShotInfo(ShotInfo info)
    {
        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Projectile Name: {info.projectileName}");
        messageBuilder.AppendLine($"Spawned at: {info.spawnTime:F2}s");
        messageBuilder.AppendLine($"Spawn Position: {info.spawnPosition}");
        messageBuilder.AppendLine($"Muzzle Velocity: {info.muzzleVelocity:F2}");
        messageBuilder.AppendLine($"Caliber: {info.caliber:F2}, Mass: {info.shellMass:F2}");
        messageBuilder.AppendLine($"Shell Type: {info.shellType}");
        LogEvent("ShotInfo", messageBuilder.ToString());
    }

    /// <summary>
    /// Logs all details of a shot event from a custom class.
    /// This method expects custom ShotInfo and ShotResult classes to be defined in your project.
    /// </summary>
    public void LogShotResult(ShotInfo info, ShotResult result)
    {
        StringBuilder messageBuilder = new StringBuilder();

        // Initial Shot Properties
        messageBuilder.AppendLine($"--- SHOT INFO ---");
        messageBuilder.AppendLine($"Projectile: {info.projectileName}, Type: {info.shellType}");
        messageBuilder.AppendLine($"Spawn Time: {info.spawnTime:F5}s, Muzzle Velocity: {info.muzzleVelocity:F2}");
        messageBuilder.AppendLine($"Caliber: {info.caliber:F2}, Mass: {info.shellMass:F2}");
        messageBuilder.AppendLine();

        // Impact & Result Properties
        messageBuilder.AppendLine("--- IMPACT & RESULT ---");
        messageBuilder.AppendLine($"Impact at {result.impactTime:F2}s, Flight Time: {(Vector3.Distance(info.spawnPosition, result.impactLocation) / info.muzzleVelocity):F5}s");
        messageBuilder.AppendLine($"Impact Location: {result.impactLocation}");
        messageBuilder.AppendLine($"Impact Velocity: {result.impactVelocity}, Angle: {result.impactAngle:F2}");
        messageBuilder.AppendLine($"Kinetic Energy: {result.KineticEnergy:F2}");

        if (result.fragmented)
        {
            messageBuilder.AppendLine($"Fragmented at: {result.timeOfFragment:F2}s");
        }

        // Hit Object Properties
        messageBuilder.AppendLine($"Hit Armor: {result.hitArmor}");
        messageBuilder.AppendLine($"Thickness: {result.thickness:F2}, Effective Thickness: {result.effectiveThickness:F2}, Penetration: {result.penetration:F2}");

        if (result.HasSpalled)
        {
            messageBuilder.AppendLine($"Spalled at: {result.timeOfSpall:F2}s");
        }

        if (result.penetrated)
        {
            messageBuilder.AppendLine($"PENETRATED! Exit Velocity: {result.exitVelocity}");
        }

        if (result.Fused)
        {
            messageBuilder.AppendLine($"Fused at {result.timeOfFuse:F2}s, Fused Distance: {result.fusedDistance:F2}");
        }

        if (result.Detonated)
        {
            messageBuilder.AppendLine($"DETONATED! At {result.timeOfDetonation:F2}s, Point: {result.detonationPoint}");
        }

        // Hit Modules

        LogEvent("ShotResult", messageBuilder.ToString());
    }

    /// <summary>
    /// Saves all log entries to a file. This is automatically called on application quit.
    /// </summary>
    public void SaveLogToFile()
    {
        // Use StringBuilder for efficient string concatenation
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- GAME LOG ---");
        sb.AppendLine($"Saved at: {System.DateTime.Now}");
        sb.AppendLine("------------------");
        sb.AppendLine();

        foreach (var entry in logEntries)
        {
            sb.AppendLine($"[{entry.gameTime:F2}s] - {entry.eventType}:\n{entry.message}");
        }

        // Write the StringBuilder's content to the file
        File.WriteAllText(logFilePath, sb.ToString());
        Debug.Log($"Log saved to {logFilePath}");
    }

    // Called when the application is quitting
    private void OnApplicationQuit()
    {
        SaveLogToFile();
    }
    private void OnDisable()
    {
        SaveLogToFile();
    }
}
