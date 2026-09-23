using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ArcLight;

namespace ArcLight.Tests
{
    internal static class SettingsTests
    {
        private const string SavedCity = "Erzurum (Türkiye)";
        private static string runRoot;
        private static int passed;
        private static int failed;

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "--write")
                {
                    NonDefault().SaveTo(args[1]);
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--read")
                {
                    AssertSavedValues(Settings.LoadFrom(args[1]));
                    return 0;
                }

                runRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
                Directory.CreateDirectory(runRoot);
                Run("UTF-8 city and all preferences survive separate processes", CrossProcessRoundTrip);
                Run("complete legacy .tmp recovers missing primary", RecoverOnlyTemporary);
                Run("empty primary recovers complete backup", RecoverEmptyPrimary);
                Run("corrupt primary recovers complete backup", RecoverCorruptPrimary);
                Run("partial primary yields to complete backup", RecoverPartialPrimary);
                Run("corrupt primary recovers complete temporary file", RecoverCorruptWithTemporary);
                Run("interrupted pre-commit write retains previous primary", InterruptedWriteRetainsPrimary);
                Run("locked destination rejects save and preserves original bytes", LockedDestinationRetainsPrimary);
                Run("second successful save replaces all fields consistently", SecondSaveRoundTrip);
                Run("malformed values preserve defaults and valid city", MalformedValuesPreserveDefaults);
                Run("times outside one day are rejected", InvalidTimesAreRejected);
                Run("out-of-range numeric fields stay inside supported ranges", NumericBounds);
                Run("legacy partial config salvages independently valid fields", PartialConfigSalvage);
                Run("incomplete recovery file cannot replace valid primary", IncompleteTemporaryIgnored);
                Run("missing files return standard defaults", MissingReturnsDefaults);
                Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed");
                Console.WriteLine("ISOLATED FIXTURES: " + runRoot);
                return failed == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 2;
            }
        }

        private static void Run(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS: " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL: " + name + " -- " + ex.Message); }
        }

        private static string Fixture(string name)
        {
            string folder = Path.Combine(runRoot, name);
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "ayarlar.cfg");
        }

        private static Settings NonDefault()
        {
            return new Settings
            {
                IsEnabled = false,
                Kelvin = 4500,
                Brightness = 77,
                AutoStart = true,
                MinimizeToTray = false,
                Language = "en",
                ScheduleMode = 1,
                CityName = SavedCity,
                CustomStartTime = new TimeSpan(22, 15, 0),
                CustomEndTime = new TimeSpan(6, 45, 0)
            };
        }

        private static string CompleteFixture()
        {
            return "IsEnabled=False\nKelvin=4500\nBrightness=77\nAutoStart=True\nMinimizeToTray=False\nLanguage=en\nScheduleMode=1\nCityName=" + SavedCity + "\nCustomStartTime=22:15\nCustomEndTime=06:45\n";
        }

        private static void Write(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static void CrossProcessRoundTrip()
        {
            string path = Fixture("cross-process");
            Child("--write", path);
            string persisted = File.ReadAllText(path, Encoding.UTF8);
            Assert(persisted.Contains("CityName=" + SavedCity), "saved UTF-8 city was missing or altered");
            Child("--read", path);
        }

        private static void Child(string mode, string path)
        {
            var info = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = mode + " \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process child = Process.Start(info))
            {
                if (!child.WaitForExit(10000))
                {
                    child.Kill();
                    throw new Exception("isolated test child did not exit in 10 seconds");
                }
                string stderr = child.StandardError.ReadToEnd();
                Assert(child.ExitCode == 0, "isolated child exited " + child.ExitCode + ": " + stderr);
            }
        }

        private static void RecoverOnlyTemporary()
        {
            string path = Fixture("only-tmp");
            Write(path + ".tmp", CompleteFixture());
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void RecoverEmptyPrimary()
        {
            string path = Fixture("empty-primary");
            Write(path, "");
            Write(path + ".bak", CompleteFixture());
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void RecoverCorruptPrimary()
        {
            string path = Fixture("corrupt-primary");
            Write(path, "broken incomplete document without any settings\0");
            Write(path + ".bak", CompleteFixture());
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void RecoverPartialPrimary()
        {
            string path = Fixture("partial-primary-backup");
            Write(path, "CityName=Ankara (Türkiye)\nBrightness=45\n");
            Write(path + ".bak", CompleteFixture());
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void RecoverCorruptWithTemporary()
        {
            string path = Fixture("corrupt-primary-tmp");
            Write(path, "unreadable settings");
            Write(path + ".tmp", CompleteFixture());
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void InterruptedWriteRetainsPrimary()
        {
            string path = Fixture("interrupted-before-commit");
            NonDefault().SaveTo(path);
            byte[] original = File.ReadAllBytes(path);
            Write(path + ".tmp", CompleteFixture().Replace(SavedCity, "Ankara (Türkiye)"));
            AssertSavedValues(Settings.LoadFrom(path));
            AssertBytes(original, File.ReadAllBytes(path), "loading leftover temp changed the committed primary");
        }

        private static void LockedDestinationRetainsPrimary()
        {
            string path = Fixture("locked-destination");
            NonDefault().SaveTo(path);
            byte[] original = File.ReadAllBytes(path);
            Settings changed = NonDefault();
            changed.CityName = "Ankara (Türkiye)";
            bool rejected = false;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { changed.SaveTo(path); }
                catch (IOException) { rejected = true; }
                catch (UnauthorizedAccessException) { rejected = true; }
            }
            Assert(rejected, "save to locked primary should report an error");
            AssertBytes(original, File.ReadAllBytes(path), "failed save altered or removed original config");
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void SecondSaveRoundTrip()
        {
            string path = Fixture("second-save");
            new Settings().SaveTo(path);
            NonDefault().SaveTo(path);
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void MalformedValuesPreserveDefaults()
        {
            string path = Fixture("malformed-fields");
            Write(path, "IsEnabled=not-a-bool\nKelvin=bad\nBrightness=bad\nAutoStart=bad\nMinimizeToTray=bad\nLanguage=tr\nScheduleMode=bad\nCityName=" + SavedCity + "\nCustomStartTime=bad\nCustomEndTime=bad\n");
            Settings actual = Settings.LoadFrom(path);
            Settings defaults = new Settings();
            Assert(actual.IsEnabled == defaults.IsEnabled, "bad IsEnabled replaced default");
            Assert(actual.Kelvin == defaults.Kelvin, "bad Kelvin replaced default");
            Assert(actual.Brightness == defaults.Brightness, "bad Brightness replaced default");
            Assert(actual.AutoStart == defaults.AutoStart, "bad AutoStart replaced default");
            Assert(actual.MinimizeToTray == defaults.MinimizeToTray, "bad MinimizeToTray replaced default");
            Assert(actual.ScheduleMode == defaults.ScheduleMode, "bad ScheduleMode replaced default");
            Assert(actual.CustomStartTime == defaults.CustomStartTime, "bad start time replaced default");
            Assert(actual.CustomEndTime == defaults.CustomEndTime, "bad end time replaced default");
            Assert(actual.CityName == SavedCity, "one bad field discarded valid city");
        }

        private static void InvalidTimesAreRejected()
        {
            string[] invalid = { "25:00", "1.00:00:00", "-01:00", "99999999999999" };
            for (int i = 0; i < invalid.Length; i++)
            {
                string path = Fixture("invalid-times-" + i);
                Write(path, CompleteFixture().Replace("22:15", invalid[i]).Replace("06:45", invalid[i]));
                Settings actual = Settings.LoadFrom(path);
                Settings defaults = new Settings();
                Assert(actual.CustomStartTime == defaults.CustomStartTime, "invalid start accepted: " + invalid[i]);
                Assert(actual.CustomEndTime == defaults.CustomEndTime, "invalid end accepted: " + invalid[i]);
                Assert(actual.CityName == SavedCity, "invalid time discarded valid city");
            }
        }

        private static void NumericBounds()
        {
            string path = Fixture("numeric-bounds");
            Write(path, CompleteFixture().Replace("Kelvin=4500", "Kelvin=-500").Replace("Brightness=77", "Brightness=999999").Replace("ScheduleMode=1", "ScheduleMode=-99"));
            Settings actual = Settings.LoadFrom(path);
            Assert(actual.Kelvin >= 1800 && actual.Kelvin <= 6500, "Kelvin outside slider bounds");
            Assert(actual.Brightness >= 25 && actual.Brightness <= 100, "brightness outside slider bounds");
            Assert(actual.ScheduleMode >= 0 && actual.ScheduleMode <= 2, "unsupported schedule mode");
        }

        private static void PartialConfigSalvage()
        {
            string path = Fixture("partial-legacy");
            Write(path, "CityName=" + SavedCity + "\nBrightness=75\n");
            Settings actual = Settings.LoadFrom(path);
            Settings defaults = new Settings();
            Assert(actual.CityName == SavedCity, "legacy city was discarded");
            Assert(actual.Brightness == 75, "legacy brightness was discarded");
            Assert(actual.Kelvin == defaults.Kelvin, "missing Kelvin lost default");
            Assert(actual.IsEnabled == defaults.IsEnabled, "missing IsEnabled lost default");
            Assert(actual.CustomStartTime == defaults.CustomStartTime, "missing time lost default");
        }

        private static void IncompleteTemporaryIgnored()
        {
            string path = Fixture("incomplete-tmp");
            NonDefault().SaveTo(path);
            Write(path + ".tmp", "CityName=Ankara (Türkiye)\n");
            AssertSavedValues(Settings.LoadFrom(path));
        }

        private static void MissingReturnsDefaults()
        {
            Settings actual = Settings.LoadFrom(Fixture("missing"));
            Settings expected = new Settings();
            Assert(actual.CityName == expected.CityName, "default city changed");
            Assert(actual.CustomStartTime == expected.CustomStartTime, "default start changed");
            Assert(actual.CustomEndTime == expected.CustomEndTime, "default end changed");
            Assert(actual.Kelvin == expected.Kelvin, "default Kelvin changed");
            Assert(actual.Brightness == expected.Brightness, "default brightness changed");
        }

        private static void AssertSavedValues(Settings actual)
        {
            Settings expected = NonDefault();
            Assert(actual.CityName == expected.CityName, "city: expected " + expected.CityName + ", got " + actual.CityName);
            Assert(actual.IsEnabled == expected.IsEnabled, "IsEnabled not preserved");
            Assert(actual.Kelvin == expected.Kelvin, "Kelvin not preserved");
            Assert(actual.Brightness == expected.Brightness, "Brightness not preserved");
            Assert(actual.AutoStart == expected.AutoStart, "AutoStart not preserved");
            Assert(actual.MinimizeToTray == expected.MinimizeToTray, "MinimizeToTray not preserved");
            Assert(actual.Language == expected.Language, "Language not preserved");
            Assert(actual.ScheduleMode == expected.ScheduleMode, "ScheduleMode not preserved");
            Assert(actual.CustomStartTime == expected.CustomStartTime, "CustomStartTime not preserved");
            Assert(actual.CustomEndTime == expected.CustomEndTime, "CustomEndTime not preserved");
        }

        private static void AssertBytes(byte[] expected, byte[] actual, string message)
        {
            Assert(expected.Length == actual.Length, message);
            for (int i = 0; i < expected.Length; i++) Assert(expected[i] == actual[i], message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
