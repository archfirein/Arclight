using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.3.1.0")]
[assembly: AssemblyFileVersion("0.3.1.0")]
namespace ArcLight
{
	public static class GammaController
	{
		public struct RAMP
		{
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
			public ushort[] Red;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
			public ushort[] Green;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
			public ushort[] Blue;
		}

		private static RAMP? _originalRamp = null;
        internal static RAMP? ExpectedRamp;
        private static DateTime _nextRecovery = DateTime.MinValue;
        private static int _recoveryFailures;

        internal static bool RampsMatch(RAMP a, RAMP b)
        {
            if (a.Red == null || a.Green == null || a.Blue == null || b.Red == null || b.Green == null || b.Blue == null) return false;
            if (a.Red.Length != 256 || a.Green.Length != 256 || a.Blue.Length != 256 || b.Red.Length != 256 || b.Green.Length != 256 || b.Blue.Length != 256) return false;
            for (int i = 0; i < 256; i++)
                if (Math.Abs((int)a.Red[i] - b.Red[i]) > 256 || Math.Abs((int)a.Green[i] - b.Green[i]) > 256 || Math.Abs((int)a.Blue[i] - b.Blue[i]) > 256) return false;
            return true;
        }

        private static RAMP? ReadRamp()
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            try
            {
                RAMP ramp = new RAMP { Red = new ushort[256], Green = new ushort[256], Blue = new ushort[256] };
                return GetDeviceGammaRamp(dc, ref ramp) ? (RAMP?)ramp : null;
            }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        private static bool WriteRamp(RAMP ramp)
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return false;
            try { return SetDeviceGammaRamp(dc, ref ramp); }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        public static void EnsureApplied()
        {
            try { Recover(ReadRamp, WriteRamp, DateTime.UtcNow); }
            catch { } // A driver transition must not terminate the UI message loop.
        }

        internal static bool Recover(Func<RAMP?> read, Func<RAMP, bool> write, DateTime now)
        {
            if (!ExpectedRamp.HasValue || now < _nextRecovery) return false;
            RAMP desired = ExpectedRamp.Value;
            RAMP? actual = read();
            if (actual.HasValue && RampsMatch(actual.Value, desired))
            {
                _recoveryFailures = 0;
                return false;
            }
            // Only rewrite on a measured mismatch; unavailable displays are retried later.
            bool restored = false;
            if (actual.HasValue && write(desired))
            {
                RAMP? verified = read();
                restored = verified.HasValue && RampsMatch(verified.Value, desired);
            }
            _recoveryFailures = restored ? 0 : Math.Min(4, _recoveryFailures + 1);
            _nextRecovery = now.AddMilliseconds(restored ? 500 : 500 * (1 << _recoveryFailures));
            return restored;
        }

        internal static void SetExpected(RAMP ramp)
        {
            ExpectedRamp = ramp;
            _nextRecovery = DateTime.MinValue;
            _recoveryFailures = 0;
        }

		[DllImport("gdi32.dll")]
		public static extern bool SetDeviceGammaRamp(IntPtr hdc, ref RAMP lpRamp);

		[DllImport("gdi32.dll")]
		public static extern bool GetDeviceGammaRamp(IntPtr hdc, ref RAMP lpRamp);

		[DllImport("user32.dll")]
		public static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		public static void SaveOriginal()
		{
			try
			{
				IntPtr dC = GetDC(IntPtr.Zero);
				if (!(dC != IntPtr.Zero))
				{
					return;
				}
				try
				{
					RAMP lpRamp = new RAMP
					{
						Red = new ushort[256],
						Green = new ushort[256],
						Blue = new ushort[256]
					};
					if (GetDeviceGammaRamp(dC, ref lpRamp))
					{
						_originalRamp = lpRamp;
					}
				}
				finally
				{
					ReleaseDC(IntPtr.Zero, dC);
				}
			}
			catch
			{
			}
		}

		public static void SetColorTemperatureAndBrightness(int kelvin, int brightnessPercent)
		{
			try
			{
				kelvin = Math.Max(1200, Math.Min(6500, kelvin));
				brightnessPercent = Math.Max(20, Math.Min(100, brightnessPercent));
				double r;
				double g;
				double b;
				KelvinToRgb(kelvin, out r, out g, out b);
				double num = (double)brightnessPercent / 100.0;
				r *= num;
				g *= num;
				b *= num;
				RAMP lpRamp = new RAMP
				{
					Red = new ushort[256],
					Green = new ushort[256],
					Blue = new ushort[256]
				};
				for (int i = 0; i < 256; i++)
				{
					double num2 = (double)i * 256.0;
					lpRamp.Red[i] = (ushort)Math.Max(0, Math.Min(65535, (int)(num2 * r)));
					lpRamp.Green[i] = (ushort)Math.Max(0, Math.Min(65535, (int)(num2 * g)));
					lpRamp.Blue[i] = (ushort)Math.Max(0, Math.Min(65535, (int)(num2 * b)));
				}
                SetExpected(lpRamp);
				IntPtr dC = GetDC(IntPtr.Zero);
				if (dC != IntPtr.Zero)
				{
					try
					{
						SetDeviceGammaRamp(dC, ref lpRamp);
						return;
					}
					finally
					{
						ReleaseDC(IntPtr.Zero, dC);
					}
				}
			}
			catch
			{
			}
		}

		public static void RestoreDefault()
		{
			try
			{
				IntPtr dC = GetDC(IntPtr.Zero);
				if (!(dC != IntPtr.Zero))
				{
					return;
				}
				try
				{
					if (_originalRamp.HasValue)
					{
						RAMP lpRamp = _originalRamp.Value;
						SetDeviceGammaRamp(dC, ref lpRamp);
						return;
					}
					RAMP lpRamp2 = new RAMP
					{
						Red = new ushort[256],
						Green = new ushort[256],
						Blue = new ushort[256]
					};
					for (int i = 0; i < 256; i++)
					{
						ushort num = (ushort)(i * 256);
						lpRamp2.Red[i] = num;
						lpRamp2.Green[i] = num;
						lpRamp2.Blue[i] = num;
					}
					SetDeviceGammaRamp(dC, ref lpRamp2);
				}
				finally
				{
					ReleaseDC(IntPtr.Zero, dC);
				}
			}
			catch
			{
			}
		}

		private static void KelvinToRgb(int kelvin, out double r, out double g, out double b)
		{
			double num = (double)kelvin / 100.0;
			double val;
			double val2;
			double val3;
			if (num <= 66.0)
			{
				val = 255.0;
				val2 = 99.4708025861 * Math.Log(num) - 161.1195681661;
				val3 = ((!(num <= 19.0)) ? (138.5177312231 * Math.Log(num - 10.0) - 305.0447927307) : 0.0);
			}
			else
			{
				val = 329.698727446 * Math.Pow(num - 60.0, -0.1332047592);
				val2 = 288.1221695283 * Math.Pow(num - 60.0, -0.0755148492);
				val3 = 255.0;
			}
			r = Math.Max(0.0, Math.Min(255.0, val)) / 255.0;
			g = Math.Max(0.0, Math.Min(255.0, val2)) / 255.0;
			b = Math.Max(0.0, Math.Min(255.0, val3)) / 255.0;
		}
	}
	public static class Loc
	{
		public static string CurrentLang = "tr";

