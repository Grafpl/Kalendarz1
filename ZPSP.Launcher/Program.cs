using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZPSP.Launcher;

internal static class Program
{
    // ====================================================================
    //  KONFIGURACJA — gdzie szukać aktualnej wersji ZPSP na QNAP
    // ====================================================================
    private const string QnapReleasePath = @"\\192.168.0.170\Install\Kalendarz1L\Release";
    private const string QnapAssetsPath = @"\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets";
    private const string LocalAppFolderName = "ZPSP";
    private const string MainExeName = "Kalendarz1.exe";

    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            // Lokalna lokalizacja docelowa: %LOCALAPPDATA%\ZPSP\
            var localDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                LocalAppFolderName);
            Directory.CreateDirectory(localDir);

            var remoteExe = Path.Combine(QnapReleasePath, MainExeName);
            var localExe = Path.Combine(localDir, MainExeName);

            // 1. Sprawdź dostępność QNAP
            if (!File.Exists(remoteExe))
            {
                // Jeśli na QNAP nie ma, ale lokalna kopia istnieje — odpal lokalną (offline mode)
                if (File.Exists(localExe))
                {
                    var result = MessageBox.Show(
                        $"Nie można połączyć się z dyskiem sieciowym:\n{QnapReleasePath}\n\n" +
                        $"Czy uruchomić ZPSP w trybie offline (poprzednia wersja)?",
                        "ZPSP — Brak sieci",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.Yes)
                    {
                        return StartLocal(localExe, localDir, args);
                    }
                    return 1;
                }

                MessageBox.Show(
                    $"Nie można połączyć się z dyskiem sieciowym ZPSP:\n{QnapReleasePath}\n\n" +
                    $"Sprawdź:\n" +
                    $" • Czy jesteś w sieci firmowej\n" +
                    $" • Czy masz dostęp do QNAP (192.168.0.170)\n\n" +
                    $"Jeśli problem się powtarza — zadzwoń do Sergiusza.",
                    "ZPSP — Brak połączenia",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            // 2. Czy potrzebna aktualizacja? FAST-CHECK przez VERSION.txt (1 plik vs 1100)
            //    Jeśli VERSION.txt zgadza się, pomijamy całą iterację — start <1 sek.
            bool needUpdate = !File.Exists(localExe);
            if (!needUpdate)
            {
                needUpdate = VersionMismatch(QnapReleasePath, localDir);
            }

            // 3. Aktualizacja (jeśli trzeba)
            if (needUpdate)
            {
                // Sprawdź czy nie ma uruchomionej aplikacji (przed pokazaniem splash)
                var runningPids = GetRunningPids(localExe);
                if (runningPids.Count > 0)
                {
                    var pidsList = string.Join(", ", runningPids);
                    var result = MessageBox.Show(
                        $"ZPSP jest obecnie uruchomiona (PID: {pidsList}).\n\n" +
                        $"Aby zaktualizować — zamknij wszystkie okna ZPSP\n" +
                        $"(albo Task Manager → znajdź 'Kalendarz1' → End Task).\n\n" +
                        $"Czy uruchomić obecną wersję (bez aktualizacji)?",
                        "ZPSP — Aktualizacja niemożliwa",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.Yes)
                    {
                        return StartLocal(localExe, localDir, args);
                    }
                    return 1;
                }

                // Sync Assets (logo + memy) z QNAP do lokalnego cache.
                // Robione przed pokazaniem splashu — szybkie, kilka małych plików.
                var localAssetsDir = Path.Combine(localDir, "Assets");
                SyncAssetsSafe(QnapAssetsPath, localAssetsDir);

                var splash = new UpdateSplash(localAssetsDir);
                splash.Show();
                Application.DoEvents();

                try
                {
                    CopyDirectoryWithProgress(QnapReleasePath, localDir, splash);
                    splash.SetCompleting();
                    System.Threading.Thread.Sleep(600); // krótka chwila żeby user zobaczył "gotowe"
                }
                catch (Exception ex)
                {
                    splash.Close();
                    MessageBox.Show(
                        $"Błąd aktualizacji ZPSP:\n{ex.Message}\n\n" +
                        $"Zadzwoń do Sergiusza.",
                        "ZPSP — Błąd aktualizacji",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }

                splash.Close();
                splash.Dispose();
            }

            // 4. Self-update launchera: jeśli w Release przyszedł __ZPSP_NEW.exe i jest nowszy
            //    niż obecnie uruchomiony launcher → uruchom batch który czeka 2 sek i podmienia
            TryScheduleSelfUpdate(localDir);

            // 5. Cleanup po poprzednim self-update (jeśli zostawiło ZPSP.exe.old)
            TryCleanupOldLauncher(localDir);

            // 6. Uruchom lokalną wersję
            return StartLocal(localExe, localDir, args);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nieoczekiwany błąd launchera:\n{ex.Message}\n\n{ex.StackTrace}",
                "ZPSP — Błąd",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int StartLocal(string exePath, string workDir, string[] args)
    {
        if (!File.Exists(exePath))
        {
            MessageBox.Show(
                $"Brak pliku wykonywalnego:\n{exePath}",
                "ZPSP — Błąd",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workDir,
            UseShellExecute = false
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        try
        {
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd uruchomienia: {ex.Message}", "ZPSP",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static List<int> GetRunningPids(string exePath)
    {
        var pids = new List<int>();
        try
        {
            var procName = Path.GetFileNameWithoutExtension(exePath);
            if (string.IsNullOrEmpty(procName)) return pids;

            var processes = Process.GetProcessesByName(procName);
            try
            {
                foreach (var p in processes)
                {
                    try
                    {
                        var mainModule = p.MainModule;
                        if (mainModule != null &&
                            string.Equals(mainModule.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            pids.Add(p.Id);
                        }
                    }
                    catch { /* Access denied - skip */ }
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
        catch { }
        return pids;
    }

    private static bool IsRunning(string exePath)
    {
        // Sprawdzenie po nazwie procesu (niezawodne, nie myli sie z antywirusem/syncem)
        try
        {
            var procName = Path.GetFileNameWithoutExtension(exePath);
            if (string.IsNullOrEmpty(procName)) return false;

            var processes = Process.GetProcessesByName(procName);
            try
            {
                foreach (var p in processes)
                {
                    try
                    {
                        var mainModule = p.MainModule;
                        if (mainModule != null &&
                            string.Equals(mainModule.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Access denied (np. proces wlasciciela innego usera) - pomin
                    }
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    try { p.Dispose(); } catch { }
                }
            }
            return false;
        }
        catch
        {
            // Jesli sprawdzenie sie wywali — pozwol skopiowac (fail-open)
            return false;
        }
    }

    /// <summary>
    /// Synchronizuje Assets (logo + memy) z QNAP do lokalu. Best-effort:
    /// gdyby QNAP nie odpowiadał — splash pokaże placeholder, nie blokuje aktualizacji.
    /// Małe pliki, szybkie kopiowanie (kilka MB total).
    /// </summary>
    private static void SyncAssetsSafe(string remoteDir, string localDir)
    {
        try
        {
            if (!Directory.Exists(remoteDir)) return;
            Directory.CreateDirectory(localDir);

            // Skopiuj wszystkie pliki rekursywnie - tylko nowe lub zmienione
            foreach (var file in Directory.EnumerateFiles(remoteDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(remoteDir, file);
                var targetPath = Path.Combine(localDir, relative);
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                bool copy = !File.Exists(targetPath);
                if (!copy)
                {
                    var srcInfo = new FileInfo(file);
                    var dstInfo = new FileInfo(targetPath);
                    copy = srcInfo.Length != dstInfo.Length ||
                           srcInfo.LastWriteTimeUtc > dstInfo.LastWriteTimeUtc;
                }

                if (copy)
                {
                    try { File.Copy(file, targetPath, overwrite: true); }
                    catch { /* skip locked */ }
                }
            }

            // Posprzątaj memy które zostały skasowane na QNAP (sync = mirror)
            var localMemes = Path.Combine(localDir, "memes");
            var remoteMemes = Path.Combine(remoteDir, "memes");
            if (Directory.Exists(localMemes) && Directory.Exists(remoteMemes))
            {
                var remoteFiles = Directory.EnumerateFiles(remoteMemes)
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var localFile in Directory.EnumerateFiles(localMemes))
                {
                    var name = Path.GetFileName(localFile);
                    if (name != null && !remoteFiles.Contains(name))
                    {
                        try { File.Delete(localFile); } catch { }
                    }
                }
            }
        }
        catch
        {
            // Best-effort - nie blokujemy aktualizacji jeśli Assets się nie skopiuje
        }
    }

    /// <summary>
    /// SELF-UPDATE: jeśli w lokalnym folderze przyszedł __ZPSP_NEW.exe (skopiowany z Release)
    /// i jest NOWSZY niż obecnie uruchomiony launcher → tworzy batch który czeka 2 sek
    /// (czas na start Kalendarz1.exe i zakończenie tego launchera), a potem podmienia ZPSP.exe.
    /// Następne uruchomienie skrótu = nowy launcher.
    /// </summary>
    private static void TryScheduleSelfUpdate(string localDir)
    {
        try
        {
            var newLauncherPath = Path.Combine(localDir, "__ZPSP_NEW.exe");
            if (!File.Exists(newLauncherPath)) return;

            // Aktualnie uruchomiony launcher
            var currentLauncherPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentLauncherPath) || !File.Exists(currentLauncherPath)) return;

            var newInfo = new FileInfo(newLauncherPath);
            var currentInfo = new FileInfo(currentLauncherPath);

            // Jeśli __ZPSP_NEW.exe NIE jest nowszy niż obecny → nie podmieniamy
            // (np. ten sam launcher = już zaktualizowany)
            if (newInfo.LastWriteTimeUtc <= currentInfo.LastWriteTimeUtc ||
                newInfo.Length == currentInfo.Length)
            {
                // Jest taki sam — usuń __ZPSP_NEW.exe żeby nie zaśmiecać
                try { File.Delete(newLauncherPath); } catch { }
                return;
            }

            // Stwórz batch script w temp
            var tempBat = Path.Combine(Path.GetTempPath(), $"zpsp_selfupdate_{Guid.NewGuid():N}.cmd");
            var scriptContent =
                "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                $"move /Y \"{currentLauncherPath}\" \"{currentLauncherPath}.old\" >nul 2>&1\r\n" +
                $"move /Y \"{newLauncherPath}\" \"{currentLauncherPath}\" >nul 2>&1\r\n" +
                "(goto) 2>nul & del \"%~f0\"\r\n";
            File.WriteAllText(tempBat, scriptContent);

            // Uruchom w tle, bez okna
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{tempBat}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch
        {
            // Self-update jest "best effort" — nie blokujemy startu jeśli coś się posypie
        }
    }

    /// <summary>
    /// Cleanup po poprzednim self-update — kasuje ZPSP.exe.old gdyby został.
    /// </summary>
    private static void TryCleanupOldLauncher(string localDir)
    {
        try
        {
            var oldPath = Path.Combine(localDir, "ZPSP.exe.old");
            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// FAST-CHECK przez VERSION.txt. Jeśli plik na QNAP i lokalna kopia mają identyczną
    /// zawartość VERSION.txt — wersja jest zgodna i NIE potrzeba aktualizacji.
    /// Sprawdzenie 1 małego pliku zamiast iteracji po 1100 plikach przez VPN.
    /// </summary>
    private static bool VersionMismatch(string remoteDir, string localDir)
    {
        var remoteVersionPath = Path.Combine(remoteDir, "VERSION.txt");
        var localVersionPath = Path.Combine(localDir, "VERSION.txt");

        try
        {
            // Jeśli na QNAP nie ma VERSION.txt → wymuszamy aktualizację (fallback do starej logiki)
            if (!File.Exists(remoteVersionPath)) return true;

            // Jeśli lokalnie nie ma VERSION.txt → na pewno aktualizacja
            if (!File.Exists(localVersionPath)) return true;

            var remoteVer = File.ReadAllText(remoteVersionPath).Trim();
            var localVer = File.ReadAllText(localVersionPath).Trim();

            // Wersje różne → aktualizacja
            return !string.Equals(remoteVer, localVer, StringComparison.Ordinal);
        }
        catch
        {
            // Błąd odczytu — bezpiecznie wymuś aktualizację
            return true;
        }
    }

    /// <summary>
    /// Kopiowanie z paskiem postępu — PARALLEL (8 wątków) z fail-safe na blokady plików.
    /// 2-5× szybsze niż sekwencyjne przy VPN.
    /// </summary>
    private static void CopyDirectoryWithProgress(string source, string target, UpdateSplash splash)
    {
        Directory.CreateDirectory(target);

        // 1. Zlicz pliki do skopiowania (porównanie size + date — pomiń identyczne)
        var allSourceFiles = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToList();
        var filesToCopy = new List<string>();

        foreach (var file in allSourceFiles)
        {
            var relative = Path.GetRelativePath(source, file);
            var targetPath = Path.Combine(target, relative);

            if (!File.Exists(targetPath))
            {
                filesToCopy.Add(file);
                continue;
            }

            var srcInfo = new FileInfo(file);
            var dstInfo = new FileInfo(targetPath);
            if (srcInfo.Length != dstInfo.Length ||
                srcInfo.LastWriteTimeUtc > dstInfo.LastWriteTimeUtc)
            {
                filesToCopy.Add(file);
            }
        }

        if (filesToCopy.Count == 0)
        {
            splash.SetTotal(1);
            splash.UpdateProgress(1, "Wszystkie pliki aktualne");
            return;
        }

        splash.SetTotal(filesToCopy.Count);

        // 2. Kopiuj PARALLEL (8 wątków, idealne dla VPN/SMB)
        int copiedCount = 0;
        var copiedLock = new object();

        var parallelOptions = new System.Threading.Tasks.ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        System.Threading.Tasks.Parallel.ForEach(filesToCopy, parallelOptions, file =>
        {
            var relative = Path.GetRelativePath(source, file);
            var targetPath = Path.Combine(target, relative);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            try
            {
                File.Copy(file, targetPath, overwrite: true);
            }
            catch (IOException)
            {
                // Plik zablokowany — pomiń (np. inna instancja appki)
            }

            int currentCount;
            lock (copiedLock)
            {
                copiedCount++;
                currentCount = copiedCount;
            }

            // Aktualizuj UI co plik (BeginInvoke - thread-safe)
            splash.UpdateProgress(currentCount, file);
        });
    }
}
