using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace FrostGlass
{
    internal sealed class AppSettings
    {
        public int Transparency = 72;        // 10 = strongest tint, 90 = clearest
        public int Clarity = 82;             // 40 = strongest available blur, 100 = completely sharp
        public int AdjustModifiers = 3;      // Ctrl=1, Alt=2, Shift=4; at least two keys
        public bool AlwaysOnTop = true;
        public bool HasLastRect = false;
        public Rectangle LastRect = Rectangle.Empty;

        public static readonly Color TintColor = Color.FromArgb(26, 32, 44);

        public int AccentAlpha
        {
            get
            {
                int transparency = Clamp(Transparency, 10, 90);
                int alpha = 255 - (int)Math.Round(transparency * 255.0 / 100.0);
                return Clamp(alpha, 18, 240);
            }
        }

        public AppSettings Clone()
        {
            AppSettings copy = new AppSettings();
            copy.Transparency = Transparency;
            copy.Clarity = Clarity;
            copy.AdjustModifiers = AdjustModifiers;
            copy.AlwaysOnTop = AlwaysOnTop;
            copy.HasLastRect = HasLastRect;
            copy.LastRect = LastRect;
            return copy;
        }

        public static string DataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FrostGlass");
            }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(DataDirectory, "settings.ini"); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath))
                    return settings;

                string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (key.Equals("Transparency", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            settings.Transparency = Clamp(parsed, 10, 90);
                    }
                    else if (key.Equals("Clarity", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            settings.Clarity = Clamp(parsed, 40, 100);
                    }
                    else if (key.Equals("AdjustModifiers", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            settings.AdjustModifiers = NormalizeAdjustModifiers(parsed);
                    }
                    else if (key.Equals("AlwaysOnTop", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.AlwaysOnTop = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("LastRect", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = value.Split(',');
                        int x, y, w, h;
                        if (parts.Length == 4 &&
                            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) &&
                            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w) &&
                            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) &&
                            w >= 24 && h >= 24)
                        {
                            settings.HasLastRect = true;
                            settings.LastRect = new Rectangle(x, y, w, h);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("读取设置失败", ex);
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 毛玻璃设置");
                sb.AppendLine("Transparency=" + Clamp(Transparency, 10, 90).ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("Clarity=" + Clamp(Clarity, 40, 100).ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("AdjustModifiers=" + NormalizeAdjustModifiers(AdjustModifiers).ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("AlwaysOnTop=" + (AlwaysOnTop ? "1" : "0"));
                if (HasLastRect && LastRect.Width >= 24 && LastRect.Height >= 24)
                {
                    sb.AppendLine("LastRect=" +
                        LastRect.X.ToString(CultureInfo.InvariantCulture) + "," +
                        LastRect.Y.ToString(CultureInfo.InvariantCulture) + "," +
                        LastRect.Width.ToString(CultureInfo.InvariantCulture) + "," +
                        LastRect.Height.ToString(CultureInfo.InvariantCulture));
                }
                File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Write("保存设置失败", ex);
            }
        }

        public static int NormalizeAdjustModifiers(int value)
        {
            int modifiers = value & 7;
            int count = 0;
            if ((modifiers & 1) != 0) count++;
            if ((modifiers & 2) != 0) count++;
            if ((modifiers & 4) != 0) count++;
            return count >= 2 ? modifiers : 3;
        }

        public static string ModifierText(int value)
        {
            int modifiers = NormalizeAdjustModifiers(value);
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            if ((modifiers & 1) != 0) parts.Add("Ctrl");
            if ((modifiers & 2) != 0) parts.Add("Alt");
            if ((modifiers & 4) != 0) parts.Add("Shift");
            return string.Join("+", parts.ToArray());
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    internal static class Log
    {
        public static void Write(string message, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.DataDirectory);
                string path = Path.Combine(AppSettings.DataDirectory, "error.log");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("---- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ----");
                sb.AppendLine(message);
                if (ex != null)
                    sb.AppendLine(ex.ToString());
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