		private static Dictionary<string, Dictionary<string, string>> _dict = new Dictionary<string, Dictionary<string, string>>
		{
			{
				"tr",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 Işık & Parlaklık" },
					{ "TabLocation", "\ud83d\udccd Lokasyon & Saat" },
					{ "TabSettings", "⚙\ufe0f Ayarlar & Dil" },
					{ "NightModeOn", "\ud83c\udf19 GECE MODU: AÇIK" },
					{ "NightModeOff", "☀\ufe0f GÜNDÜZ MODU: KAPALI" },
					{ "ClickToToggle", "(Tek tıkla veya Ctrl+Alt+N ile)" },
					{ "StatusNight", "Gece Modu devrede. Mavi ışık filtreleniyor." },
					{ "StatusDay", "Ekran standart gün ışığı modunda." },
					{ "ColorTemp", "\ud83d\udd25 Renk Sıcaklığı" },
					{ "Brightness", "☀\ufe0f Ekran Parlaklığı" },
					{ "PresetCandle", "Mum 2200K" },
					{ "PresetNight", "Gece 3400K" },
					{ "PresetSunset", "Akşam 4500K" },
					{ "PresetNormal", "Normal 6500K" },
					{ "DescCandle", "Mum Işığı / Çok Sıcak" },
					{ "DescNight", "Rahat Gece Modu" },
					{ "DescSunset", "Akşam Işığı" },
					{ "DescNormal", "Normal Gün Işığı" },
					{ "SchedTitle", "\ud83d\udccd Akıllı Zamanlama & Lokasyon" },
					{ "ModeSun", "Güneş Takibi (Konuma Göre)" },
					{ "ModeCustom", "Özel Saat Aralığı" },
					{ "ModeManual", "Manuel (Zamanlayıcı Yok)" },
					{ "CitySelect", "Şehir Seçin (7 Bölge & Dünya):" },
					{ "SunriseSunset", "\ud83c\udf05 Gün Doğumu: {0}   |   \ud83c\udf07 Gün Batımı: {1}\n(Güneş saatlerine göre otomatik geçiş yapar)" },
					{ "StartTime", "\ud83c\udf19 Gece Başlangıcı:" },
					{ "EndTime", "☀\ufe0f Gündüz Başlangıcı:" },
					{ "LangTitle", "\ud83c\udf10 Uygulama Dili / Language" },
					{ "AutoStart", "Windows ile başlat" },
					{ "MinimizeTray", "Kapatınca tepsiye küçült" },
					{ "ExitApp", "\ud83d\uded1 ArcLight'tan Çık" },
					{ "TrayToggle", "Gece Modunu Aç / Kapat" },
					{ "TrayOpen", "ArcLight Ayarlarını Aç" },
					{ "TrayExit", "ArcLight'tan Çıkış" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● AKTİF" },
					{ "StatusPillInactive", "○ PASİF" },
					{ "SegmentSun", "☀\ufe0f Güneş" },
					{ "SegmentCustom", "⏱\ufe0f Özel Saat" },
					{ "SegmentManual", "✋ Manuel" },
					{ "SunriseLabel", "\ud83c\udf05 GÜNDOĞUMU" },
					{ "SunsetLabel", "\ud83c\udf07 GÜNBATIMI" },
					{ "ManualNotice", "✋ Manuel Mod devrede.\nEkran renk sıcaklığı ve parlaklık sadece sizin kontrolünüzdedir." }
				}
			},
			{
				"en",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 Light & Brightness" },
					{ "TabLocation", "\ud83d\udccd Location & Schedule" },
					{ "TabSettings", "⚙\ufe0f Settings & Language" },
					{ "NightModeOn", "\ud83c\udf19 NIGHT MODE: ON" },
					{ "NightModeOff", "☀\ufe0f DAY MODE: OFF" },
					{ "ClickToToggle", "(Click or press Ctrl+Alt+N)" },
					{ "StatusNight", "Night Mode is active. Blue light filtered." },
					{ "StatusDay", "Standard daylight mode is active." },
					{ "ColorTemp", "\ud83d\udd25 Color Temperature" },
					{ "Brightness", "☀\ufe0f Screen Brightness" },
					{ "PresetCandle", "Candle 2200K" },
					{ "PresetNight", "Night 3400K" },
					{ "PresetSunset", "Sunset 4500K" },
					{ "PresetNormal", "Normal 6500K" },
					{ "DescCandle", "Candlelight / Very Warm" },
					{ "DescNight", "Cozy Night Mode" },
					{ "DescSunset", "Sunset / Evening Light" },
					{ "DescNormal", "Natural Daylight" },
					{ "SchedTitle", "\ud83d\udccd Smart Schedule & Location" },
					{ "ModeSun", "Sun Tracking (By Location)" },
					{ "ModeCustom", "Custom Time Range" },
					{ "ModeManual", "Manual (No Timer)" },
					{ "CitySelect", "Select City:" },
					{ "SunriseSunset", "\ud83c\udf05 Sunrise: {0}   |   \ud83c\udf07 Sunset: {1}\n(Automatically switches based on solar movement)" },
					{ "StartTime", "\ud83c\udf19 Night Starts:" },
					{ "EndTime", "☀\ufe0f Day Starts:" },
					{ "LangTitle", "\ud83c\udf10 Application Language" },
					{ "AutoStart", "Start with Windows" },
					{ "MinimizeTray", "Minimize to tray on close" },
					{ "ExitApp", "\ud83d\uded1 Exit ArcLight" },
					{ "TrayToggle", "Toggle Night Mode" },
					{ "TrayOpen", "Open ArcLight Settings" },
					{ "TrayExit", "Exit ArcLight" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● ACTIVE" },
					{ "StatusPillInactive", "○ INACTIVE" },
					{ "SegmentSun", "☀\ufe0f Solar" },
					{ "SegmentCustom", "⏱\ufe0f Custom" },
					{ "SegmentManual", "✋ Manual" },
					{ "SunriseLabel", "\ud83c\udf05 SUNRISE" },
					{ "SunsetLabel", "\ud83c\udf07 SUNSET" },
					{ "ManualNotice", "✋ Manual Mode active.\nScreen lighting is fully controlled by you. Timer is disabled." }
				}
			},
			{
				"es",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 Luz y Brillo" },
					{ "TabLocation", "\ud83d\udccd Ubicación y Horario" },
					{ "TabSettings", "⚙\ufe0f Ajustes e Idioma" },
					{ "NightModeOn", "\ud83c\udf19 MODO NOCHE: ACTIVADO" },
					{ "NightModeOff", "☀\ufe0f MODO DÍA: DESACTIVADO" },
					{ "ClickToToggle", "(Clic o Ctrl+Alt+N)" },
					{ "StatusNight", "Modo noche activo. Luz azul filtrada." },
					{ "StatusDay", "Modo de luz diurna estándar activo." },
					{ "ColorTemp", "\ud83d\udd25 Temperatura de Color" },
					{ "Brightness", "☀\ufe0f Brillo de Pantalla" },
					{ "PresetCandle", "Vela 2200K" },
					{ "PresetNight", "Noche 3400K" },
					{ "PresetSunset", "Tarde 4500K" },
					{ "PresetNormal", "Normal 6500K" },
					{ "DescCandle", "Luz de Vela / Muy Cálido" },
					{ "DescNight", "Modo Noche Cómodo" },
					{ "DescSunset", "Luz de Atardecer" },
					{ "DescNormal", "Luz Diurna Natural" },
					{ "SchedTitle", "\ud83d\udccd Horario Inteligente y Ubicación" },
					{ "ModeSun", "Seguimiento Solar (Ubicación)" },
					{ "ModeCustom", "Horas Personalizadas" },
					{ "ModeManual", "Manual (Sin Temporizador)" },
					{ "CitySelect", "Seleccionar Ciudad:" },
					{ "SunriseSunset", "\ud83c\udf05 Amanecer: {0}   |   \ud83c\udf07 Atardecer: {1}\n(Cambia automáticamente con la posición del sol)" },
					{ "StartTime", "\ud83c\udf19 Inicio Noche:" },
					{ "EndTime", "☀\ufe0f Fin Noche:" },
					{ "LangTitle", "\ud83c\udf10 Idioma de la Aplicación" },
					{ "AutoStart", "Iniciar con Windows" },
					{ "MinimizeTray", "Minimizar a la bandeja al cerrar" },
					{ "ExitApp", "\ud83d\uded1 Salir de ArcLight" },
					{ "TrayToggle", "Alternar Modo Noche" },
					{ "TrayOpen", "Abrir Ajustes de ArcLight" },
					{ "TrayExit", "Salir de ArcLight" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● ACTIVO" },
					{ "StatusPillInactive", "○ INACTIVO" },
					{ "SegmentSun", "☀\ufe0f Solar" },
					{ "SegmentCustom", "⏱\ufe0f Horario" },
					{ "SegmentManual", "✋ Manual" },
					{ "SunriseLabel", "\ud83c\udf05 AMANECER" },
					{ "SunsetLabel", "\ud83c\udf07 ATARDECER" },
					{ "ManualNotice", "✋ Modo Manual activo.\nLa iluminación está bajo su control. El temporizador está desactivado." }
				}
			},
			{
				"fr",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 Lumière et Luminosité" },
					{ "TabLocation", "\ud83d\udccd Emplacement et Heures" },
					{ "TabSettings", "⚙\ufe0f Paramètres et Langue" },
					{ "NightModeOn", "\ud83c\udf19 MODE NUIT : ACTIVÉ" },
					{ "NightModeOff", "☀\ufe0f MODE JOUR : DÉSACTIVÉ" },
					{ "ClickToToggle", "(Cliquez ou Ctrl+Alt+N)" },
					{ "StatusNight", "Mode nuit actif. Lumière bleue filtrée." },
					{ "StatusDay", "Mode lumière du jour standard actif." },
					{ "ColorTemp", "\ud83d\udd25 Température de Couleur" },
					{ "Brightness", "☀\ufe0f Luminosité de l'écran" },
					{ "PresetCandle", "Bougie 2200K" },
					{ "PresetNight", "Nuit 3400K" },
					{ "PresetSunset", "Soir 4500K" },
					{ "PresetNormal", "Normal 6500K" },
					{ "DescCandle", "Lueur de Bougie / Très Chaud" },
					{ "DescNight", "Mode Nuit Confortable" },
					{ "DescSunset", "Lumière du Crépuscule" },
					{ "DescNormal", "Lumière du Jour Naturelle" },
					{ "SchedTitle", "\ud83d\udccd Planification & Emplacement" },
					{ "ModeSun", "Suivi Solaire (Par Emplacement)" },
					{ "ModeCustom", "Plage Horaire Personnalisée" },
					{ "ModeManual", "Manuel (Sans Minuteur)" },
					{ "CitySelect", "Choisir une Ville :" },
					{ "SunriseSunset", "\ud83c\udf05 Lever du Soleil : {0}   |   \ud83c\udf07 Coucher : {1}\n(Bascule automatiquement selon le soleil)" },
					{ "StartTime", "\ud83c\udf19 Début Nuit :" },
					{ "EndTime", "☀\ufe0f Fin Nuit :" },
					{ "LangTitle", "\ud83c\udf10 Langue de l'Application" },
					{ "AutoStart", "Lancer avec Windows" },
					{ "MinimizeTray", "Réduire dans la barre d'état" },
					{ "ExitApp", "\ud83d\uded1 Quitter ArcLight" },
					{ "TrayToggle", "Basculer le Mode Nuit" },
					{ "TrayOpen", "Ouvrir ArcLight" },
					{ "TrayExit", "Quitter ArcLight" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● ACTIF" },
					{ "StatusPillInactive", "○ INACTIF" },
					{ "SegmentSun", "☀\ufe0f Solaire" },
					{ "SegmentCustom", "⏱\ufe0f Heures" },
					{ "SegmentManual", "✋ Manuel" },
					{ "SunriseLabel", "\ud83c\udf05 LEVER" },
					{ "SunsetLabel", "\ud83c\udf07 COUCHER" },
					{ "ManualNotice", "✋ Mode Manuel actif.\nL'éclairage est sous votre contrôle. La minuterie est désactivée." }
				}
			},
			{
				"zh",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 光线与亮度" },
					{ "TabLocation", "\ud83d\udccd 位置与计划" },
					{ "TabSettings", "⚙\ufe0f 设置与语言" },
					{ "NightModeOn", "\ud83c\udf19 夜间模式：已开启" },
					{ "NightModeOff", "☀\ufe0f 日间模式：已关闭" },
					{ "ClickToToggle", "(单击或按 Ctrl+Alt+N 切换)" },
					{ "StatusNight", "夜间护眼模式运行中，过滤蓝光。" },
					{ "StatusDay", "标准日光模式运行中。" },
					{ "ColorTemp", "\ud83d\udd25 屏幕色温" },
					{ "Brightness", "☀\ufe0f 屏幕亮度" },
					{ "PresetCandle", "烛光 2200K" },
					{ "PresetNight", "夜间 3400K" },
					{ "PresetSunset", "傍晚 4500K" },
					{ "PresetNormal", "正常 6500K" },
					{ "DescCandle", "温馨烛光 / 超暖色" },
					{ "DescNight", "舒适夜间模式" },
					{ "DescSunset", "傍晚柔和光" },
					{ "DescNormal", "自然日光" },
					{ "SchedTitle", "\ud83d\udccd 智能计划与位置" },
					{ "ModeSun", "太阳跟随 (基于地理位置)" },
					{ "ModeCustom", "自定义时间段" },
					{ "ModeManual", "手动模式 (无计划)" },
					{ "CitySelect", "选择城市：" },
					{ "SunriseSunset", "\ud83c\udf05 日出：{0}   |   \ud83c\udf07 日落：{1}\n(根据太阳升落自动切换护眼模式)" },
					{ "StartTime", "\ud83c\udf19 开启时间：" },
					{ "EndTime", "☀\ufe0f 关闭时间：" },
					{ "LangTitle", "\ud83c\udf10 界面语言 / Language" },
					{ "AutoStart", "开机自动启动" },
					{ "MinimizeTray", "关闭时最小化到托盘" },
					{ "ExitApp", "\ud83d\uded1 退出 ArcLight" },
					{ "TrayToggle", "开启/关闭夜间模式" },
					{ "TrayOpen", "打开 ArcLight 设置" },
					{ "TrayExit", "退出 ArcLight" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● 已开启" },
					{ "StatusPillInactive", "○ 已关闭" },
					{ "SegmentSun", "☀\ufe0f 太阳" },
					{ "SegmentCustom", "⏱\ufe0f 定时" },
					{ "SegmentManual", "✋ 手动" },
					{ "SunriseLabel", "\ud83c\udf05 日出" },
					{ "SunsetLabel", "\ud83c\udf07 日落" },
					{ "ManualNotice", "✋ 手动模式已激活。\n屏幕光线完全由您控制，定时器已停用。" }
				}
			},
			{
				"ja",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 明るさと光" },
					{ "TabLocation", "\ud83d\udccd 位置とスケジュール" },
					{ "TabSettings", "⚙\ufe0f 設定と言語" },
					{ "NightModeOn", "\ud83c\udf19 夜間モード：オン" },
					{ "NightModeOff", "☀\ufe0f 昼間モード：オフ" },
					{ "ClickToToggle", "(クリックまたは Ctrl+Alt+N)" },
					{ "StatusNight", "夜間モード稼働中。ブルーライトをカット。" },
					{ "StatusDay", "標準の昼光モードです。" },
					{ "ColorTemp", "\ud83d\udd25 色温度" },
					{ "Brightness", "☀\ufe0f 画面の明るさ" },
					{ "PresetCandle", "ろうそく 2200K" },
					{ "PresetNight", "夜間 3400K" },
					{ "PresetSunset", "夕方 4500K" },
					{ "PresetNormal", "標準 6500K" },
					{ "DescCandle", "キャンドル / 超暖色" },
					{ "DescNight", "快適な夜間モード" },
					{ "DescSunset", "夕暮れの光" },
					{ "DescNormal", "自然な昼光" },
					{ "SchedTitle", "\ud83d\udccd スマートスケジュールと位置" },
					{ "ModeSun", "太陽追跡 (位置ベース)" },
					{ "ModeCustom", "カスタム時間範囲" },
					{ "ModeManual", "手動 (タイマーなし)" },
					{ "CitySelect", "都市を選択：" },
					{ "SunriseSunset", "\ud83c\udf05 日の出：{0}   |   \ud83c\udf07 日の入り：{1}\n(日の出・日の入り時刻に合わせて自動切り替え)" },
					{ "StartTime", "\ud83c\udf19 開始時間：" },
					{ "EndTime", "☀\ufe0f 終了時間：" },
					{ "LangTitle", "\ud83c\udf10 言語選択 / Language" },
					{ "AutoStart", "Windows起動時に実行" },
					{ "MinimizeTray", "閉じる時にタスクトレイに格納" },
					{ "ExitApp", "\ud83d\uded1 ArcLightを終了" },
					{ "TrayToggle", "夜間モードの切り替え" },
					{ "TrayOpen", "ArcLight設定を開く" },
					{ "TrayExit", "ArcLightの終了" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● 有効" },
					{ "StatusPillInactive", "○ 無効" },
					{ "SegmentSun", "☀\ufe0f 太陽" },
					{ "SegmentCustom", "⏱\ufe0f 時間指定" },
					{ "SegmentManual", "✋ 手動" },
					{ "SunriseLabel", "\ud83c\udf05 日の出" },
					{ "SunsetLabel", "\ud83c\udf07 日の入り" },
					{ "ManualNotice", "✋ 手動モード有効。\n画面の明るさは手動制御です。タイマーはオフです。" }
				}
			},
			{
				"ko",
				new Dictionary<string, string>
				{
					{ "TabLight", "\ud83d\udca1 조명 및 밝기" },
					{ "TabLocation", "\ud83d\udccd 위치 및 일정" },
					{ "TabSettings", "⚙\ufe0f 설정 및 언어" },
					{ "NightModeOn", "\ud83c\udf19 야간 모드: 켜짐" },
					{ "NightModeOff", "☀\ufe0f 주간 모드: 꺼짐" },
					{ "ClickToToggle", "(클릭 또는 Ctrl+Alt+N)" },
					{ "StatusNight", "야간 모드가 켜져 있습니다. 블루라이트 차단 중." },
					{ "StatusDay", "표준 주간 모드입니다." },
					{ "ColorTemp", "\ud83d\udd25 색온도" },
					{ "Brightness", "☀\ufe0f 화면 밝기" },
					{ "PresetCandle", "촛불 2200K" },
					{ "PresetNight", "야간 3400K" },
					{ "PresetSunset", "일몰 4500K" },
					{ "PresetNormal", "표준 6500K" },
					{ "DescCandle", "촛불 불빛 / 따뜻한 색" },
					{ "DescNight", "편안한 야간 모드" },
					{ "DescSunset", "노을빛 / 저녁 조명" },
					{ "DescNormal", "자연 주광" },
					{ "SchedTitle", "\ud83d\udccd 스마트 일정 및 위치" },
					{ "ModeSun", "태양 추적 (위치 기반)" },
					{ "ModeCustom", "사용자 지정 시간 범위" },
					{ "ModeManual", "수동 (일정 없음)" },
					{ "CitySelect", "도시 선택:" },
					{ "SunriseSunset", "\ud83c\udf05 일출: {0}   |   \ud83c\udf07 일몰: {1}\n(태양 이동에 따라 자동으로 전환됩니다)" },
					{ "StartTime", "\ud83c\udf19 시작 시간:" },
					{ "EndTime", "☀\ufe0f 종료 시간:" },
					{ "LangTitle", "\ud83c\udf10 인터페이스 언어 / Language" },
					{ "AutoStart", "Windows 시작 시 실행" },
					{ "MinimizeTray", "닫을 때 트레이로 최소화" },
					{ "ExitApp", "\ud83d\uded1 ArcLight 종료" },
					{ "TrayToggle", "야간 모드 켜기/끄기" },
					{ "TrayOpen", "ArcLight 설정 열기" },
					{ "TrayExit", "ArcLight 종료" },
					{ "HotkeyHint", "⌨ Ctrl+Alt+N" },
					{ "StatusPillActive", "● 활성" },
					{ "StatusPillInactive", "○ 비활성" },
					{ "SegmentSun", "☀\ufe0f 태양" },
					{ "SegmentCustom", "⏱\ufe0f 지정시간" },
					{ "SegmentManual", "✋ 수동" },
					{ "SunriseLabel", "\ud83c\udf05 일출" },
					{ "SunsetLabel", "\ud83c\udf07 일몰" },
					{ "ManualNotice", "✋ 수동 모드 활성화.\n화면 밝기가 수동으로 제어되며 타이머가 꺼집니다." }
				}
			}
		};

