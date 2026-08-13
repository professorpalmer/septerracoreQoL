using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class SepterraSteamEntry
{
    [STAThread]
    private static int Main()
    {
        string gameDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string albeoris = Path.Combine(gameDir, "Launcher", "Septerra.exe");
        string engine = Path.Combine(gameDir, "septerra.bin");

        if (!File.Exists(albeoris))
        {
            MessageBox.Show(
                "Missing Launcher\\Septerra.exe. Re-run scripts\\deploy-to-steam.ps1.",
                "Septerra QoL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        if (!File.Exists(engine))
        {
            MessageBox.Show(
                "Missing septerra.bin (original game engine). Re-run scripts\\deploy-to-steam.ps1.",
                "Septerra QoL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        var start = new ProcessStartInfo
        {
            FileName = albeoris,
            Arguments = "run . -r",
            WorkingDirectory = gameDir,
            UseShellExecute = true
        };

        try
        {
            using (Process process = Process.Start(start))
            {
                if (process == null)
                    return 2;
                process.WaitForExit();
                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Septerra QoL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 3;
        }
    }
}
