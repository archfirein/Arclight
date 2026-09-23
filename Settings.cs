using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ArcLight
{
    public class Settings
    {
        public bool IsEnabled = true;
        public int Kelvin = 3400;
        public int Brightness = 90;
        public bool AutoStart = false;
        public bool MinimizeToTray = true;
        public string Language = "tr";
        public int ScheduleMode = 0;
        public string CityName = "İstanbul (Türkiye)";
        public TimeSpan CustomStartTime = new TimeSpan(20, 0, 0);
        public TimeSpan CustomEndTime = new TimeSpan(7, 0, 0);

        private static readonly object SaveLock = new object();

        private static string ConfigPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "ArcLight", "ayarlar.cfg");
            }
        }

        public void Save()
        {
            try { SaveTo(ConfigPath); }
            catch (Exception ex)
            {
                // A failed save must never remove the last complete configuration.
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(ConfigPath), "ayarlar-hata.log"),
                        DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " " + ex + Environment.NewLine);
                }
                catch { }
            }
        }

        internal void SaveTo(string path)
        {
            lock (SaveLock)
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                string temporaryPath = path + ".tmp";
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create,
                    FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine("IsEnabled=" + IsEnabled);
                    writer.WriteLine("Kelvin=" + Kelvin.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("Brightness=" + Brightness.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("AutoStart=" + AutoStart);
                    writer.WriteLine("MinimizeToTray=" + MinimizeToTray);
                    writer.WriteLine("Language=" + Language);
                    writer.WriteLine("ScheduleMode=" + ScheduleMode.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("CityName=" + CityName);
                    writer.WriteLine("CustomStartTime=" + CustomStartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
                    writer.WriteLine("CustomEndTime=" + CustomEndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    Settings previous;
                    // Keep an existing good backup if the main file was damaged.
                    string backupPath = TryRead(path, true, out previous) ? path + ".bak" : null;
                    File.Replace(temporaryPath, path, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
        }

        public static Settings Load()
        {
            return LoadFrom(ConfigPath);
        }

        internal static Settings LoadFrom(string path)
        {
            lock (SaveLock)
            {
                Settings settings;
                if (TryRead(path, true, out settings)) return settings;
                // Older versions deleted the main file before moving .tmp into place.
                if (TryRead(path + ".tmp", true, out settings)) return settings;
                if (TryRead(path + ".bak", true, out settings)) return settings;
                // Retain support for older, incomplete configuration formats.
                if (TryRead(path, false, out settings)) return settings;
                return new Settings();
            }
        }

        private static bool TryRead(string path, bool requireComplete, out Settings settings)
        {
            settings = new Settings();
            int validFields = 0;
            try
            {
                if (!File.Exists(path)) return false;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int separator = line.IndexOf('=');
                    if (separator < 1) continue;
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    bool booleanValue;
                    int integerValue;
                    TimeSpan timeValue;
                    switch (key)
                    {
                        case "IsEnabled":
                            if (bool.TryParse(value, out booleanValue))
                            { settings.IsEnabled = booleanValue; validFields |= 1; }
                            break;
                        case "Kelvin":
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                            { settings.Kelvin = Math.Max(1800, Math.Min(6500, integerValue)); validFields |= 2; }
                            break;
                        case "Brightness":
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                            { settings.Brightness = Math.Max(25, Math.Min(100, integerValue)); validFields |= 4; }
                            break;
                        case "AutoStart":
                            if (bool.TryParse(value, out booleanValue))
                            { settings.AutoStart = booleanValue; validFields |= 8; }
                            break;
                        case "MinimizeToTray":
                            if (bool.TryParse(value, out booleanValue))
                            { settings.MinimizeToTray = booleanValue; validFields |= 16; }
                            break;
                        case "Language":
                            if (Array.IndexOf(new[] { "tr", "en", "es", "fr", "zh", "ja", "ko" }, value) >= 0)
                            { settings.Language = value; validFields |= 32; }
                            break;
                        case "ScheduleMode":
                            if (int.TryParse(value, out integerValue) && integerValue >= 0 && integerValue <= 2)
                            { settings.ScheduleMode = integerValue; validFields |= 64; }
                            break;
                        case "CityName":
                            if (!string.IsNullOrWhiteSpace(value))
                            { settings.CityName = value; validFields |= 128; }
                            break;
                        case "CustomStartTime":
                            if (TryParseTime(value, out timeValue))
                            { settings.CustomStartTime = timeValue; validFields |= 256; }
                            break;
                        case "CustomEndTime":
                            if (TryParseTime(value, out timeValue))
                            { settings.CustomEndTime = timeValue; validFields |= 512; }
                            break;
                    }
                }
                return requireComplete ? validFields == 1023 : validFields != 0;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static bool TryParseTime(string value, out TimeSpan time)
        {
            return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time)
                && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }
    }
}
