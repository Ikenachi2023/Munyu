using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

// Explicit aliases for WPF types vs WinForms types
using Window = System.Windows.Window;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using ScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;

using Munyu.Controls;
using Munyu.Models;
using Munyu.Services;

namespace Munyu
{
    public enum DockPosition
    {
        Right,
        Left,
        Top,
        Bottom
    }

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private readonly ObservableCollection<MunyuItem> _items = new();
        private HotKeyManager? _hotKeyManager;
        private MunyuItem? _selectedItem;
        private bool _isVisibleState = false;

        private EventWaitHandle? _toggleEventHandle;
        private Thread? _toggleListenerThread;
        private volatile bool _isListeningToggle = true;

        // Default initial dock position set to Top (画面上部)
        private DockPosition _currentDock = DockPosition.Top;
        
        // Dimensions for docking states
        private double _dockWidth = -1;  // Length when Top/Bottom, Thickness when Left/Right
        private double _dockHeight = -1; // Length when Left/Right, Thickness when Top/Bottom

        private const double DefaultThickness = 140; // Default thickness for non-length side
        private const double MinDimensionLimit = 90; // Minimum size for resizable side

        // Slime Edge Sliding state
        private bool _isSliding = false;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            // Setup Drag & Drop Handlers
            DragOver += MainWindow_DragOver;
            Drop += MainWindow_Drop;

            // Load persisted items
            LoadItems();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            _hotKeyManager = new HotKeyManager(ToggleWindow);
            _hotKeyManager.Register(hWnd);

