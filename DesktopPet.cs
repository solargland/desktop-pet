using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DesktopPet
{

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new DesktopPetApp();
        app.Run();
    }
}

internal sealed class DesktopPetApp : Application
{
    private MainWindow _mainWindow;
    private Forms.NotifyIcon _trayIcon;
    private Drawing.Bitmap _trayBitmap;
    private Drawing.Icon _trayImageIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsStore.Load();
        _mainWindow = new MainWindow(settings);

        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("显示桌宠", null, (sender, args) => ShowPet());
        trayMenu.Items.Add("退出", null, (sender, args) => Shutdown());

        var trayIcon = CreateTrayIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "桌宠",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _trayIcon.DoubleClick += (sender, args) => ShowPet();

        _mainWindow.Show();
    }

    private void ShowPet()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null) _trayIcon.Dispose();
        if (_trayImageIcon != null) _trayImageIcon.Dispose();
        if (_trayBitmap != null) _trayBitmap.Dispose();
        base.OnExit(e);
    }

    private Drawing.Icon CreateTrayIcon()
    {
        try
        {
            var pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "tray.png");
            if (File.Exists(pngPath))
            {
                _trayBitmap = new Drawing.Bitmap(pngPath);
                _trayImageIcon = Drawing.Icon.FromHandle(_trayBitmap.GetHicon());
                return _trayImageIcon;
            }
        }
        catch
        {
            // 图标加载失败时使用系统图标，不能影响桌宠启动。
        }
        return Drawing.SystemIcons.Application;
    }
}

internal sealed class PetSettings
{
    public double Opacity { get; set; }
    public double PlaybackSpeed { get; set; }
    public double Size { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }

    public PetSettings()
    {
        Opacity = 1.0;
        PlaybackSpeed = 1.0;
        Size = 1.0;
        Left = -1;
        Top = -1;
    }
}

internal static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static PetSettings Load()
    {
        var settings = new PetSettings();
        if (!File.Exists(FilePath)) return settings;

        try
        {
            var json = File.ReadAllText(FilePath);
            settings.Opacity = ReadNumber(json, "Opacity", settings.Opacity);
            settings.PlaybackSpeed = ReadNumber(json, "PlaybackSpeed", settings.PlaybackSpeed);
            settings.Size = ReadNumber(json, "Size", settings.Size);
            settings.Left = ReadNumber(json, "Left", settings.Left);
            settings.Top = ReadNumber(json, "Top", settings.Top);
            if (ReadNumber(json, "SizeCalibrationVersion", -1) < 2)
            {
                // 新标准是旧标准的一半，并将其重新定义为 100%。
                settings.Size = 1.0;
            }
        }
        catch
        {
            // 损坏的设置不应阻止桌宠启动，使用默认值即可。
        }

        settings.Opacity = Clamp(settings.Opacity, 0.2, 1.0);
        settings.PlaybackSpeed = Clamp(settings.PlaybackSpeed, 0.5, 2.0);
        settings.Size = Clamp(settings.Size, 0.01, 2.0);
        return settings;
    }

    public static void Save(PetSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var culture = CultureInfo.InvariantCulture;
            var json = new StringBuilder()
                .AppendLine("{")
                .AppendLine(string.Format(culture, "  \"Opacity\": {0},", settings.Opacity))
                .AppendLine(string.Format(culture, "  \"PlaybackSpeed\": {0},", settings.PlaybackSpeed))
                .AppendLine(string.Format(culture, "  \"Size\": {0},", settings.Size))
                .AppendLine("  \"SizeCalibrationVersion\": 2,")
                .AppendLine(string.Format(culture, "  \"Left\": {0},", settings.Left))
                .AppendLine(string.Format(culture, "  \"Top\": {0}", settings.Top))
                .AppendLine("}")
                .ToString();
            File.WriteAllText(FilePath, json, Encoding.UTF8);
        }
        catch
        {
            // 设置保存失败不应影响退出。
        }
    }

    private static double ReadNumber(string json, string name, double fallback)
    {
        var pattern = string.Format("\\\"{0}\\\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)", name);
        var match = Regex.Match(json, pattern);
        double value;
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out value)) return value;
        return fallback;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}

