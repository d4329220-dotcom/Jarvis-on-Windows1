using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Jarvis.Core;
using Jarvis.Data;
using Jarvis.UI;

namespace Jarvis;

public partial class MainWindow : Window
{
    private readonly AssistantEngine _engine;
    private readonly HistoryRepository _history;
    private readonly AppSettings _settings;
    private readonly SecretStore _secrets;
    private readonly ObservableCollection<string> _items = new();
    private readonly SolidColorBrush _brush = new(Color.FromRgb(0, 180, 255));
    private readonly GradientStop _glow = new(Color.FromArgb(120, 0, 180, 255), 0);
    private readonly GradientStop _coreEdge = new(Color.FromRgb(0, 180, 255), 1);

    public bool AllowClose { get; set; }

    private static readonly Dictionary<AssistantState, (Color Color, string Ru, string En, double Spin, double Pulse)> Look = new()
    {
        [AssistantState.Idle] = (Color.FromRgb(0, 180, 255), "ОЖИДАНИЕ", "IDLE", 24, 1.8),
        [AssistantState.Listening] = (Color.FromRgb(0, 255, 156), "СЛУШАЮ...", "LISTENING", 8, 0.7),
        [AssistantState.Processing] = (Color.FromRgb(255, 184, 0), "ОБРАБОТКА...", "PROCESSING", 2.5, 0.35),
        [AssistantState.Speaking] = (Color.FromRgb(176, 107, 255), "ГОВОРЮ...", "SPEAKING", 5, 0.5),
        [AssistantState.Error] = (Color.FromRgb(255, 59, 59), "ОШИБКА", "ERROR", 1.2, 0.25),
    };

    public MainWindow(AssistantEngine engine, HistoryRepository history, AppSettings settings, SecretStore secrets)
    {
        InitializeComponent();
        _engine = engine; _history = history; _settings = settings; _secrets = secrets;
        HistoryList.ItemsSource = _items;

        foreach (var ring in new[] { Ring1, Ring2, Ring3 }) ring.Stroke = _brush;
        TitleText.Foreground = _brush; OnlineText.Foreground = _brush; StateText.Foreground = _brush;
        Glow.Fill = new RadialGradientBrush(new GradientStopCollection { _glow, new GradientStop(Color.FromArgb(0, 0, 0, 0), 1) });
        CoreDot.Fill = new RadialGradientBrush(new GradientStopCollection { new GradientStop(Colors.White, 0), _coreEdge });
        Apply(AssistantState.Idle, false);

        engine.StateChanged += s => Dispatcher.InvokeAsync(() => Apply(s, true));
        engine.CommandHeard += c => Dispatcher.InvokeAsync(() => LastCommand.Text = $"«{c}»");
        engine.Responded += r => Dispatcher.InvokeAsync(() => LastReply.Text = r);
        engine.HistoryChanged += () => Dispatcher.InvokeAsync(RefreshHistory);
        engine.VoiceChanged += v => Dispatcher.InvokeAsync(() => UpdateVoice(v));
        RefreshHistory();
        UpdateVoice(engine.VoiceEnabled);
    }

    private void Apply(AssistantState s, bool animate)
    {
        var l = Look[s];
        var d = TimeSpan.FromMilliseconds(animate ? 450 : 0);
        _brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(l.Color, d));
        _glow.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(Color.FromArgb(120, l.Color.R, l.Color.G, l.Color.B), d));
        _coreEdge.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(l.Color, d));
        StateText.Text = l.Ru; StateEn.Text = l.En;
        Spin(Ring1Rot, l.Spin, false); Spin(Ring2Rot, l.Spin * 0.7, true); Spin(Ring3Rot, l.Spin * 1.3, false);
        var pulse = new DoubleAnimation(0.85, 1.25, TimeSpan.FromSeconds(l.Pulse))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
        CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        if (_engine.VoiceEnabled) MicDot.Fill = s == AssistantState.Listening ? new SolidColorBrush(Color.FromRgb(0, 255, 156)) : _brush;
    }

    private static void Spin(RotateTransform rt, double seconds, bool reverse) =>
        rt.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(reverse ? 360 : 0, reverse ? 0 : 360, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever });

    private void UpdateVoice(bool on)
    {
        VoiceButton.Content = on ? "🎙 Микрофон: ВКЛ" : "🎙 Микрофон: ВЫКЛ";
        MicText.Text = on ? "Микрофон активен — скажите «Джарвис»" : "Микрофон выключен";
        MicDot.Fill = on ? _brush : new SolidColorBrush(Color.FromRgb(68, 86, 106));
    }

    private void RefreshHistory()
    {
        _items.Clear();
        try
        {
            foreach (var h in _history.Recent(50)) _items.Add($"{h.Time:dd.MM HH:mm} — «{h.Command}» → {h.Response}");
        }
        catch (Exception ex) { Log.Error(ex, "history load"); }
    }

    public void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }

    public void OpenSettings()
    {
        ShowFromTray();
        new SettingsWindow(_settings, _secrets, _engine) { Owner = this }.ShowDialog();
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.TextBox) DragMove();
    }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseClick(object sender, RoutedEventArgs e) => Hide();            // в трей
    private void VoiceClick(object sender, RoutedEventArgs e) { if (_engine.VoiceEnabled) _engine.StopVoice(); else _engine.StartVoice(); }
    private void SpeakClick(object sender, RoutedEventArgs e) => _engine.ListenOnce();
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void ClearClick(object sender, RoutedEventArgs e) { _history.Clear(); RefreshHistory(); }

    private void CommandKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var text = CommandBox.Text.Trim();
        CommandBox.Clear();
        if (text.Length > 0) _ = Task.Run(() => _engine.HandleTextAsync(text));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
}
