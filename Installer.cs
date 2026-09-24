using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ArcLightInstaller
{
    public class InstallerForm : Form
    {
        private Color _bgDark = Color.FromArgb(18, 18, 22);
        private Color _cardDark = Color.FromArgb(28, 28, 34);
        private Color _accentOrange = Color.FromArgb(249, 115, 22);
        private Color _accentGold = Color.FromArgb(251, 191, 36);

        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private CheckBox _chkAutoStart;
        private CheckBox _chkLaunchNow;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private Button _btnInstall;
        private bool _installationComplete;
        private string _installedExe;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
                DwmSetWindowAttribute(this.Handle, 19, ref darkMode, sizeof(int));
            }
            catch { }
        }

        public InstallerForm()
        {
            this.Text = "ArcLight Kurulum Sihirbazı";
            this.Size = new Size(500, 390);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = _bgDark;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Icon = CreateAppIcon();

            InitializeUI();
        }

        private void InitializeUI()
        {
            // Üst Başlık ve Logo
            Panel headerPanel = new Panel
            {
                Location = new Point(20, 16),
                Size = new Size(446, 50),
                BackColor = Color.Transparent
            };

            PictureBox picLogo = new PictureBox
            {
                Image = CreateLogoBitmap(42),
                Size = new Size(42, 42),
                Location = new Point(4, 2),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            headerPanel.Controls.Add(picLogo);

            Label lblTitle = new Label
            {
                Text = "ArcLight Kurulumu",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(56, 2),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblTitle);

            Label lblSub = new Label
            {
                Text = "Akıllı Gece Modu & Mavi Işık Filtresi (v1.0)",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(180, 180, 190),
                Location = new Point(58, 28),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblSub);
            this.Controls.Add(headerPanel);

            // Seçenekler Kartı
            Panel cardOptions = new Panel
            {
                Location = new Point(20, 78),
                Size = new Size(446, 170),
                BackColor = _cardDark
            };
            cardOptions.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(50, 50, 60), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, cardOptions.Width - 1, cardOptions.Height - 1);
                }
            };

            Label lblOptsTitle = new Label
            {
                Text = "Kurulum Seçenekleri",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = _accentGold,
                Location = new Point(14, 12),
                AutoSize = true
            };
            cardOptions.Controls.Add(lblOptsTitle);

            _chkDesktop = new CheckBox
            {
                Text = "Masaüstü kısayolu oluştur",
                Location = new Point(18, 42),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            cardOptions.Controls.Add(_chkDesktop);

            _chkStartMenu = new CheckBox
            {
                Text = "Başlat Menüsüne ekle",
                Location = new Point(18, 70),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            cardOptions.Controls.Add(_chkStartMenu);

            _chkAutoStart = new CheckBox
            {
                Text = "Windows açıldığında otomatik başlasın",
                Location = new Point(18, 98),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            cardOptions.Controls.Add(_chkAutoStart);

            _chkLaunchNow = new CheckBox
            {
                Text = "Kurulum tamamlandığında ArcLight'ı çalıştır",
                Location = new Point(18, 126),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            cardOptions.Controls.Add(_chkLaunchNow);

            this.Controls.Add(cardOptions);

            // İlerleme ve Durum
            _lblStatus = new Label
            {
                Text = "Kuruluma hazır.",
                Location = new Point(20, 258),
                Size = new Size(446, 20),
                ForeColor = Color.FromArgb(200, 200, 210),
                Font = new Font("Segoe UI", 9f)
            };
            this.Controls.Add(_lblStatus);

            _progressBar = new ProgressBar
            {
                Location = new Point(20, 280),
                Size = new Size(446, 12),
                Visible = false
            };
            this.Controls.Add(_progressBar);

            // Buton
            _btnInstall = new Button
            {
                Text = "🚀 Hemen Kur",
                Location = new Point(20, 302),
                Size = new Size(446, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = _accentOrange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += (s, e) => { if (_installationComplete) { if (_chkLaunchNow.Checked) System.Diagnostics.Process.Start(_installedExe); Application.Exit(); } else StartInstallation(); };
            this.Controls.Add(_btnInstall);
        }

        private void StartInstallation()
        {
            _btnInstall.Enabled = false;
            _progressBar.Visible = true;
            _progressBar.Value = 10;
            _lblStatus.Text = "Açık çalışan ArcLight süreçleri kontrol ediliyor...";
            Application.DoEvents();

            try
            {
                string existingExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcLight", "ArcLight.exe");
                foreach (var process in System.Diagnostics.Process.GetProcessesByName("ArcLight"))
                {
                    using (process)
                    {
                        if (!process.HasExited && string.Equals(process.MainModule.FileName, existingExe, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Önce ArcLight içindeki Çık düğmesiyle uygulamayı kapatın, sonra yeniden deneyin.");
                    }
                }
                _progressBar.Value = 30;
                _lblStatus.Text = "ArcLight dosyaları kopyalanıyor...";
                Application.DoEvents();
                // Hedef Klasör: %LOCALAPPDATA%\ArcLight
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string installDir = Path.Combine(localAppData, "ArcLight");
                if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);

                string targetExe = Path.Combine(installDir, "ArcLight.exe");

                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArcLight.exe"))
                {
                    if (resource == null) throw new InvalidDataException("Uygulama dosyası kurulum paketinde bulunamadı.");
                    string stagedExe = targetExe + ".new";
                    using (FileStream output = new FileStream(stagedExe, FileMode.Create, FileAccess.Write))
                    {
                        resource.CopyTo(output);
                        output.Flush(true);
                    }
                    if (File.Exists(targetExe)) File.Replace(stagedExe, targetExe, targetExe + ".bak", true);
                    else File.Move(stagedExe, targetExe);
                }
                _progressBar.Value = 60;
                _lblStatus.Text = "Kısayollar ve kayıtlar oluşturuluyor...";
                Application.DoEvents();

                // Masaüstü Kısayolu
                if (_chkDesktop.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "ArcLight.lnk"), targetExe, installDir);
                }

                // Başlat Menüsü Kısayolu
                if (_chkStartMenu.Checked)
                {
                    string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    CreateShortcut(Path.Combine(startMenu, "ArcLight.lnk"), targetExe, installDir);
                }

                // Windows Başlangıç Kaydı
                if (_chkAutoStart.Checked)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null) key.SetValue("ArcLight", "\"" + targetExe + "\"");
                    }
                }

                // Windows Program Ekle / Kaldır Kaydı (Uninstall)
                RegisterUninstaller(installDir, targetExe);

                _progressBar.Value = 100;
                _lblStatus.Text = "✅ ArcLight başarıyla kuruldu!";
                _lblStatus.ForeColor = Color.FromArgb(74, 222, 128);

                _btnInstall.Text = "🎉 Tamamla ve Kapat";
                _btnInstall.BackColor = Color.FromArgb(22, 101, 52);
                _btnInstall.Enabled = true;
                _installedExe = targetExe;
                _installationComplete = true;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Hata: " + ex.Message;
                _lblStatus.ForeColor = Color.Red;
                _btnInstall.Enabled = true;
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workDir;
                shortcut.Description = "ArcLight - Akıllı Gece Modu & Mavi Işık Filtresi";
                shortcut.Save();
            }
            catch { }
        }

        private void RegisterUninstaller(string installDir, string targetExe)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ArcLight";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "ArcLight");
                        key.SetValue("DisplayVersion", "0.3.1");
                        key.SetValue("Publisher", "ArcLight");
                        key.SetValue("DisplayIcon", targetExe);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", "cmd /c taskkill /f /im ArcLight.exe & timeout /t 1 >nul & rmdir /s /q \"" + installDir + "\" & del /q \"%USERPROFILE%\\Desktop\\ArcLight.lnk\" & del /q \"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\ArcLight.lnk\" & reg delete HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ArcLight /f");
                    }
                }
            }
            catch { }
        }

        private static Bitmap CreateLogoBitmap(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float s = (float)size;

                using (GraphicsPath pGlow = new GraphicsPath())
                {
                    pGlow.AddEllipse(s * 0.02f, s * 0.02f, s * 0.96f, s * 0.96f);
                    using (PathGradientBrush pgb = new PathGradientBrush(pGlow))
                    {
                        pgb.CenterColor = Color.FromArgb(249, 115, 22);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, 249, 115, 22) };
                        g.FillPath(pgb, pGlow);
                    }
                }

                using (Brush bgBrush = new SolidBrush(Color.FromArgb(22, 22, 28)))
                {
                    g.FillEllipse(bgBrush, s * 0.04f, s * 0.04f, s * 0.92f, s * 0.92f);
                }

                float penW = Math.Max(1.2f, s * 0.045f);
                using (Pen ringPen = new Pen(Color.FromArgb(249, 115, 22), penW))
                {
                    g.DrawEllipse(ringPen, s * 0.05f, s * 0.05f, s * 0.90f, s * 0.90f);
                }

                using (GraphicsPath aPath = new GraphicsPath())
                {
                    PointF topOuter = new PointF(s * 0.50f, s * 0.15f);
                    PointF btmLeftOuter = new PointF(s * 0.19f, s * 0.83f);
                    PointF btmLeftInner = new PointF(s * 0.33f, s * 0.83f);
                    PointF btmRightInner = new PointF(s * 0.67f, s * 0.83f);
                    PointF btmRightOuter = new PointF(s * 0.81f, s * 0.83f);

                    PointF[] aOutline = new PointF[]
                    {
                        topOuter,
                        btmRightOuter,
                        btmRightInner,
                        new PointF(s * 0.63f, s * 0.66f),
                        new PointF(s * 0.37f, s * 0.66f),
                        btmLeftInner,
                        btmLeftOuter
                    };
                    aPath.AddPolygon(aOutline);

                    PointF topHole = new PointF(s * 0.50f, s * 0.32f);
                    PointF leftHole = new PointF(s * 0.40f, s * 0.54f);
                    PointF rightHole = new PointF(s * 0.60f, s * 0.54f);
                    GraphicsPath holePath = new GraphicsPath();
                    holePath.AddPolygon(new PointF[] { topHole, rightHole, leftHole });

                    using (Region reg = new Region(aPath))
                    {
                        reg.Exclude(holePath);
                        using (Brush aBrush = new SolidBrush(Color.White))
                        {
                            g.FillRegion(aBrush, reg);
                        }
                    }
                }

                PointF starCenter = new PointF(s * 0.50f, s * 0.44f);
                float outerR = s * 0.125f;
                float innerR = outerR * 0.48f;
                PointF[] starPts = new PointF[10];
                double step = Math.PI / 5.0;
                double startAngle = -Math.PI / 2.0;
                for (int i = 0; i < 10; i++)
                {
                    float r = (i % 2 == 0) ? outerR : innerR;
                    double a = startAngle + i * step;
                    starPts[i] = new PointF(starCenter.X + (float)(r * Math.Cos(a)), starCenter.Y + (float)(r * Math.Sin(a)));
                }

                using (Brush starBrush = new SolidBrush(Color.FromArgb(251, 191, 36)))
                {
                    g.FillPolygon(starBrush, starPts);
                }
                using (Pen starBorder = new Pen(Color.FromArgb(217, 119, 6), Math.Max(0.6f, s * 0.015f)))
                {
                    g.DrawPolygon(starBorder, starPts);
                }
            }
            return bmp;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon CreateAppIcon()
        {
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath)) return new Icon(icoPath);
                Icon assoc = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (assoc != null) return assoc;
            }
            catch { }
            using (Bitmap bmp = CreateLogoBitmap(48))
            {
                IntPtr hIcon = bmp.GetHicon();
                Icon ico = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return ico;
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }
}