		public static string T(string key)
		{
			if (_dict.ContainsKey(CurrentLang) && _dict[CurrentLang].ContainsKey(key))
			{
				return _dict[CurrentLang][key];
			}
			if (_dict["en"].ContainsKey(key))
			{
				return _dict["en"][key];
			}
			return key;
		}
	}
	public static class SolarCalculator
	{
		public class CityInfo
		{
			public string Name { get; set; }

			public double Latitude { get; set; }

			public double Longitude { get; set; }

			public CityInfo(string name, double lat, double lon)
			{
				Name = name;
				Latitude = lat;
				Longitude = lon;
			}

			public override string ToString()
			{
				return Name;
			}
		}

		public static List<CityInfo> Cities = new List<CityInfo>
		{
			new CityInfo("İstanbul (Türkiye)", 41.0082, 28.9784),
			new CityInfo("Ankara (Türkiye)", 39.9334, 32.8597),
			new CityInfo("İzmir (Türkiye)", 38.4237, 27.1428),
			new CityInfo("Antalya (Türkiye)", 36.8969, 30.7133),
			new CityInfo("Trabzon (Türkiye)", 41.0027, 39.7168),
			new CityInfo("Erzurum (Türkiye)", 39.9043, 41.2679),
			new CityInfo("Diyarbakır (Türkiye)", 37.9144, 40.2306),
			new CityInfo("London (United Kingdom)", 51.5074, -0.1278),
			new CityInfo("Paris (France)", 48.8566, 2.3522),
			new CityInfo("Berlin (Germany)", 52.52, 13.405),
			new CityInfo("Madrid (Spain)", 40.4168, -3.7038),
			new CityInfo("Barcelona (Spain)", 41.3851, 2.1734),
			new CityInfo("Rome (Italy)", 41.9028, 12.4964),
			new CityInfo("Amsterdam (Netherlands)", 52.3676, 4.9041),
			new CityInfo("Vienna (Austria)", 48.2082, 16.3738),
			new CityInfo("New York (USA)", 40.7128, -74.006),
			new CityInfo("Los Angeles (USA)", 34.0522, -118.2437),
			new CityInfo("Chicago (USA)", 41.8781, -87.6298),
			new CityInfo("Toronto (Canada)", 43.6532, -79.3832),
			new CityInfo("Mexico City (Mexico)", 19.4326, -99.1332),
			new CityInfo("São Paulo (Brazil)", -23.5505, -46.6333),
			new CityInfo("Buenos Aires (Argentina)", -34.6037, -58.3816),
			new CityInfo("Tokyo (Japan)", 35.6762, 139.6503),
			new CityInfo("Osaka (Japan)", 34.6937, 135.5023),
			new CityInfo("Seoul (South Korea)", 37.5665, 126.978),
			new CityInfo("Busan (South Korea)", 35.1796, 129.0756),
			new CityInfo("Beijing (China)", 39.9042, 116.4074),
			new CityInfo("Shanghai (China)", 31.2304, 121.4737),
			new CityInfo("Hong Kong", 22.3193, 114.1694),
			new CityInfo("Taipei (Taiwan)", 25.033, 121.5654),
			new CityInfo("Singapore", 1.3521, 103.8198),
			new CityInfo("Dubai (UAE)", 25.2048, 55.2708),
			new CityInfo("Sydney (Australia)", -33.8688, 151.2093)
		};

		public static void CalculateSunriseSunset(double lat, double lon, DateTime date, out TimeSpan sunrise, out TimeSpan sunset)
		{
			int dayOfYear = date.DayOfYear;
			double num = Math.PI * 2.0 / 365.0 * ((double)(dayOfYear - 1) + (double)(date.Hour - 12) / 24.0);
			double num2 = 229.18 * (7.5E-05 + 0.001868 * Math.Cos(num) - 0.032077 * Math.Sin(num) - 0.014615 * Math.Cos(2.0 * num) - 0.040849 * Math.Sin(2.0 * num));
			double num3 = 0.006918 - 0.399912 * Math.Cos(num) + 0.070257 * Math.Sin(num) - 0.006758 * Math.Cos(2.0 * num) + 0.000907 * Math.Sin(2.0 * num);
			double num4 = lat * Math.PI / 180.0;
			double d = 1.5853349194640092;
			double num5 = (Math.Cos(d) - Math.Sin(num4) * Math.Sin(num3)) / (Math.Cos(num4) * Math.Cos(num3));
			if (double.IsNaN(num5) || num5 > 1.0)
			{
				sunrise = TimeSpan.FromHours(6.0);
				sunset = TimeSpan.FromHours(18.0);
				return;
			}
			if (num5 < -1.0)
			{
				sunrise = TimeSpan.FromHours(0.0);
				sunset = TimeSpan.FromHours(24.0);
				return;
			}
			double num6 = Math.Acos(num5) * 180.0 / Math.PI;
			double num7 = 4.0 * lon;
			double num8 = 720.0 - num7 - num2;
			double num9 = num8 - num6 * 4.0;
			double num10 = num8 + num6 * 4.0;
			double totalMinutes = TimeZoneInfo.Local.GetUtcOffset(date).TotalMinutes;
			double value = (num9 + totalMinutes + 1440.0) % 1440.0;
			double value2 = (num10 + totalMinutes + 1440.0) % 1440.0;
			sunrise = TimeSpan.FromMinutes(value);
			sunset = TimeSpan.FromMinutes(value2);
		}
	}
	public class MainForm : Form
	{
		private class LanguageItem
		{
			public string Code { get; set; }