internal sealed class MainWindow : Window
{
    private readonly PetSettings _settings;
    private readonly PetVisual _petVisual;
    private readonly DispatcherTimer _animationTimer;
    private SettingsWindow _settingsWindow;
    private bool _dragging;
    private Point _dragOffset;
    private Point _dragStartScreenPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private const double StandardPetSize = 125.0;
    private const double StandardSliderValue = 1.0;
    private const double BasePetSize = StandardPetSize / StandardSliderValue;

    public MainWindow(PetSettings settings)
    {
        _settings = settings;
        Title = "桌宠";
        Width = 250;
        Height = 250;
        MinWidth = 1;
        MinHeight = 1;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        Cursor = Cursors.Hand;

        _petVisual = new PetVisual();
        Content = _petVisual;
        Opacity = _settings.Opacity;
        ApplySize(_settings.Size, false);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonUp += OnMouseRightButtonUp;
        Loaded += (sender, args) => RestoreOrPlace();
        Closed += (sender, args) => SaveSettings();

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _animationTimer.Tick += (sender, args) => _petVisual.Tick(_settings.PlaybackSpeed);
        _animationTimer.Start();
    }

    private void RestoreOrPlace()
    {
        var area = GetWorkArea();
        if (_settings.Left >= area.Left && _settings.Left <= area.Right - Width &&
            _settings.Top >= area.Top && _settings.Top <= area.Bottom - Height)
        {
            Left = _settings.Left;
            Top = _settings.Top;
        }
        else
        {
            Left = area.Right - Width - 28;
            Top = area.Bottom - Height - 28;
        }
        ClampToWorkArea();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        _dragging = true;
        _dragOffset = e.GetPosition(this);
        _dragStartScreenPoint = ScreenPixelsToDip(PointToScreen(_dragOffset));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        CaptureMouse();
        _petVisual.IsDragging = true;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var screenPoint = ScreenPixelsToDip(PointToScreen(e.GetPosition(this)));
        Left = _dragStartLeft + screenPoint.X - _dragStartScreenPoint.X;
        Top = _dragStartTop + screenPoint.Y - _dragStartScreenPoint.Y;
        ClampToWorkArea();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        _petVisual.IsDragging = false;
        SaveSettings();
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ShowSettings();
        e.Handled = true;
    }

    private void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(this, _settings);
        _settingsWindow.Closed += (sender, args) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    public void ApplyOpacity(double value)
    {
        _settings.Opacity = value;
        Opacity = value;
    }

    public void ApplySpeed(double value)
    {
        _settings.PlaybackSpeed = value;
    }

    public void ApplySize(double value, bool keepCenter)
    {
        var oldWidth = Width;
        var oldHeight = Height;
        _settings.Size = value;
        Width = BasePetSize * value;
        Height = BasePetSize * value;
        if (keepCenter)
        {
            Left += (oldWidth - Width) / 2;
            Top += (oldHeight - Height) / 2;
        }
        ClampToWorkArea();
    }

    private void SaveSettings()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        SettingsStore.Save(_settings);
    }

    private void ClampToWorkArea()
    {
        var area = GetWorkArea();
        Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
        Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
    }

    private Rect GetWorkArea()
    {
        try
        {
            var screenPoint = PointToScreen(new Point(Math.Max(1, Width / 2), Math.Max(1, Height / 2)));
            var screen = Forms.Screen.FromPoint(new Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                var transform = source.CompositionTarget.TransformFromDevice;
                var topLeft = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
                var bottomRight = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
                return new Rect(topLeft, bottomRight);
            }
        }
        catch
        {
            // 启动早期可能还没有可用的窗口句柄，回退到主屏工作区。
        }
        return SystemParameters.WorkArea;
    }

    private Point ScreenPixelsToDip(Point screenPoint)
    {
        var source = PresentationSource.FromVisual(this);
        if (source != null && source.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        }
        return screenPoint;
    }

    public Rect GetWorkAreaForPopup()
    {
        return GetWorkArea();
    }
}

