using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

// Explicit namespace alias to prevent WPF / WinForms conflicts
using Application = System.Windows.Application;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using MouseButtons = System.Windows.Forms.MouseButtons;

namespace Munyu
{
    public partial class App : Application
    {
        private const string MutexName = @"Global\Munyu_SingleInstance_Mutex_9988";
        public const string ToggleEventName = @"Global\Munyu_Toggle_Event_9988";

        private Mutex? _singleInstanceMutex;
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Single Instance Check via Named Mutex
            bool isNewInstance = false;
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out isNewInstance);
            }
            catch (AbandonedMutexException)
            {
                isNewInstance = true;
            }

            if (!isNewInstance)
            {
                // Signal 1st instance via System EventWaitHandle to toggle island visibility
                try
                {
                    using (var toggleEvent = EventWaitHandle.OpenExisting(ToggleEventName))
                    {
                        toggleEvent.Set();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error signaling toggle event: {ex.Message}");
                }

                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 2. Create System Tray Icon
            InitializeNotifyIcon();

            // 3. Instantiate and show MainWindow docked at screen edge
            _mainWindow = new MainWindow();
            _mainWindow.ShowWindow(); // Show island immediately on startup
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "Munyu (Ctrl+Shift+M)";

            // Create custom tray icon: Sleek black rounded square with cute eyes!
            using (var bitmap = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 1. Black Rounded Square Container
                using (var path = CreateRoundedRectanglePath(new RectangleF(1, 1, 30, 30), 6))
                using (var bgBrush = new SolidBrush(Color.FromArgb(10, 10, 12)))
                using (var borderPen = new Pen(Color.FromArgb(63, 63, 70), 1.2f))
                {
                    g.FillPath(bgBrush, path);
                    g.DrawPath(borderPen, path);
                }

                // 2. Pair of Cute White Eyes
                // Left Eye Sclera (White)
                g.FillEllipse(Brushes.White, 7, 11, 8, 8);
                // Right Eye Sclera (White)
                g.FillEllipse(Brushes.White, 17, 11, 8, 8);

                // Left & Right Pupil (Dark Black)
                using (var pupilBrush = new SolidBrush(Color.FromArgb(15, 15, 18)))
                {
                    g.FillEllipse(pupilBrush, 9, 13, 4, 4);
                    g.FillEllipse(pupilBrush, 19, 13, 4, 4);
                }

                // White Catchlight Highlights
                g.FillEllipse(Brushes.White, 11.5f, 13, 1.5f, 1.5f);
                g.FillEllipse(Brushes.White, 21.5f, 13, 1.5f, 1.5f);

                IntPtr hIcon = bitmap.GetHicon();
                _notifyIcon.Icon = Icon.FromHandle(hIcon);
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Click += (s, e) =>
            {
                if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
                {
                    _mainWindow?.ToggleWindow();
                }
            };

            // Context Menu
            var contextMenu = new ContextMenuStrip();
            var toggleItem = new ToolStripMenuItem("Toggle (Ctrl+Shift+M)", null, (s, e) => _mainWindow?.ToggleWindow());
            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitApp());

            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ExitApp()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _singleInstanceMutex?.ReleaseMutex();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ExitApp();
            base.OnExit(e);
        }
    }
}