            // Hook CompositionTarget.Rendering for continuous mouse eye tracking
            CompositionTarget.Rendering += (s, ev) => UpdateEyesTracking();
        }

        private void StartToggleEventListener()
        {
            try
            {
                _toggleEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, App.ToggleEventName);
                _toggleListenerThread = new Thread(() =>
                {
                    while (_isListeningToggle && _toggleEventHandle != null)
                    {
                        if (_toggleEventHandle.WaitOne(500))
                        {
                            Dispatcher.Invoke(() => ToggleWindow());
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "Munyu_Toggle_Listener_Thread"
                };
                _toggleListenerThread.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting toggle listener: {ex.Message}");
            }
        }

        private void UpdateEyesTracking()
        {
            if (!_isVisibleState || !IsVisible) return;

            if (GetCursorPos(out POINT pt))
            {
                try
                {
                    DpiScale dpi = VisualTreeHelper.GetDpi(this);
                    double mouseX = pt.X / dpi.DpiScaleX;
                    double mouseY = pt.Y / dpi.DpiScaleY;

                    System.Windows.Point leftEyeScreen = LeftPupilGrid.PointToScreen(new System.Windows.Point(5, 5));
                    double leftEyeX = leftEyeScreen.X / dpi.DpiScaleX;
                    double leftEyeY = leftEyeScreen.Y / dpi.DpiScaleY;
                    UpdateSingleEye(mouseX, mouseY, leftEyeX, leftEyeY, LeftPupilTransform);

                    System.Windows.Point rightEyeScreen = RightPupilGrid.PointToScreen(new System.Windows.Point(5, 5));
                    double rightEyeX = rightEyeScreen.X / dpi.DpiScaleX;
                    double rightEyeY = rightEyeScreen.Y / dpi.DpiScaleY;
                    UpdateSingleEye(mouseX, mouseY, rightEyeX, rightEyeY, RightPupilTransform);
                }
                catch
                {
                    // Ignore if visual source is not connected yet
                }
            }
        }

        private void UpdateSingleEye(double mouseX, double mouseY, double eyeX, double eyeY, TranslateTransform transform)
        {
            double dx = mouseX - eyeX;
            double dy = mouseY - eyeY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > 0.001)
            {
                double maxOffset = 3.5;
                double offset = Math.Min(dist / 35.0, maxOffset);
                double angle = Math.Atan2(dy, dx);

                transform.X = offset * Math.Cos(angle);
                transform.Y = offset * Math.Sin(angle);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyDockLayout(_currentDock);
            StartToggleEventListener();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _isListeningToggle = false;
            _toggleEventHandle?.Dispose();
            _hotKeyManager?.Dispose();
        }

        #region Clean Edge Sliding (画面端移動)
        private void HeaderGripBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _isSliding = true;
                HeaderGripBar.CaptureMouse();
                HeaderGripBar.MouseMove += HeaderGripBar_MouseMove;
                HeaderGripBar.MouseLeftButtonUp += HeaderGripBar_MouseLeftButtonUp;
            }
        }

        private void HeaderGripBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSliding) return;

            if (GetCursorPos(out POINT pt))
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                double mouseX = pt.X / dpi.DpiScaleX;
                double mouseY = pt.Y / dpi.DpiScaleY;

                Rect workArea = MonitorHelper.GetActiveMonitorWorkArea(this);

                double distRight = Math.Abs(mouseX - workArea.Right);
                double distLeft = Math.Abs(mouseX - workArea.Left);
                double distTop = Math.Abs(mouseY - workArea.Top);
                double distBottom = Math.Abs(mouseY - workArea.Bottom);

                double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

                DockPosition targetDock;
                if (minDist == distRight) targetDock = DockPosition.Right;
                else if (minDist == distLeft) targetDock = DockPosition.Left;
                else if (minDist == distTop) targetDock = DockPosition.Top;
                else targetDock = DockPosition.Bottom;

                if (_currentDock != targetDock)
                {
                    bool isOldVertical = (_currentDock == DockPosition.Right || _currentDock == DockPosition.Left);
                    bool isNewVertical = (targetDock == DockPosition.Right || targetDock == DockPosition.Left);

                    if (isOldVertical != isNewVertical && _dockWidth > 0 && _dockHeight > 0)
                    {
                        double temp = _dockWidth;
                        _dockWidth = _dockHeight;
                        _dockHeight = temp;
                    }
                    _currentDock = targetDock;
                    ApplyDockLayout(_currentDock);
                }

                // Simple & direct edge sliding without any deformation/animation
                if (_currentDock == DockPosition.Right)
                {
                    Left = workArea.Right - Width;
                    Top = Math.Max(workArea.Top, Math.Min(mouseY - 30, workArea.Bottom - Height));
                }
                else if (_currentDock == DockPosition.Left)
                {
                    Left = workArea.Left;
                    Top = Math.Max(workArea.Top, Math.Min(mouseY - 30, workArea.Bottom - Height));
                }
                else if (_currentDock == DockPosition.Top)
                {
                    Top = workArea.Top;
                    Left = Math.Max(workArea.Left, Math.Min(mouseX - 30, workArea.Right - Width));
                }
                else if (_currentDock == DockPosition.Bottom)
                {
                    Top = workArea.Bottom - Height;
                    Left = Math.Max(workArea.Left, Math.Min(mouseX - 30, workArea.Right - Width));
                }
            }
        }

        private void HeaderGripBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSliding)
            {
                _isSliding = false;
                HeaderGripBar.MouseMove -= HeaderGripBar_MouseMove;
                HeaderGripBar.MouseLeftButtonUp -= HeaderGripBar_MouseLeftButtonUp;
                HeaderGripBar.ReleaseMouseCapture();

                ApplyDockLayout(_currentDock);
            }
        }

        public void ApplyDockLayout(DockPosition dock)
        {
            _currentDock = dock;
            Rect workArea = MonitorHelper.GetActiveMonitorWorkArea(this);

            if (_currentDock == DockPosition.Right || _currentDock == DockPosition.Left)
            {
                // Left/Right Dock
                if (_dockHeight <= 0) _dockHeight = Math.Round(workArea.Height * 0.27);
                if (_dockWidth <= 0) _dockWidth = DefaultThickness;

                Width = Math.Max(MinDimensionLimit, Math.Min(_dockWidth, workArea.Width / 2.0));
                double newHeight = Math.Max(MinDimensionLimit, Math.Min(_dockHeight, workArea.Height));

                double currentCenterY = Top + Height / 2.0;
                if (double.IsNaN(currentCenterY) || currentCenterY <= 0)
                {
                    currentCenterY = workArea.Top + workArea.Height / 2.0;
                }

                Height = newHeight;

                if (Math.Abs(Height - workArea.Height) < 1.0)
                {
                    Top = workArea.Top;
                }
                else if (!_isSliding)
                {
                    Top = Math.Max(workArea.Top, Math.Min(currentCenterY - Height / 2.0, workArea.Bottom - Height));
                }

                if (_currentDock == DockPosition.Right)
                {
                    Left = workArea.Right - Width;
                    MainContainer.CornerRadius = new CornerRadius(16, 0, 0, 16);
                    MainContainer.BorderThickness = new Thickness(1, 1, 0, 1);
                }
                else
                {
                    Left = workArea.Left;
                    MainContainer.CornerRadius = new CornerRadius(0, 16, 16, 0);
                    MainContainer.BorderThickness = new Thickness(0, 1, 1, 1);
                }

                // Grid Column/Row Definitions for Vertical Layout
                HeaderCol.Width = new GridLength(1, GridUnitType.Star);
                ContentCol.Width = new GridLength(0);
                FooterCol.Width = new GridLength(0);

                HeaderRow.Height = GridLength.Auto;
                ContentRow.Height = new GridLength(1, GridUnitType.Star);
                FooterRow.Height = GridLength.Auto;

                Grid.SetRow(HeaderGripBar, 0); Grid.SetColumn(HeaderGripBar, 0);
                Grid.SetRow(MainScrollViewer, 1); Grid.SetColumn(MainScrollViewer, 0);

                HeaderStackPanel.Orientation = Orientation.Vertical;
                DragPill.Width = 28; DragPill.Height = 4; DragPill.Margin = new Thickness(0, 0, 0, 4);

                MainScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                MainScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                ItemsWrapPanel.Orientation = Orientation.Horizontal;
                ItemsWrapPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                ItemsWrapPanel.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            }
            else
            {
                // Top/Bottom Dock (Default is Top, 27% screen width)
                if (_dockWidth <= 0) _dockWidth = Math.Round(workArea.Width * 0.27);
                if (_dockHeight <= 0) _dockHeight = DefaultThickness;

                Height = Math.Max(MinDimensionLimit, Math.Min(_dockHeight, workArea.Height / 2.0));
                double newWidth = Math.Max(MinDimensionLimit, Math.Min(_dockWidth, workArea.Width));

                double currentCenterX = Left + Width / 2.0;
                if (double.IsNaN(currentCenterX) || currentCenterX <= 0)
                {
                    currentCenterX = workArea.Left + workArea.Width / 2.0;
                }

                Width = newWidth;

                if (Math.Abs(Width - workArea.Width) < 1.0)
                {
                    Left = workArea.Left;
                }
                else if (!_isSliding)
                {
                    Left = Math.Max(workArea.Left, Math.Min(currentCenterX - Width / 2.0, workArea.Right - Width));
                }

                if (_currentDock == DockPosition.Top)
                {
                    Top = workArea.Top;
                    MainContainer.CornerRadius = new CornerRadius(0, 0, 16, 16);
                    MainContainer.BorderThickness = new Thickness(1, 0, 1, 1);
                }
                else
                {
                    Top = workArea.Bottom - Height;
                    MainContainer.CornerRadius = new CornerRadius(16, 16, 0, 0);
                    MainContainer.BorderThickness = new Thickness(1, 1, 1, 0);
                }

                // Grid Column/Row Definitions for Horizontal Layout
                HeaderRow.Height = new GridLength(1, GridUnitType.Star);
                ContentRow.Height = new GridLength(0);
                FooterRow.Height = new GridLength(0);

                HeaderCol.Width = GridLength.Auto;
                ContentCol.Width = new GridLength(1, GridUnitType.Star);
                FooterCol.Width = GridLength.Auto;

                Grid.SetRow(HeaderGripBar, 0); Grid.SetColumn(HeaderGripBar, 0);
                Grid.SetRow(MainScrollViewer, 0); Grid.SetColumn(MainScrollViewer, 1);

                HeaderStackPanel.Orientation = Orientation.Horizontal;
                DragPill.Width = 4; DragPill.Height = 28; DragPill.Margin = new Thickness(0, 0, 6, 0);

                MainScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                MainScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                ItemsWrapPanel.Orientation = Orientation.Vertical;
                ItemsWrapPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                ItemsWrapPanel.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            }

            RenderItems();
        }
        #endregion

        #region Window Positioning & Visibility
        public void ShowWindow()
        {
            WindowState = WindowState.Normal;
            ApplyDockLayout(_currentDock);
            Show();
            Topmost = true;
            Activate();
            Focus();
            _isVisibleState = true;
        }

        public void HideWindow()
        {
            Hide();
            _isVisibleState = false;
        }

        public void ToggleWindow()
        {
            if (_isVisibleState)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Responsive, brisk scroll acceleration (28px per wheel notch)
            double step = e.Delta > 0 ? 28 : -28;
            Rect workArea = MonitorHelper.GetActiveMonitorWorkArea(this);

            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) ||
                                  Keyboard.IsKeyDown(Key.RightShift) ||
                                  Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (_currentDock == DockPosition.Right || _currentDock == DockPosition.Left)
            {
                if (isShiftPressed)
                {
                    // Shift + Wheel: Adjust Thickness (Width)
                    double maxThickness = workArea.Width / 2.0;
                    _dockWidth = Math.Max(MinDimensionLimit, Math.Min(_dockWidth + step, maxThickness));
                }
                else
                {
                    // Plain Wheel: Adjust Length along edge (Height) Symmetrically
                    _dockHeight = Math.Max(MinDimensionLimit, Math.Min(_dockHeight + step, workArea.Height));
                }
            }
            else
            {
                if (isShiftPressed)
                {
                    // Shift + Wheel: Adjust Thickness (Height)
                    double maxThickness = workArea.Height / 2.0;
                    _dockHeight = Math.Max(MinDimensionLimit, Math.Min(_dockHeight + step, maxThickness));
                }
                else
                {
                    // Plain Wheel: Adjust Length along edge (Width) Symmetrically
                    _dockWidth = Math.Max(MinDimensionLimit, Math.Min(_dockWidth + step, workArea.Width));
                }
            }

            ApplyDockLayout(_currentDock);
            e.Handled = true;
        }
        #endregion

        #region Items Management & Rendering
        private void LoadItems()
        {
            _items.Clear();
            var loaded = DataManager.LoadData();
            foreach (var item in loaded)
            {
                _items.Add(item);
            }

            RenderItems();
        }

        private void SaveItems()
        {
            DataManager.SaveData(_items);
            UpdateEmptyHint();
        }

        private void RenderItems()
        {
            ItemsWrapPanel.Children.Clear();
            bool isVerticalDock = (_currentDock == DockPosition.Right || _currentDock == DockPosition.Left);

            // Compute optimal card scale for standalone cards based on container width/height
            double standaloneCardW = 76;
            double standaloneCardH = 86;

            if (isVerticalDock)
            {
                double usableW = Math.Max(70, Width - 16);
                int cols = (int)Math.Max(1, Math.Floor(usableW / 82.0));
                if (cols > 0)
                {
                    double calcW = (usableW - (cols * 8)) / cols;
                    if (calcW < 76)
                    {
                        standaloneCardW = Math.Max(52, calcW);
                        standaloneCardH = Math.Round(standaloneCardW * (86.0 / 76.0));
                    }
                }
            }
            else
            {
                double usableH = Math.Max(70, Height - 16);
                int rows = (int)Math.Max(1, Math.Floor(usableH / 92.0));
                if (rows > 0)
                {
                    double calcH = (usableH - (rows * 8)) / rows;
                    if (calcH < 86)
                    {
                        standaloneCardH = Math.Max(60, calcH);
                        standaloneCardW = Math.Round(standaloneCardH * (76.0 / 86.0));
                    }
                }
            }

            foreach (var item in _items)
            {
                if (item.Type == "binder" && item.IsExpanded)
                {
                    var binderGroup = new BinderGroupControl
                    {
                        BinderItem = item
                    };

                    binderGroup.UpdateBoundsConstraint(Width, Height, isVerticalDock);

                    binderGroup.CollapseRequested += (s, b) =>
                    {
                        b.IsExpanded = false;
                        SaveItems();
                        RenderItems();
                    };

                    binderGroup.ChildItemSelected += (s, child) => SelectItem(child);
                    binderGroup.ChildItemDeleteRequested += (s, child) =>
                    {
                        if (item.Children == null || item.Children.Count == 0)
                        {
                            _items.Remove(item);
                        }
                        SaveItems();
                        RenderItems();
                    };
                    binderGroup.DragOutCompleted += Card_DragOutCompleted;

                    ItemsWrapPanel.Children.Add(binderGroup);
                }
                else
                {
                    var card = new ItemCard
                    {
                        Item = item
                    };

                    card.SetCardScale(standaloneCardW, standaloneCardH);

                    card.ItemSelected += Card_ItemSelected;
                    card.ItemDeleteRequested += Card_ItemDeleteRequested;
                    card.BinderUnpackRequested += Card_BinderUnpackRequested;
                    card.BinderExpandRequested += (s, b) =>
                    {
                        b.IsExpanded = true;
                        SaveItems();
                        RenderItems();
                    };
                    card.DragOutCompleted += Card_DragOutCompleted;

                    ItemsWrapPanel.Children.Add(card);
                }
            }

            UpdateEmptyHint();
        }

        private void UpdateEmptyHint()
        {
            // Empty hint removed per user preference
        }

        private void SelectItem(MunyuItem item)
        {
            foreach (var i in _items)
            {
                i.IsSelected = false;
            }

            _selectedItem = item;
            _selectedItem.IsSelected = true;
        }

        private void Card_ItemSelected(object? sender, MunyuItem item)
        {
            SelectItem(item);
        }

        private void Card_ItemDeleteRequested(object? sender, MunyuItem item)
        {
            DeleteItem(item);
        }

        private void Card_BinderUnpackRequested(object? sender, MunyuItem item)
        {
            UnpackBinder(item);
        }

        private void Card_DragOutCompleted(object? sender, EventArgs e)
        {
            // Keep island open after drag out
        }

        private void DeleteItem(MunyuItem item)
        {
            if (_items.Remove(item))
            {
                if (_selectedItem == item)
                {
                    _selectedItem = null;
                }
                SaveItems();
                RenderItems();
            }
        }

        private void UnpackBinder(MunyuItem binderItem)
        {
            if (binderItem.Type != "binder" || binderItem.Children == null) return;

            int index = _items.IndexOf(binderItem);
            if (index < 0) return;

            _items.RemoveAt(index);

            int insertIdx = index;
            foreach (var child in binderItem.Children)
            {
                child.IconSource = IconService.GetIconForItem(child.Type, child.Content);
                _items.Insert(insertIdx++, child);
            }

            SaveItems();
            RenderItems();
        }
        #endregion

        #region Keyboard Handlers
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWindow();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (_selectedItem != null)
                {
                    DeleteItem(_selectedItem);
                    e.Handled = true;
                }
            }
        }
        #endregion

        #region Drag and Drop Input (Dropping onto Munyu)
        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            // Reject drops that originated from Munyu itself (島から島へのD&D無効化)
            if (e.Data.GetDataPresent("Munyu_Internal_Item_Drag"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                e.Data.GetDataPresent(DataFormats.Text) ||
                e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            // Reject drops that originated from Munyu itself (島から島へのD&D無効化)
            if (e.Data.GetDataPresent("Munyu_Internal_Item_Drag"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // 1. Files dropped
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    if (files.Length == 1)
                    {
                        AddFileItem(files[0]);
                    }
                    else
                    {
                        AddBinderItem(files);
                    }
                }
            }
            // 2. Text / URL dropped
            else if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                string text = (e.Data.GetData(DataFormats.UnicodeText) as string) ??
                              (e.Data.GetData(DataFormats.Text) as string) ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Trim();
                    if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uriResult) &&
                        (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                    {
                        AddUrlItem(text);
                    }
                    else
                    {
                        AddTextItem(text);
                    }
                }
            }

            SaveItems();
            RenderItems();
            e.Handled = true;
        }

        private void AddFileItem(string path)
        {
            var item = new MunyuItem
            {
                Type = "file",
                Content = path,
                CreatedAt = DateTime.UtcNow,
                IconSource = IconService.GetIconForItem("file", path)
            };
            _items.Add(item);
        }

        private void AddBinderItem(IEnumerable<string> filePaths)
        {
            var children = filePaths.Select(path => new MunyuItem
            {
                Type = "file",
                Content = path,
                CreatedAt = DateTime.UtcNow,
                IconSource = IconService.GetIconForItem("file", path)
            }).ToList();

            var binder = new MunyuItem
            {
                Type = "binder",
                Content = "Binder",
                Children = children,
                CreatedAt = DateTime.UtcNow,
                IconSource = IconService.GetIconForItem("binder", string.Empty)
            };

            _items.Add(binder);
        }

        private void AddUrlItem(string url)
        {
            var item = new MunyuItem
            {
                Type = "url",
                Content = url,
                CreatedAt = DateTime.UtcNow,
                IconSource = IconService.GetIconForItem("url", url)
            };
            _items.Add(item);
        }

        private void AddTextItem(string text)
        {
            var item = new MunyuItem
            {
                Type = "text",
                Content = text,
                CreatedAt = DateTime.UtcNow,
                IconSource = IconService.GetIconForItem("text", text)
            };
            _items.Add(item);
        }
        #endregion
    }
}