			public string Display { get; set; }

			public LanguageItem(string code, string display)
			{
				Code = code;
				Display = display;
			}

			public override string ToString()
			{
				return Display;
			}
		}

		private const int HOTKEY_ID = 9001;

		private const uint MOD_ALT = 1u;

		private const uint MOD_CONTROL = 2u;

		private const int WM_HOTKEY = 786;

		private const uint WM_SETICON = 128u;

		private Settings _settings;

		private NotifyIcon _trayIcon;

		private ContextMenuStrip _trayMenu;

		private System.Windows.Forms.Timer _scheduleTimer;
        private System.Windows.Forms.Timer _gammaRecoveryTimer;

		private System.Windows.Forms.Timer _saveDebounceTimer;

		private System.Windows.Forms.Timer _fadeTimer;

		private double _currentK = 6500.0;

		private double _currentB = 100.0;

		private double _targetK = 6500.0;

		private double _targetB = 100.0;

		private bool _isUpdatingUI = false;

		private bool? _lastScheduleNightState = null;

		private bool _hasShownTrayBalloon = false;

		private EventHandler _displayHandler;

		private PowerModeChangedEventHandler _powerHandler;

		private static readonly IntPtr ICON_SMALL = (IntPtr)0;

		private static readonly IntPtr ICON_BIG = (IntPtr)1;

		private PictureBox _picLogo;

		private Label _lblTitle;

		private Label _lblStatusPill;

		private Label _lblHotkeyHint;

		private Button _btnToggle;

		private Label _lblStatusText;

		private Label _lblToggleHint;

		private Panel _cardToggle;

		private Panel[] _cardsBelowToggle;

		private bool _isUpdatingStatusLayout;

		private Label _lblKTitle;

		private Label _lblKelvinVal;

		private TrackBar _tbKelvin;

		private Label _lblBTitle;

		private Label _lblBrightnessVal;

		private TrackBar _tbBrightness;

		private Button _btnPresetCandle;

		private Button _btnPresetNight;

		private Button _btnPresetSunset;

		private Button _btnPresetNormal;

		private Label _lblSchedTitle;

		private Button _btnModeSun;

		private Button _btnModeCustom;

		private Button _btnModeManual;

		private Panel _panelSunSettings;

		private ComboBox _cbCity;

		private Label _lblSunriseBadge;

		private Label _lblSunsetBadge;

		private Label _lblSolarInfo;

		private Panel _panelCustomSettings;

		private Label _lblStart;

		private DateTimePicker _dtpStart;

		private Label _lblEnd;

		private DateTimePicker _dtpEnd;

		private Panel _panelManualSettings;

		private Label _lblManualNotice;

		private ComboBox _cbLanguage;

		private CheckBox _chkAutoStart;

		private CheckBox _chkMinimizeTray;

		private Button _btnExitApp;

		private Label _lblFooter;

		private Color _bgDark = Color.FromArgb(18, 18, 22);

		private Color _cardDark = Color.FromArgb(28, 28, 35);

		private Color _accentOrange = Color.FromArgb(249, 115, 22);

		private Color _accentGold = Color.FromArgb(251, 191, 36);

		private Color _offGray = Color.FromArgb(42, 42, 54);

		private bool _isExiting = false;

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("shell32.dll", SetLastError = true)]
		public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

		public MainForm()
		{
			_settings = Settings.Load();
			Loc.CurrentLang = _settings.Language;
			GammaController.SaveOriginal();
			base.Icon = CreateAppIcon();
			SetupDebounceTimer();
			SetupFadeTimer();
			InitializeUI();
			SetupTrayIcon();
			SetupTimer();
            _gammaRecoveryTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _gammaRecoveryTimer.Tick += delegate { if (!_isExiting && _settings.IsEnabled && (_fadeTimer == null || !_fadeTimer.Enabled)) GammaController.EnsureApplied(); };
            _gammaRecoveryTimer.Start();
			SetupSystemEvents();
			CheckScheduleAndApply(true);
		}

		private void SetupSystemEvents()
		{
			try
			{
				_displayHandler = delegate
				{
					if (base.IsHandleCreated && !base.IsDisposed)
					{
						try
						{
							BeginInvoke((Action)delegate
							{
								if (!base.IsDisposed)
								{
									ApplyCurrentSettingsImmediate(false);
									System.Windows.Forms.Timer t = new System.Windows.Forms.Timer
									{
										Interval = 1000
									};
									t.Tick += delegate
									{
										t.Stop();
										t.Dispose();
										if (!base.IsDisposed)
										{
											ApplyCurrentSettingsImmediate(false);
										}
									};
									t.Start();
								}
							});
						}
						catch
						{
						}
					}
				};
				_powerHandler = delegate(object s, PowerModeChangedEventArgs e)
				{
					if (e.Mode == PowerModes.Resume && base.IsHandleCreated && !base.IsDisposed)
					{
						try
						{
							BeginInvoke((Action)delegate
							{
								if (!base.IsDisposed)
								{
									ApplyCurrentSettingsImmediate();
									System.Windows.Forms.Timer t = new System.Windows.Forms.Timer
									{
										Interval = 1200
									};
									t.Tick += delegate
									{
										t.Stop();
										t.Dispose();
										if (!base.IsDisposed)
										{
											ApplyCurrentSettingsImmediate(false);
										}
									};
									t.Start();
								}
							});
						}
						catch
						{
						}
					}
				};
				SystemEvents.DisplaySettingsChanged += _displayHandler;
				SystemEvents.PowerModeChanged += _powerHandler;
			}
			catch
			{
			}
		}

		private void DetachSystemEvents()
		{
			try
			{
				if (_displayHandler != null)
				{
					SystemEvents.DisplaySettingsChanged -= _displayHandler;
					_displayHandler = null;
				}
				if (_powerHandler != null)
				{
					SystemEvents.PowerModeChanged -= _powerHandler;
					_powerHandler = null;
				}
			}
			catch
			{
			}
		}

		private void SetupDebounceTimer()
		{
			_saveDebounceTimer = new System.Windows.Forms.Timer();
			_saveDebounceTimer.Interval = 400;
			_saveDebounceTimer.Tick += delegate
			{
				_saveDebounceTimer.Stop();
				_settings.Save();
			};
		}

		private void SetupFadeTimer()
		{
			_fadeTimer = new System.Windows.Forms.Timer();
			_fadeTimer.Interval = 15;
			_fadeTimer.Tick += delegate
			{
				double num = _targetK - _currentK;
				double num2 = _targetB - _currentB;
				if (Math.Abs(num) < 20.0 && Math.Abs(num2) < 1.0)
				{
					_currentK = _targetK;
					_currentB = _targetB;
					_fadeTimer.Stop();
					GammaController.SetColorTemperatureAndBrightness((int)_currentK, (int)_currentB);
				}
				else
				{
					_currentK += num * 0.28;
					_currentB += num2 * 0.28;
					GammaController.SetColorTemperatureAndBrightness((int)_currentK, (int)_currentB);
				}
			};
		}