internal sealed class PetVisual : FrameworkElement
{
    private double _time;
    private double _actionTime;
    private int _action;
    private readonly BitmapSource[] _idleFrames;
    private readonly BitmapSource[] _dragFrames;
    private bool _isDragging;

    public PetVisual()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        _idleFrames = LoadFrames(Path.Combine("assets", "idle"), "idle-");
        _dragFrames = LoadFrames(Path.Combine("assets", "drag"), "drag-");
    }

    public bool IsDragging
    {
        get { return _isDragging; }
        set
        {
            if (_isDragging == value) return;
            _isDragging = value;
            _action = 0;
            _actionTime = 0;
            InvalidateVisual();
        }
    }

    public void Tick(double speed)
    {
        var safeSpeed = Math.Max(0.5, Math.Min(2.0, speed));
        _time += 0.05 * safeSpeed;
        _actionTime += 0.05 * safeSpeed;
        var interval = IsDragging ? 0.55 : 1.2;
        if (_actionTime >= interval)
        {
            _actionTime = 0;
            var frames = IsDragging ? _dragFrames : _idleFrames;
            _action = frames.Length == 0 ? 0 : (_action + 1) % frames.Length;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var frames = IsDragging ? _dragFrames : _idleFrames;
        if (frames.Length > 0)
        {
            dc.DrawImage(frames[_action % frames.Length], new Rect(0, 0, ActualWidth, ActualHeight));
            return;
        }

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var bob = Math.Sin(_time * 2.1) * (IsDragging ? 3 : 5);
        var pulse = 1 + Math.Sin(_time * 3.5) * 0.025;
        var center = new Point(ActualWidth / 2, ActualHeight / 2 + bob);

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null,
            new Point(center.X, ActualHeight * 0.88), size * 0.3, size * 0.055);

        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new ScaleTransform(pulse, pulse));

        var bodyWidth = size * 0.52;
        var bodyHeight = size * 0.48;
        var bodyRect = new Rect(-bodyWidth / 2, -bodyHeight / 2 + size * 0.08, bodyWidth, bodyHeight);
        var bodyBrush = new LinearGradientBrush(
            Color.FromRgb(255, 190, 73), Color.FromRgb(237, 82, 52), 90);
        dc.DrawRoundedRectangle(bodyBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 230, 151)), size * 0.012),
            bodyRect, size * 0.1, size * 0.1);

        DrawEar(dc, -bodyWidth * 0.38, -bodyHeight * 0.34, false);
        DrawEar(dc, bodyWidth * 0.38, -bodyHeight * 0.34, true);

        var eyeY = -bodyHeight * 0.05;
        var eyeRadius = size * 0.035;
        var blink = !IsDragging && _action == 1 && Math.Sin(_actionTime * 13) > 0;
        if (blink)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(62, 28, 27)), size * 0.018);
            dc.DrawLine(pen, new Point(-bodyWidth * 0.2, eyeY), new Point(-bodyWidth * 0.08, eyeY));
            dc.DrawLine(pen, new Point(bodyWidth * 0.08, eyeY), new Point(bodyWidth * 0.2, eyeY));
        }
        else
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(62, 28, 27)), null,
                new Point(-bodyWidth * 0.14, eyeY), eyeRadius, eyeRadius * 1.3);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(62, 28, 27)), null,
                new Point(bodyWidth * 0.14, eyeY), eyeRadius, eyeRadius * 1.3);
        }

        var mouthPen = new Pen(new SolidColorBrush(Color.FromRgb(112, 36, 27)), size * 0.014);
        if (_action == 0)
            dc.DrawLine(mouthPen, new Point(-size * 0.025, size * 0.075), new Point(size * 0.025, size * 0.075));
        else
            dc.DrawEllipse(null, mouthPen, new Point(0, size * 0.075), size * 0.03, size * 0.024);

        var armPen = new Pen(new SolidColorBrush(Color.FromRgb(246, 117, 51)), size * 0.055)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        var armOffset = IsDragging && _action == 1 ? size * 0.08 : 0;
        dc.DrawLine(armPen, new Point(-bodyWidth * 0.48, size * 0.05),
            new Point(-bodyWidth * 0.7, size * 0.17 - armOffset));
        dc.DrawLine(armPen, new Point(bodyWidth * 0.48, size * 0.05),
            new Point(bodyWidth * 0.7, size * 0.17 + armOffset));

        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 239, 175)), null,
            new Point(0, bodyHeight * 0.43), size * 0.035, size * 0.018);

        dc.Pop();
        dc.Pop();
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        return new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    private static BitmapSource[] LoadFrames(string relativeFolder, string prefix)
    {
        var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeFolder);
        if (!Directory.Exists(folder)) return new BitmapSource[0];

        var files = Directory.GetFiles(folder, prefix + "*.png");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var frames = new BitmapSource[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = File.OpenRead(files[i]))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                frames[i] = bitmap;
            }
            catch
            {
                frames[i] = null;
            }
        }

        var valid = new System.Collections.Generic.List<BitmapSource>();
        foreach (var frame in frames)
        {
            if (frame != null) valid.Add(frame);
        }
        return valid.ToArray();
    }

    private static void DrawEar(DrawingContext dc, double x, double y, bool mirror)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var direction = mirror ? -1 : 1;
            context.BeginFigure(new Point(x - direction * 22, y + 19), true, true);
            context.LineTo(new Point(x + direction * 2, y - 22), true, false);
            context.LineTo(new Point(x + direction * 22, y + 19), true, false);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(238, 98, 50)),
            new Pen(new SolidColorBrush(Color.FromRgb(255, 218, 125)), 3), geometry);
    }
}

