using System;
using System.IO;
using System.Threading.Tasks;
using GTA;

// Lightweight logger to scripts\CarDealerShipMod.log
public static class ModLogger
{
    private static readonly string LogPath = "scripts\\CarDealerShipMod.log";

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory("scripts");
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] --- Logger initialized ---{Environment.NewLine}");

            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    Log("[UnhandledException] " + (ex != null ? ex.ToString() : e.ExceptionObject?.ToString()));
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    Log("[UnobservedTaskException] " + e.Exception.ToString());
                    e.SetObserved();
                }
                catch { }
            };
        }
        catch { /* ignore logging errors */ }
    }

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* ignore logging errors */ }
    }

    public static void LogException(string header, Exception ex)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {header}: {ex}{Environment.NewLine}");
        }
        catch { }
    }
}

// Separate SHVDN script whose only job is to confirm the assembly is active in-game
public class ModActivationProbe : Script
{
    private bool _loggedFirstTick = false;

    public ModActivationProbe()
    {
        try
        {
            ModLogger.Init();
            ModLogger.Log("ModActivationProbe constructed: assembly loaded by SHVDN.");

            // Log basic environment
            try
            {
                ModLogger.Log($"Game Version: {Game.Version}");
                ModLogger.Log($"Scripts folder: {Path.GetFullPath("scripts")}");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Env info error", ex);
            }

            // Check for common dependencies presence in scripts folder
            try
            {
                var scriptsDir = Path.GetFullPath("scripts");
                var lemon = Path.Combine(scriptsDir, "LemonUI.SHVDN3.dll");
                var ifruit = Path.Combine(scriptsDir, "iFruitAddon2.dll");
                ModLogger.Log($"Check deps: LemonUI={(File.Exists(lemon) ? "OK" : "MISSING")}, iFruitAddon2={(File.Exists(ifruit) ? "OK" : "MISSING")}");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Deps check error", ex);
            }

            Tick += OnTick;
            Aborted += OnAborted;
            KeyDown += OnKeyDownSafe;
        }
        catch (Exception ex)
        {
            // avoid crashing the script if logging fails
            ModLogger.LogException("Constructor error", ex);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            if (!_loggedFirstTick)
            {
                _loggedFirstTick = true;
                ModLogger.Log("OnTick reached: scripts are running in-game.");
            }
        }
        catch (Exception ex)
        {
            ModLogger.LogException("OnTick error", ex);
        }
    }

    private void OnKeyDownSafe(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        try
        {
            // reserved for future debug keybinds
        }
        catch (Exception ex)
        {
            ModLogger.LogException("OnKeyDown error", ex);
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        try
        {
            ModLogger.Log("Script aborted (game or SHVDN unloading). Goodbye.");
        }
        catch { }
    }
}