		private void TriggerDebouncedSave()
		{
			if (_saveDebounceTimer != null)
			{
				_saveDebounceTimer.Stop();
				_saveDebounceTimer.Start();
			}
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 786 && m.WParam.ToInt32() == 9001)
			{
				ToggleNightMode();
			}
			else if (Program.WM_SHOWFIRSTINSTANCE != 0 && m.Msg == (int)Program.WM_SHOWFIRSTINSTANCE)
			{
				ShowWindow();
			}
			base.WndProc(ref m);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			try
			{
				int attrValue = 1;
				DwmSetWindowAttribute(base.Handle, 20, ref attrValue, 4);
				DwmSetWindowAttribute(base.Handle, 19, ref attrValue, 4);
			}
			catch
			{
			}
			try
			{
				RegisterHotKey(base.Handle, 9001, 3u, 78u);
			}
			catch
			{
			}
			try
			{
				if (base.Icon != null)
				{
					SendMessage(base.Handle, 128u, ICON_SMALL, base.Icon.Handle);
					SendMessage(base.Handle, 128u, ICON_BIG, base.Icon.Handle);
				}
			}
			catch
			{
			}
		}

		protected override void OnHandleDestroyed(EventArgs e)
		{
			try
			{
				UnregisterHotKey(base.Handle, 9001);
			}
			catch
			{
			}
			base.OnHandleDestroyed(e);
		}

		private void InitializeUI()
		{
			Text = "ArcLight";
			base.AutoScaleMode = AutoScaleMode.None;
			base.ClientSize = new Size(410, 690);
			base.StartPosition = FormStartPosition.CenterScreen;
			base.FormBorderStyle = FormBorderStyle.FixedSingle;
			base.MaximizeBox = false;
			BackColor = _bgDark;
			ForeColor = Color.White;
			Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
			Panel panel = new Panel();
			panel.Location = new Point(14, 12);
			panel.Size = new Size(382, 34);
			panel.BackColor = Color.Transparent;
			Panel panel2 = panel;
			_picLogo = new PictureBox
			{
				Image = CreateLogoBitmap(30),
				Size = new Size(30, 30),
				Location = new Point(0, 2),
				SizeMode = PictureBoxSizeMode.Zoom
			};
			panel2.Controls.Add(_picLogo);
			_lblTitle = new Label
			{
				Text = "ArcLight",
				Font = new Font("Segoe UI", 15f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(36, 0),
				AutoSize = true
			};
			panel2.Controls.Add(_lblTitle);
			_lblStatusPill = new Label
			{
				Location = new Point(148, 5),
				Size = new Size(74, 24),
				Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleCenter,
				Cursor = Cursors.Hand
			};
			_lblStatusPill.Click += delegate
			{
				ToggleNightMode();
			};
			_lblStatusPill.Paint += delegate(object s, PaintEventArgs e)
			{
				Color color = (_settings.IsEnabled ? Color.FromArgb(249, 115, 22) : Color.FromArgb(60, 60, 72));
				using (Pen pen = new Pen(color, 1f))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, _lblStatusPill.Width - 1, _lblStatusPill.Height - 1);
				}
			};
			panel2.Controls.Add(_lblStatusPill);
			_lblHotkeyHint = new Label
			{
				Location = new Point(232, 5),
				Size = new Size(150, 24),
				Text = Loc.T("HotkeyHint"),
				Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
				ForeColor = Color.White,
				BackColor = Color.FromArgb(28, 28, 38),
				TextAlign = ContentAlignment.MiddleCenter
			};
			_lblHotkeyHint.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Color.FromArgb(58, 58, 72), 1f))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, _lblHotkeyHint.Width - 1, _lblHotkeyHint.Height - 1);
				}
			};
			panel2.Controls.Add(_lblHotkeyHint);
			base.Controls.Add(panel2);
			Panel panel3 = CreateCardPanel(14, 52, 382, 84);
			_cardToggle = panel3;
			_btnToggle = new Button
			{
				Location = new Point(10, 10),
				Size = new Size(362, 44),
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			_btnToggle.FlatAppearance.BorderSize = 0;
			_btnToggle.Click += delegate
			{
				ToggleNightMode();
			};
			panel3.Controls.Add(_btnToggle);
			_lblStatusText = new Label
			{
				Location = new Point(10, 58),
				Size = new Size(362, 20),
				TextAlign = ContentAlignment.MiddleCenter,
				UseMnemonic = false,
				Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
			};
			panel3.Controls.Add(_lblStatusText);
			_lblToggleHint = new Label
			{
				TextAlign = ContentAlignment.MiddleCenter,
				UseMnemonic = false,
				Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
				ForeColor = Color.FromArgb(220, 220, 230)
			};
			panel3.Controls.Add(_lblToggleHint);
			base.Controls.Add(panel3);
			Panel panel4 = CreateCardPanel(14, 144, 382, 208);
			_lblKTitle = new Label
			{
				Text = Loc.T("ColorTemp"),
				Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(12, 12),
				AutoSize = true
			};
			panel4.Controls.Add(_lblKTitle);
			_lblKelvinVal = new Label
			{
				Text = _settings.Kelvin + " K",
				Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(160, 12),
				Size = new Size(210, 18),
				TextAlign = ContentAlignment.MiddleRight
			};
			panel4.Controls.Add(_lblKelvinVal);
			_tbKelvin = new TrackBar
			{
				Location = new Point(8, 36),
				Size = new Size(366, 40),
				Minimum = 1800,
				Maximum = 6500,
				TickFrequency = 500,
				SmallChange = 100,
				LargeChange = 500,
				Value = Math.Max(1800, Math.Min(6500, _settings.Kelvin)),
				Cursor = Cursors.Hand
			};
			_tbKelvin.Scroll += delegate
			{
				HandleKelvinChange();
			};
			_tbKelvin.ValueChanged += delegate
			{
				HandleKelvinChange();
			};
			panel4.Controls.Add(_tbKelvin);
			_lblBTitle = new Label
			{
				Text = Loc.T("Brightness"),
				Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(12, 84),
				AutoSize = true
			};
			panel4.Controls.Add(_lblBTitle);
			_lblBrightnessVal = new Label
			{
				Text = "%" + _settings.Brightness,
				Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(220, 84),
				Size = new Size(150, 18),
				TextAlign = ContentAlignment.MiddleRight
			};
			panel4.Controls.Add(_lblBrightnessVal);
			_tbBrightness = new TrackBar
			{
				Location = new Point(8, 108),
				Size = new Size(366, 40),
				Minimum = 25,
				Maximum = 100,
				TickFrequency = 5,
				SmallChange = 5,
				LargeChange = 10,
				Value = Math.Max(25, Math.Min(100, _settings.Brightness)),
				Cursor = Cursors.Hand
			};
			_tbBrightness.Scroll += delegate
			{
				HandleBrightnessChange();
			};
			_tbBrightness.ValueChanged += delegate
			{
				HandleBrightnessChange();
			};
			panel4.Controls.Add(_tbBrightness);
			_btnPresetCandle = CreatePresetButton(Loc.T("PresetCandle"), 2200, 75, 10, 158, 86);
			_btnPresetNight = CreatePresetButton(Loc.T("PresetNight"), 3400, 85, 102, 158, 86);
			_btnPresetSunset = CreatePresetButton(Loc.T("PresetSunset"), 4500, 95, 194, 158, 86);
			_btnPresetNormal = CreatePresetButton(Loc.T("PresetNormal"), 6500, 100, 286, 158, 86);
			panel4.Controls.Add(_btnPresetCandle);
			panel4.Controls.Add(_btnPresetNight);
			panel4.Controls.Add(_btnPresetSunset);
			panel4.Controls.Add(_btnPresetNormal);
			base.Controls.Add(panel4);
			Panel panel5 = CreateCardPanel(14, 360, 382, 182);
			_lblSchedTitle = new Label
			{
				Text = Loc.T("SchedTitle"),
				Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(12, 8),
				AutoSize = true
			};
			panel5.Controls.Add(_lblSchedTitle);
			_btnModeSun = CreateSegmentButton(Loc.T("SegmentSun"), 12, 28, 114, 0);
			_btnModeCustom = CreateSegmentButton(Loc.T("SegmentCustom"), 133, 28, 114, 1);
			_btnModeManual = CreateSegmentButton(Loc.T("SegmentManual"), 254, 28, 114, 2);
			panel5.Controls.Add(_btnModeSun);
			panel5.Controls.Add(_btnModeCustom);
			panel5.Controls.Add(_btnModeManual);
			Panel pnlSubContainer = new Panel
			{
				Location = new Point(10, 62),
				Size = new Size(362, 110),
				BackColor = Color.FromArgb(20, 20, 26)
			};
			pnlSubContainer.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Color.FromArgb(42, 42, 52), 1f))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, pnlSubContainer.Width - 1, pnlSubContainer.Height - 1);
				}
			};
			_panelSunSettings = new Panel
			{
				Location = new Point(0, 0),
				Size = new Size(362, 110),
				BackColor = Color.Transparent
			};
			_cbCity = new ComboBox
			{
				Location = new Point(8, 8),
				Size = new Size(346, 26),
				DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = Color.FromArgb(36, 36, 46),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Segoe UI", 9.2f),
				Cursor = Cursors.Hand
			};
			foreach (SolarCalculator.CityInfo city in SolarCalculator.Cities)
			{
				_cbCity.Items.Add(city);
			}
			SelectCityInCombo(_settings.CityName);
			SolarCalculator.CityInfo cityInfo = _cbCity.SelectedItem as SolarCalculator.CityInfo;
			if (cityInfo != null)
			{
				_settings.CityName = cityInfo.Name;
			}
			_cbCity.SelectedIndexChanged += delegate
			{
				SolarCalculator.CityInfo cityInfo2 = _cbCity.SelectedItem as SolarCalculator.CityInfo;
				if (cityInfo2 != null)
				{
					_settings.CityName = cityInfo2.Name;
					UpdateSolarCalculations();
					_settings.Save();
					CheckScheduleAndApply(true);
				}
			};
			_panelSunSettings.Controls.Add(_cbCity);
			_lblSunriseBadge = CreateBadgeLabel(8, 40, 169, 62);
			_lblSunsetBadge = CreateBadgeLabel(185, 40, 169, 62);
			_panelSunSettings.Controls.Add(_lblSunriseBadge);
			_panelSunSettings.Controls.Add(_lblSunsetBadge);
			_lblSolarInfo = new Label
			{
				Visible = false
			};
			_panelSunSettings.Controls.Add(_lblSolarInfo);
			pnlSubContainer.Controls.Add(_panelSunSettings);
			_panelCustomSettings = new Panel
			{
				Location = new Point(0, 0),
				Size = new Size(362, 110),
				BackColor = Color.Transparent,
				Visible = false
			};
			_lblStart = new Label
			{
				Text = Loc.T("StartTime"),
				Location = new Point(12, 14),
				AutoSize = true,
				Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
				ForeColor = Color.White
			};
			_panelCustomSettings.Controls.Add(_lblStart);
			_dtpStart = new DateTimePicker
			{
				Format = DateTimePickerFormat.Custom,
				CustomFormat = "HH:mm",
				ShowUpDown = true,
				Location = new Point(12, 38),
				Size = new Size(150, 26),
				Font = new Font("Segoe UI", 10.5f),
				Value = DateTime.Today.Add(_settings.CustomStartTime),
				Cursor = Cursors.Hand
			};
			_dtpStart.ValueChanged += delegate
			{
				_settings.CustomStartTime = _dtpStart.Value.TimeOfDay;
				_settings.Save();
				CheckScheduleAndApply(true);
			};
			_panelCustomSettings.Controls.Add(_dtpStart);
			_lblEnd = new Label
			{
				Text = Loc.T("EndTime"),
				Location = new Point(195, 14),
				AutoSize = true,
				Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
				ForeColor = Color.White
			};
			_panelCustomSettings.Controls.Add(_lblEnd);
			_dtpEnd = new DateTimePicker
			{
				Format = DateTimePickerFormat.Custom,
				CustomFormat = "HH:mm",
				ShowUpDown = true,
				Location = new Point(195, 38),
				Size = new Size(150, 26),
				Font = new Font("Segoe UI", 10.5f),
				Value = DateTime.Today.Add(_settings.CustomEndTime),
				Cursor = Cursors.Hand
			};
			_dtpEnd.ValueChanged += delegate
			{
				_settings.CustomEndTime = _dtpEnd.Value.TimeOfDay;
				_settings.Save();
				CheckScheduleAndApply(true);
			};
			_panelCustomSettings.Controls.Add(_dtpEnd);
			pnlSubContainer.Controls.Add(_panelCustomSettings);
			_panelManualSettings = new Panel
			{
				Location = new Point(0, 0),
				Size = new Size(362, 110),
				BackColor = Color.Transparent,
				Visible = false
			};
			_lblManualNotice = new Label
			{
				Location = new Point(12, 16),
				Size = new Size(338, 76),
				Font = new Font("Segoe UI", 9f),
				ForeColor = Color.White,
				TextAlign = ContentAlignment.MiddleCenter,
				Text = Loc.T("ManualNotice")
			};
			_panelManualSettings.Controls.Add(_lblManualNotice);
			pnlSubContainer.Controls.Add(_panelManualSettings);
			panel5.Controls.Add(pnlSubContainer);
			base.Controls.Add(panel5);
			Panel panel6 = CreateCardPanel(14, 550, 382, 126);
			_cbLanguage = new ComboBox
			{
				Location = new Point(12, 10),
				Size = new Size(170, 26),
				DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = Color.FromArgb(36, 36, 46),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Segoe UI", 9f),
				Cursor = Cursors.Hand
			};
			_cbLanguage.Items.Add(new LanguageItem("tr", "\ud83c\uddf9\ud83c\uddf7 Türkçe"));
			_cbLanguage.Items.Add(new LanguageItem("en", "\ud83c\uddec\ud83c\udde7 English"));
			_cbLanguage.Items.Add(new LanguageItem("es", "\ud83c\uddea\ud83c\uddf8 Español"));
			_cbLanguage.Items.Add(new LanguageItem("fr", "\ud83c\uddeb\ud83c\uddf7 Français"));
			_cbLanguage.Items.Add(new LanguageItem("zh", "\ud83c\udde8\ud83c\uddf3 中文 (Chinese)"));
			_cbLanguage.Items.Add(new LanguageItem("ja", "\ud83c\uddef\ud83c\uddf5 日本語"));
			_cbLanguage.Items.Add(new LanguageItem("ko", "\ud83c\uddf0\ud83c\uddf7 한국어"));
			SelectLanguageInCombo(_settings.Language);
			_cbLanguage.SelectedIndexChanged += delegate
			{
				LanguageItem languageItem = _cbLanguage.SelectedItem as LanguageItem;
				if (languageItem != null)
				{
					_settings.Language = languageItem.Code;
					Loc.CurrentLang = languageItem.Code;
					_settings.Save();
					ApplyLanguageChanges();
				}
			};
			panel6.Controls.Add(_cbLanguage);
			_chkAutoStart = new CheckBox
			{
				Text = Loc.T("AutoStart"),
				Location = new Point(194, 12),
				AutoSize = true,
				Checked = _settings.AutoStart,
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 8.5f),
				Cursor = Cursors.Hand
			};
			_chkAutoStart.CheckedChanged += delegate
			{
				_settings.AutoStart = _chkAutoStart.Checked;
				SetAutoStartRegistry(_settings.AutoStart);
				_settings.Save();
			};
			panel6.Controls.Add(_chkAutoStart);
			_chkMinimizeTray = new CheckBox
			{
				Text = Loc.T("MinimizeTray"),
				Location = new Point(12, 46),
				AutoSize = true,
				Checked = _settings.MinimizeToTray,
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 8.5f),
				MaximumSize = new Size(236, 0),
				Cursor = Cursors.Hand
			};
			_chkMinimizeTray.CheckedChanged += delegate
			{
				_settings.MinimizeToTray = _chkMinimizeTray.Checked;
				_settings.Save();
			};
			panel6.Controls.Add(_chkMinimizeTray);
			_btnExitApp = new Button
			{
				Text = Loc.T("ExitApp"),
				Location = new Point(252, 42),
				Size = new Size(118, 28),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(130, 24, 24),
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			_btnExitApp.FlatAppearance.BorderSize = 0;
			_btnExitApp.Click += delegate
			{
				ExitApplication();
			};
			panel6.Controls.Add(_btnExitApp);
			_lblFooter = new Label
			{
				Location = new Point(10, 88),
				Size = new Size(362, 22),
				Text = "ArcLight v0.3.1  •  x64 / x86 / ARM64  •  Hafif & Güvenli",
				Font = new Font("Segoe UI", 8f),
				ForeColor = Color.FromArgb(190, 190, 205),
				TextAlign = ContentAlignment.MiddleCenter
			};
			panel6.Controls.Add(_lblFooter);
			base.Controls.Add(panel6);
			_cardsBelowToggle = new Panel[] { panel4, panel5, panel6 };
			base.AutoScroll = true;
			_lblStatusText.FontChanged += delegate { LayoutToggleStatus(); };
			_lblToggleHint.FontChanged += delegate { LayoutToggleStatus(); };
			base.Shown += delegate { LayoutToggleStatus(); };
			base.SizeChanged += delegate { LayoutToggleStatus(); };
			UpdateSegmentedModeButtons();
			UpdateLabels();
			UpdateToggleButtonState();
			UpdateSolarCalculations();
		}

		private void LayoutToggleStatus()
		{
			if (_isUpdatingStatusLayout || _cardsBelowToggle == null) return;
			_isUpdatingStatusLayout = true;
			try
			{
				// Measure wrapped labels independently, including the keyboard shortcut.
				int textWidth = _cardToggle.ClientSize.Width - 20;
				// Reserve the larger day/night description so toggling never moves the cards.
				int statusHeight;
				using (Label measure = new Label { Font = _lblStatusText.Font, UseMnemonic = false })
				{
					measure.Text = Loc.T("StatusDay");
					statusHeight = measure.GetPreferredSize(new Size(textWidth, 0)).Height;
					measure.Text = Loc.T("StatusNight");
					statusHeight = Math.Max(statusHeight, measure.GetPreferredSize(new Size(textWidth, 0)).Height);
				}
				int hintHeight = _lblToggleHint.GetPreferredSize(new Size(textWidth, 0)).Height;
				_lblStatusText.SetBounds(10, _btnToggle.Bottom + 4, textWidth, statusHeight);
				_lblToggleHint.SetBounds(10, _lblStatusText.Bottom + 2, textWidth, hintHeight);
				_cardToggle.Height = _lblToggleHint.Bottom + 8;
				_cardToggle.Invalidate();

				int nextTop = _cardToggle.Bottom + 8;
				foreach (Panel card in _cardsBelowToggle)
				{
					card.Top = nextTop;
					nextTop = card.Bottom + 8;
				}

				// Grow with the text; use vertical scrolling when the desktop is shorter.
				int contentHeight = nextTop + 6 - base.AutoScrollPosition.Y;
				Rectangle workArea = Screen.FromControl(this).WorkingArea;
				int frameHeight = SizeFromClientSize(new Size(410, 690)).Height - 690;
				int availableHeight = Math.Max(100, workArea.Height - frameHeight);
				bool needsScroll = contentHeight > availableHeight;
				base.AutoScrollMinSize = new Size(0, contentHeight);
				base.ClientSize = new Size(410 + (needsScroll ? SystemInformation.VerticalScrollBarWidth : 0),
					Math.Min(contentHeight, availableHeight));
				if (base.Visible && base.WindowState == FormWindowState.Normal)
				{
					base.Location = new Point(Math.Max(workArea.Left, Math.Min(base.Left, workArea.Right - base.Width)),
						Math.Max(workArea.Top, Math.Min(base.Top, workArea.Bottom - base.Height)));
				}
			}
			finally
			{
				_isUpdatingStatusLayout = false;
			}
		}

		private void ToggleNightMode()
		{
			_settings.IsEnabled = !_settings.IsEnabled;
			UpdateToggleButtonState();
			ApplyCurrentSettingsImmediate();
			_settings.Save();
		}

		private void HandleKelvinChange()
		{
			if (!_isUpdatingUI && (_settings.Kelvin != _tbKelvin.Value || !_settings.IsEnabled))
			{
				_settings.Kelvin = _tbKelvin.Value;
				UpdateLabels();
				if (!_settings.IsEnabled)
				{
					_settings.IsEnabled = true;
					UpdateToggleButtonState();
				}
				ApplyCurrentSettingsImmediate(false);
				TriggerDebouncedSave();
			}
		}

		private void HandleBrightnessChange()
		{
			if (!_isUpdatingUI && (_settings.Brightness != _tbBrightness.Value || !_settings.IsEnabled))
			{
				_settings.Brightness = _tbBrightness.Value;
				UpdateLabels();
				if (!_settings.IsEnabled)
				{
					_settings.IsEnabled = true;
					UpdateToggleButtonState();
				}
				ApplyCurrentSettingsImmediate(false);
				TriggerDebouncedSave();
			}
		}

		private Label CreateBadgeLabel(int x, int y, int width, int height)
		{
			Label label = new Label();
			label.Location = new Point(x, y);
			label.Size = new Size(width, height);
			label.BackColor = Color.FromArgb(28, 28, 38);
			label.ForeColor = Color.White;
			label.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
			label.TextAlign = ContentAlignment.MiddleCenter;
			Label label2 = label;
			label2.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Color.FromArgb(52, 52, 66), 1f))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, width - 1, height - 1);
				}
			};
			return label2;
		}

		private Button CreateSegmentButton(string text, int x, int y, int width, int modeIndex)
		{
			Button button = new Button();
			button.Text = text;
			button.Location = new Point(x, y);
			button.Size = new Size(width, 28);
			button.FlatStyle = FlatStyle.Flat;
			button.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
			button.Cursor = Cursors.Hand;
			Button button2 = button;
			button2.FlatAppearance.BorderSize = 1;
			button2.Click += delegate
			{
				ChangeScheduleMode(modeIndex);
			};
			return button2;
		}

		private void SelectLanguageInCombo(string code)
		{
			for (int i = 0; i < _cbLanguage.Items.Count; i++)
			{
				LanguageItem languageItem = _cbLanguage.Items[i] as LanguageItem;
				if (languageItem != null && languageItem.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
				{
					_cbLanguage.SelectedIndex = i;
					return;
				}
			}
			if (_cbLanguage.Items.Count > 0)
			{
				_cbLanguage.SelectedIndex = 0;
			}
		}

		private void ApplyLanguageChanges()
		{
			if (_lblHotkeyHint != null)
			{
				_lblHotkeyHint.Text = Loc.T("HotkeyHint");
			}
			if (_lblKTitle != null)
			{
				_lblKTitle.Text = Loc.T("ColorTemp");
			}
			if (_lblBTitle != null)
			{
				_lblBTitle.Text = Loc.T("Brightness");
			}
			if (_btnPresetCandle != null)
			{
				_btnPresetCandle.Text = Loc.T("PresetCandle");
			}
			if (_btnPresetNight != null)
			{
				_btnPresetNight.Text = Loc.T("PresetNight");
			}
			if (_btnPresetSunset != null)
			{
				_btnPresetSunset.Text = Loc.T("PresetSunset");
			}
			if (_btnPresetNormal != null)
			{
				_btnPresetNormal.Text = Loc.T("PresetNormal");
			}
			if (_lblSchedTitle != null)
			{
				_lblSchedTitle.Text = Loc.T("SchedTitle");
			}
			if (_btnModeSun != null)
			{
				_btnModeSun.Text = Loc.T("SegmentSun");
			}
			if (_btnModeCustom != null)
			{
				_btnModeCustom.Text = Loc.T("SegmentCustom");
			}
			if (_btnModeManual != null)
			{
				_btnModeManual.Text = Loc.T("SegmentManual");
			}
			if (_lblStart != null)
			{
				_lblStart.Text = Loc.T("StartTime");
			}
			if (_lblEnd != null)
			{
				_lblEnd.Text = Loc.T("EndTime");
			}
			if (_lblManualNotice != null)
			{
				_lblManualNotice.Text = Loc.T("ManualNotice");
			}
			if (_chkAutoStart != null)
			{
				_chkAutoStart.Text = Loc.T("AutoStart");
			}
			if (_chkMinimizeTray != null)
			{
				_chkMinimizeTray.Text = Loc.T("MinimizeTray");
			}
			if (_btnExitApp != null)
			{
				_btnExitApp.Text = Loc.T("ExitApp");
			}
			UpdateLabels();
			UpdateToggleButtonState();
			UpdateSegmentedModeButtons();
			UpdateSolarCalculations();
			if (_trayMenu != null && _trayMenu.Items.Count >= 4)
			{
				_trayMenu.Items[0].Text = Loc.T("TrayToggle");
				_trayMenu.Items[1].Text = Loc.T("TrayOpen");
				_trayMenu.Items[3].Text = Loc.T("TrayExit");
			}
		}

		private Panel CreateCardPanel(int x, int y, int width, int height)
		{
			Panel panel = new Panel();
			panel.Location = new Point(x, y);
			panel.Size = new Size(width, height);
			panel.BackColor = _cardDark;
			Panel panel2 = panel;
			panel2.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Color.FromArgb(46, 46, 56), 1f))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, panel2.ClientSize.Width - 1, panel2.ClientSize.Height - 1);
				}
			};
			return panel2;
		}

		private Button CreatePresetButton(string text, int kelvin, int brightness, int x, int y, int width, int height = 36)
		{
			Button button = new Button();
			button.Text = text;
			button.Location = new Point(x, y);
			button.Size = new Size(width, height);
			button.FlatStyle = FlatStyle.Flat;
			button.BackColor = Color.FromArgb(36, 36, 46);
			button.ForeColor = Color.White;
			button.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
			button.Margin = Padding.Empty;
			button.Padding = Padding.Empty;
			button.Cursor = Cursors.Hand;
			Button button2 = button;
			button2.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 68);
			button2.Click += delegate
			{
				_isUpdatingUI = true;
				_settings.Kelvin = kelvin;
				_settings.Brightness = brightness;
				_tbKelvin.Value = Math.Max(1800, Math.Min(6500, kelvin));
				_tbBrightness.Value = Math.Max(25, Math.Min(100, brightness));
				_isUpdatingUI = false;
				if (!_settings.IsEnabled)
				{
					_settings.IsEnabled = true;
					UpdateToggleButtonState();
				}
				UpdateLabels();
				ApplyCurrentSettingsImmediate();
				_settings.Save();
			};
			return button2;
		}

		private void UpdatePresetButtonHighlight(Button btn, bool active)
		{
			if (btn != null)
			{
				if (active)
				{
					btn.BackColor = Color.FromArgb(64, 38, 22);
					btn.ForeColor = Color.White;
					btn.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
					btn.FlatAppearance.BorderColor = _accentOrange;
				}
				else
				{
					btn.BackColor = Color.FromArgb(36, 36, 46);
					btn.ForeColor = Color.White;
					btn.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
					btn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 68);
				}
			}
		}

		private void ChangeScheduleMode(int mode)
		{
			_settings.ScheduleMode = mode;
			UpdateSegmentedModeButtons();
			_settings.Save();
			CheckScheduleAndApply(true);
		}

		private void UpdateSegmentedModeButtons()
		{
			int scheduleMode = _settings.ScheduleMode;
			SetSegmentButtonState(_btnModeSun, scheduleMode == 0);
			SetSegmentButtonState(_btnModeCustom, scheduleMode == 1);
			SetSegmentButtonState(_btnModeManual, scheduleMode == 2);
			if (_panelSunSettings != null)
			{
				_panelSunSettings.Visible = scheduleMode == 0;
			}
			if (_panelCustomSettings != null)
			{
				_panelCustomSettings.Visible = scheduleMode == 1;
			}
			if (_panelManualSettings != null)
			{
				_panelManualSettings.Visible = scheduleMode == 2;
			}
		}

		private void SetSegmentButtonState(Button btn, bool active)
		{
			if (btn != null)
			{
				if (active)
				{
					btn.BackColor = _accentOrange;
					btn.ForeColor = Color.White;
					btn.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
					btn.FlatAppearance.BorderColor = _accentOrange;
				}
				else
				{
					btn.BackColor = Color.FromArgb(36, 36, 46);
					btn.ForeColor = Color.White;
					btn.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
					btn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 68);
				}
			}
		}

		private static string NormalizeCity(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return string.Empty;
			}
			string text2 = text.ToLowerInvariant().Replace("ı", "i").Replace("ğ", "g")
				.Replace("ü", "u")
				.Replace("ş", "s")
				.Replace("ö", "o")
				.Replace("ç", "c");
			StringBuilder stringBuilder = new StringBuilder();
			string text3 = text2;
			foreach (char c in text3)
			{
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}

		private void SelectCityInCombo(string cityName)
		{
			if (string.IsNullOrEmpty(cityName))
			{
				cityName = "İstanbul (Türkiye)";
			}
			for (int i = 0; i < _cbCity.Items.Count; i++)
			{
				SolarCalculator.CityInfo cityInfo = _cbCity.Items[i] as SolarCalculator.CityInfo;
				if (cityInfo != null && cityInfo.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase))
				{
					_cbCity.SelectedIndex = i;
					return;
				}
			}
			string text = cityName.Split(' ', '(', '-', '_')[0].Trim();
			if (text.Length >= 3)
			{
				for (int i = 0; i < _cbCity.Items.Count; i++)
				{
					SolarCalculator.CityInfo cityInfo = _cbCity.Items[i] as SolarCalculator.CityInfo;
					if (cityInfo != null && cityInfo.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
					{
						_cbCity.SelectedIndex = i;
						return;
					}
				}
			}
			string text2 = cityName.Split('(')[0].Trim();
			string text3 = NormalizeCity(text2);
			if (!string.IsNullOrEmpty(text3) && text3.Length >= 3)
			{
				for (int i = 0; i < _cbCity.Items.Count; i++)
				{
					SolarCalculator.CityInfo cityInfo = _cbCity.Items[i] as SolarCalculator.CityInfo;
					if (cityInfo != null)
					{
						string text4 = cityInfo.Name.Split('(')[0].Trim();
						string text5 = NormalizeCity(text4);
						if (!string.IsNullOrEmpty(text5) && (text5.StartsWith(text3) || text3.StartsWith(text5)))
						{
							_cbCity.SelectedIndex = i;
							return;
						}
					}
				}
			}
			if (_cbCity.Items.Count > 0)
			{
				_cbCity.SelectedIndex = 0;
			}
		}

		private void UpdateSolarCalculations()
		{
			if (_cbCity == null || _cbCity.SelectedItem == null)
			{
				return;
			}
			SolarCalculator.CityInfo cityInfo = _cbCity.SelectedItem as SolarCalculator.CityInfo;
			if (cityInfo != null)
			{
				TimeSpan sunrise;
				TimeSpan sunset;
				SolarCalculator.CalculateSunriseSunset(cityInfo.Latitude, cityInfo.Longitude, DateTime.Now, out sunrise, out sunset);
				string text = string.Format("{0:D2}:{1:D2}", sunrise.Hours, sunrise.Minutes);
				string text2 = string.Format("{0:D2}:{1:D2}", sunset.Hours, sunset.Minutes);
				if (_lblSunriseBadge != null)
				{
					_lblSunriseBadge.Text = Loc.T("SunriseLabel") + "\n" + text;
				}
				if (_lblSunsetBadge != null)
				{
					_lblSunsetBadge.Text = Loc.T("SunsetLabel") + "\n" + text2;
				}
				if (_lblSolarInfo != null)
				{
					_lblSolarInfo.Text = string.Format(Loc.T("SunriseSunset"), text, text2);
				}
			}
		}

		private void SetupTimer()
		{
			_scheduleTimer = new System.Windows.Forms.Timer();
			_scheduleTimer.Interval = 20000;
			_scheduleTimer.Tick += delegate
			{
				CheckScheduleAndApply(false);
			};
			_scheduleTimer.Start();
		}

		private void CheckScheduleAndApply(bool force)
		{
			if (_settings.ScheduleMode == 2)
			{
				if (force)
				{
					ApplyCurrentSettingsImmediate(false);
				}
				return;
			}
			TimeSpan timeOfDay = DateTime.Now.TimeOfDay;
			TimeSpan timeSpan;
			TimeSpan timeSpan2;
			if (_settings.ScheduleMode == 0)
			{
				SolarCalculator.CityInfo cityInfo = ((_cbCity != null && _cbCity.SelectedItem != null) ? (_cbCity.SelectedItem as SolarCalculator.CityInfo) : SolarCalculator.Cities[0]);
				if (cityInfo == null)
				{
					cityInfo = SolarCalculator.Cities[0];
				}
				TimeSpan sunrise;
				TimeSpan sunset;
				SolarCalculator.CalculateSunriseSunset(cityInfo.Latitude, cityInfo.Longitude, DateTime.Now, out sunrise, out sunset);
				timeSpan = sunset;
				timeSpan2 = sunrise;
			}
			else
			{
				timeSpan = _settings.CustomStartTime;
				timeSpan2 = _settings.CustomEndTime;
			}
			bool flag = ((!(timeSpan <= timeSpan2)) ? (timeOfDay >= timeSpan || timeOfDay < timeSpan2) : (timeOfDay >= timeSpan && timeOfDay < timeSpan2));
			if (force)
			{
				_lastScheduleNightState = flag;
				_settings.IsEnabled = flag;
				UpdateToggleButtonState();
				ApplyCurrentSettingsImmediate(false);
			}
			else if (_lastScheduleNightState.HasValue && _lastScheduleNightState.Value != flag)
			{
				_lastScheduleNightState = flag;
				_settings.IsEnabled = flag;
				UpdateToggleButtonState();
				ApplyCurrentSettingsImmediate();
			}
			else if (!_lastScheduleNightState.HasValue)
			{
				_lastScheduleNightState = flag;
			}
		}

		private void UpdateToggleButtonState()
		{
			if (_settings.IsEnabled)
			{
				_btnToggle.Text = Loc.T("NightModeOn");
				_btnToggle.BackColor = _accentOrange;
				_btnToggle.ForeColor = Color.White;
				_lblStatusText.Text = Loc.T("StatusNight");
				_lblStatusText.ForeColor = Color.FromArgb(254, 240, 225);
				if (_lblStatusPill != null)
				{
					_lblStatusPill.Text = Loc.T("StatusPillActive");
					_lblStatusPill.BackColor = Color.FromArgb(58, 28, 14);
					_lblStatusPill.ForeColor = Color.White;
					_lblStatusPill.Invalidate();
				}
			}
			else
			{
				_btnToggle.Text = Loc.T("NightModeOff");
				_btnToggle.BackColor = _offGray;
				_btnToggle.ForeColor = Color.White;
				_lblStatusText.Text = Loc.T("StatusDay");
				_lblStatusText.ForeColor = Color.FromArgb(220, 220, 230);
				if (_lblStatusPill != null)
				{
					_lblStatusPill.Text = Loc.T("StatusPillInactive");
					_lblStatusPill.BackColor = Color.FromArgb(32, 32, 40);
					_lblStatusPill.ForeColor = Color.White;
					_lblStatusPill.Invalidate();
				}
			}
			_lblToggleHint.Text = Loc.T("ClickToToggle");
			LayoutToggleStatus();
		}

		private void UpdateLabels()
		{
			string text = "";
			if (_settings.Kelvin <= 2400)
			{
				text = " (" + Loc.T("DescCandle") + ")";
			}
			else if (_settings.Kelvin <= 3400)
			{
				text = " (" + Loc.T("DescNight") + ")";
			}
			else if (_settings.Kelvin <= 4500)
			{
				text = " (" + Loc.T("DescSunset") + ")";
			}
			else if (_settings.Kelvin >= 6000)
			{
				text = " (" + Loc.T("DescNormal") + ")";
			}
			_lblKelvinVal.Text = _settings.Kelvin + " K" + text;
			_lblBrightnessVal.Text = "%" + _settings.Brightness;
			int kelvin = _settings.Kelvin;
			UpdatePresetButtonHighlight(_btnPresetCandle, kelvin == 2200);
			UpdatePresetButtonHighlight(_btnPresetNight, kelvin == 3400);
			UpdatePresetButtonHighlight(_btnPresetSunset, kelvin == 4500);
			UpdatePresetButtonHighlight(_btnPresetNormal, kelvin == 6500);
		}

		private void ApplyCurrentSettingsImmediate(bool smooth = true)
		{
			if (_settings.IsEnabled)
			{
				_targetK = _settings.Kelvin;
				_targetB = _settings.Brightness;
			}
			else
			{
				_targetK = 6500.0;
				_targetB = 100.0;
			}
			if (smooth)
			{
				if (_fadeTimer != null)
				{
					_fadeTimer.Stop();
					_fadeTimer.Start();
				}
				return;
			}
			_currentK = _targetK;
			_currentB = _targetB;
			if (_fadeTimer != null)
			{
				_fadeTimer.Stop();
			}
			GammaController.SetColorTemperatureAndBrightness((int)_currentK, (int)_currentB);
		}

		private void SetupTrayIcon()
		{
			_trayMenu = new ContextMenuStrip();
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(Loc.T("TrayToggle"));
			toolStripMenuItem.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
			toolStripMenuItem.Click += delegate
			{
				ToggleNightMode();
			};
			ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem(Loc.T("TrayOpen"));
			toolStripMenuItem2.Click += delegate
			{
				ShowWindow();
			};
			ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem(Loc.T("TrayExit"));
			toolStripMenuItem3.Click += delegate
			{
				ExitApplication();
			};
			_trayMenu.Items.Add(toolStripMenuItem);
			_trayMenu.Items.Add(toolStripMenuItem2);
			_trayMenu.Items.Add(new ToolStripSeparator());
			_trayMenu.Items.Add(toolStripMenuItem3);
			_trayIcon = new NotifyIcon
			{
				Text = "ArcLight (Ctrl+Alt+N)",
				Icon = base.Icon,
				ContextMenuStrip = _trayMenu,
				Visible = true
			};
			_trayIcon.MouseClick += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					ShowWindow();
				}
			};
			_trayIcon.DoubleClick += delegate
			{
				ShowWindow();
			};
		}

		public void ShowWindow()
		{
			Show();
			if (base.WindowState == FormWindowState.Minimized)
			{
				base.WindowState = FormWindowState.Normal;
			}
			BringToFront();
			Activate();
			try
			{
				SetForegroundWindow(base.Handle);
			}
			catch
			{
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (e.CloseReason == CloseReason.UserClosing && _settings.MinimizeToTray)
			{
				e.Cancel = true;
				Hide();
				if (!_hasShownTrayBalloon)
				{
					_hasShownTrayBalloon = true;
					_trayIcon.ShowBalloonTip(1500, "ArcLight", Loc.T("ClickToToggle") + " | " + Loc.T("HotkeyHint"), ToolTipIcon.Info);
				}
			}
			else
			{
				ExitApplication();
			}
			base.OnFormClosing(e);
		}

		private void ExitApplication()
		{
			if (!_isExiting)
			{
				_isExiting = true;
                if (_gammaRecoveryTimer != null) { _gammaRecoveryTimer.Stop(); _gammaRecoveryTimer.Dispose(); _gammaRecoveryTimer = null; }
				try
				{
					UnregisterHotKey(base.Handle, 9001);
				}
				catch
				{
				}
				DetachSystemEvents();
				if (_scheduleTimer != null)
				{
					_scheduleTimer.Stop();
					_scheduleTimer.Dispose();
					_scheduleTimer = null;
				}
				if (_saveDebounceTimer != null)
				{
					_saveDebounceTimer.Stop();
					_saveDebounceTimer.Dispose();
					_saveDebounceTimer = null;
				}
				if (_fadeTimer != null)
				{
					_fadeTimer.Stop();
					_fadeTimer.Dispose();
					_fadeTimer = null;
				}
				GammaController.RestoreDefault();
				if (_trayIcon != null)
				{
					_trayIcon.Visible = false;
					_trayIcon.Dispose();
					_trayIcon = null;
				}
				_settings.Save();
				Application.ExitThread();
				Environment.Exit(0);
			}
		}

		public static Bitmap CreateLogoBitmap(int size)
		{
			Bitmap bitmap = new Bitmap(size, size);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.Clear(Color.Transparent);
				float num = size;
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					graphicsPath.AddEllipse(num * 0.02f, num * 0.02f, num * 0.96f, num * 0.96f);
					using (PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath))
					{
						pathGradientBrush.CenterColor = Color.FromArgb(249, 115, 22);
						pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, 249, 115, 22) };
						graphics.FillPath(pathGradientBrush, graphicsPath);
					}
				}
				using (Brush brush = new SolidBrush(Color.FromArgb(22, 22, 28)))
				{
					graphics.FillEllipse(brush, num * 0.04f, num * 0.04f, num * 0.92f, num * 0.92f);
				}
				float num2 = Math.Max(1.2f, num * 0.045f);
				using (Pen pen = new Pen(Color.FromArgb(249, 115, 22), num2))
				{
					graphics.DrawEllipse(pen, num * 0.05f, num * 0.05f, num * 0.9f, num * 0.9f);
				}
				using (GraphicsPath graphicsPath2 = new GraphicsPath())
				{
					PointF pointF = new PointF(num * 0.5f, num * 0.15f);
					PointF pointF2 = new PointF(num * 0.19f, num * 0.83f);
					PointF pointF3 = new PointF(num * 0.33f, num * 0.83f);
					PointF pointF4 = new PointF(num * 0.67f, num * 0.83f);
					PointF pointF5 = new PointF(num * 0.81f, num * 0.83f);
					PointF[] points = new PointF[7]
					{
						pointF,
						pointF5,
						pointF4,
						new PointF(num * 0.63f, num * 0.66f),
						new PointF(num * 0.37f, num * 0.66f),
						pointF3,
						pointF2
					};
					graphicsPath2.AddPolygon(points);
					PointF pointF6 = new PointF(num * 0.5f, num * 0.32f);
					PointF pointF7 = new PointF(num * 0.4f, num * 0.54f);
					PointF pointF8 = new PointF(num * 0.6f, num * 0.54f);
					using (GraphicsPath graphicsPath3 = new GraphicsPath())
					{
						graphicsPath3.AddPolygon(new PointF[3] { pointF6, pointF8, pointF7 });
						using (Region region = new Region(graphicsPath2))
						{
							region.Exclude(graphicsPath3);
							using (Brush brush2 = new SolidBrush(Color.White))
							{
								graphics.FillRegion(brush2, region);
							}
						}
					}
				}
				PointF pointF9 = new PointF(num * 0.5f, num * 0.44f);
				float num3 = num * 0.125f;
				float num4 = num3 * 0.48f;
				PointF[] array = new PointF[10];
				double num5 = Math.PI / 5.0;
				double num6 = -Math.PI / 2.0;
				for (int i = 0; i < 10; i++)
				{
					float num7 = ((i % 2 == 0) ? num3 : num4);
					double num8 = num6 + (double)i * num5;
					array[i] = new PointF(pointF9.X + (float)((double)num7 * Math.Cos(num8)), pointF9.Y + (float)((double)num7 * Math.Sin(num8)));
				}
				using (Brush brush3 = new SolidBrush(Color.FromArgb(251, 191, 36)))
				{
					graphics.FillPolygon(brush3, array);
				}
				using (Pen pen2 = new Pen(Color.FromArgb(217, 119, 6), Math.Max(0.6f, num * 0.015f)))
				{
					graphics.DrawPolygon(pen2, array);
				}
			}
			return bitmap;
		}

		private static Icon CreateAppIcon()
		{
			try
			{
				string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
				if (File.Exists(text))
				{
					return new Icon(text);
				}
				Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
				if (icon != null)
				{
					return icon;
				}
			}
			catch
			{
			}
			using (Bitmap bitmap = CreateLogoBitmap(48))
			{
				IntPtr hicon = bitmap.GetHicon();
				Icon result = (Icon)Icon.FromHandle(hicon).Clone();
				DestroyIcon(hicon);
				return result;
			}
		}

		private void SetAutoStartRegistry(bool enable)
		{
			try
			{
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
				{
					if (registryKey != null)
					{
						if (enable)
						{
							registryKey.SetValue("ArcLight", "\"" + Application.ExecutablePath + "\"");
						}
						else
						{
							registryKey.DeleteValue("ArcLight", false);
						}
					}
				}
			}
			catch
			{
			}
		}
	}
	internal static class Program
	{
		private const string MUTEX_ID = "ArcLight_SingleInstance_Mutex_991823";

		private static Mutex _appMutex;

		private static readonly IntPtr HWND_BROADCAST = new IntPtr(65535);

		public static readonly uint WM_SHOWFIRSTINSTANCE = RegisterWindowMessage("ArcLight_ShowFirstInstance_Msg_991823");

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern uint RegisterWindowMessage(string lpString);

		[DllImport("user32.dll")]
		public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[STAThread]
		private static void Main()
		{
			bool createdNew;
			_appMutex = new Mutex(true, "ArcLight_SingleInstance_Mutex_991823", out createdNew);
			if (!createdNew)
			{
				PostMessage(HWND_BROADCAST, WM_SHOWFIRSTINSTANCE, IntPtr.Zero, IntPtr.Zero);
				IntPtr intPtr = FindWindow(null, "ArcLight");
				if (intPtr != IntPtr.Zero)
				{
					ShowWindow(intPtr, 9);
					SetForegroundWindow(intPtr);
				}
				return;
			}
			try
			{
				MainForm.SetCurrentProcessExplicitAppUserModelID("ArcLight.NightLight.DesktopApp");
			}
			catch
			{
			}
			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hata.log"), ex.ToString(), Encoding.UTF8);
			}
			finally
			{
				if (_appMutex != null)
				{
					_appMutex.ReleaseMutex();
					_appMutex.Dispose();
				}
			}
		}
	}
}