internal sealed class PetSlider : FrameworkElement
{
    private readonly double _minimum;
    private readonly double _maximum;
    private double _value;
    private bool _captured;

    public event Action<double> ValueChanged;

    public PetSlider(double minimum, double maximum, double initial)
    {
        _minimum = minimum;
        _maximum = maximum;
        _value = Clamp(initial, minimum, maximum);
        Focusable = false;
        SnapsToDevicePixels = true;
    }

    public double Value
    {
        get { return _value; }
        set
        {
            var next = Clamp(value, _minimum, _maximum);
            if (Math.Abs(next - _value) < 0.0001) return;
            _value = next;
            InvalidateVisual();
            if (ValueChanged != null) ValueChanged(_value);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var left = 8.0;
        var right = Math.Max(left + 1, ActualWidth - 8.0);
        var y = Math.Max(8, ActualHeight / 2.0);
        var ratio = (_value - _minimum) / (_maximum - _minimum);
        var knobX = left + (right - left) * ratio;
        var trackHeight = 4.0;
        var radius = trackHeight / 2.0;
        var white = new SolidColorBrush(Colors.White);
        var accent = new SolidColorBrush(Color.FromRgb(157, 57, 40));

        dc.DrawRoundedRectangle(white, null,
            new Rect(left, y - trackHeight / 2, right - left, trackHeight), radius, radius);
        dc.DrawRoundedRectangle(accent, null,
            new Rect(left, y - trackHeight / 2, Math.Max(trackHeight, knobX - left), trackHeight), radius, radius);
        dc.DrawEllipse(accent, null, new Point(knobX, y), 8, 8);
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        return new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _captured = true;
        CaptureMouse();
        SetValueFromPoint(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_captured || e.LeftButton != MouseButtonState.Pressed) return;
        SetValueFromPoint(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_captured) return;
        _captured = false;
        ReleaseMouseCapture();
        SetValueFromPoint(e.GetPosition(this).X);
        e.Handled = true;
    }

    private void SetValueFromPoint(double x)
    {
        var left = 8.0;
        var right = Math.Max(left + 1, ActualWidth - 8.0);
        var ratio = Clamp((x - left) / (right - left), 0, 1);
        Value = _minimum + (_maximum - _minimum) * ratio;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}

internal sealed class SettingsWindow : Window
{
    private static readonly Color PanelColor = Color.FromRgb(206, 203, 193);
    private static readonly Color TextColor = Color.FromRgb(52, 54, 53);
    private static readonly Color AccentColor = Color.FromRgb(157, 57, 40);
    private bool _closeScheduled;
    private bool _closing;

    public SettingsWindow(MainWindow owner, PetSettings settings)
    {
        Owner = owner;
        Title = "桌宠设置";
        Width = 248;
        Height = 232;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = true;

        var border = new Border
        {
            Background = new SolidColorBrush(PanelColor),
            BorderBrush = new SolidColorBrush(TextColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(15)
        };

        var stack = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var title = new TextBlock
        {
            Text = "桌宠设置",
            Foreground = new SolidColorBrush(TextColor),
            FontSize = 17,
            FontWeight = FontWeights.Bold
        };
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        var closePanel = new Button
        {
            Content = "×",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = new SolidColorBrush(TextColor),
            FontSize = 18,
            Cursor = Cursors.Hand
        };
        closePanel.Click += (sender, args) => Close();
        DockPanel.SetDock(closePanel, Dock.Right);
        header.Children.Add(closePanel);
        stack.Children.Add(header);

        stack.Children.Add(CreateSliderRow("透明度", "%", settings.Opacity, 0.2, 1.0, 0.01,
            value => owner.ApplyOpacity(value), value => value * 100, false));
        stack.Children.Add(CreateSliderRow("播放速度", "x", settings.PlaybackSpeed, 0.5, 2.0, 0.05,
            value => owner.ApplySpeed(value), value => value, true));
        stack.Children.Add(CreateSliderRow("大小", "%", settings.Size, 0.01, 2.0, 0.01,
            value => owner.ApplySize(value, true), value => value * 100, false));

        var exitButton = new Button
        {
            Content = "关闭桌宠",
            Height = 30,
            Margin = new Thickness(0, 8, 0, 0),
            Background = new SolidColorBrush(TextColor),
            Foreground = new SolidColorBrush(AccentColor),
            BorderBrush = new SolidColorBrush(TextColor),
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Cursor = Cursors.Hand
        };
        exitButton.Click += (sender, args) => System.Windows.Application.Current.Shutdown();
        stack.Children.Add(exitButton);

        border.Child = stack;
        Content = border;
        Loaded += (sender, args) => PositionBeside(owner);
        Closing += (sender, args) => _closing = true;
        Deactivated += (sender, args) => RequestCloseFromDeactivation();
    }

    private FrameworkElement CreateSliderRow(string label, string suffix, double initial,
        double min, double max, double tick, Action<double> update, Func<double, double> display,
        bool accentText)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        var line = new DockPanel();
        var labelText = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(accentText ? AccentColor : TextColor),
            FontSize = 12,
            FontWeight = accentText ? FontWeights.Bold : FontWeights.Normal
        };
        DockPanel.SetDock(labelText, Dock.Left);
        line.Children.Add(labelText);
        var valueText = new TextBlock
        {
            Foreground = new SolidColorBrush(accentText ? AccentColor : TextColor),
            FontSize = 12,
            FontWeight = accentText ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(valueText, Dock.Right);
        line.Children.Add(valueText);
        stack.Children.Add(line);

        var slider = new PetSlider(min, max, initial)
        {
            Margin = new Thickness(0, 0, 0, 0),
            Cursor = Cursors.Hand,
            Height = 18
        };
        Action<double> refresh = delegate(double value)
        {
            valueText.Text = suffix == "x"
                ? string.Format("{0:0.00}{1}", display(value), suffix)
                : string.Format("{0:0}{1}", display(value), suffix);
            update(value);
        };
        slider.ValueChanged += delegate(double value)
        {
            refresh(value);
        };
        refresh(initial);
        stack.Children.Add(slider);
        return stack;
    }

    private void PositionBeside(MainWindow owner)
    {
        var area = owner.GetWorkAreaForPopup();
        const double gap = 2;
        Left = owner.Left + owner.Width + gap;
        Top = owner.Top;

        if (Left + Width > area.Right)
        {
            Left = owner.Left - Width - gap;
        }
        Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
        Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
    }

    private void RequestCloseFromDeactivation()
    {
        if (_closeScheduled || _closing) return;
        _closeScheduled = true;
        Dispatcher.BeginInvoke(new Action(delegate
        {
            if (!_closing)
            {
                _closing = true;
                Close();
            }
        }), DispatcherPriority.Input);
    }

}
}
