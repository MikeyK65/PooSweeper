using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using PooSweep.Core.Audio;
using PooSweep.Services;

namespace PooSweep.Controls;

/// <summary>A chunky, squishy cartoon button with a 3D lip, optional icon and a click sound.</summary>
public sealed class GameButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(GameButton), string.Empty, propertyChanged: (b, _, n) => ((GameButton)b)._label.Text = (string)n);

    public static readonly BindableProperty ButtonColorProperty =
        BindableProperty.Create(nameof(ButtonColor), typeof(Color), typeof(GameButton), Color.FromArgb("#FF9F1C"), propertyChanged: (b, _, _) => ((GameButton)b).ApplyColors());

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(IconKind), typeof(GameButton), IconKind.None, propertyChanged: (b, _, _) => ((GameButton)b).ApplyIcon());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(GameButton), 22.0, propertyChanged: (b, _, n) => ((GameButton)b).ApplySize());

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(GameButton));

    public static readonly BindableProperty ClickSoundProperty =
        BindableProperty.Create(nameof(ClickSound), typeof(Sfx), typeof(GameButton), Sfx.UiClick);

    private readonly Border _shade;
    private readonly Border _face;
    private readonly Label _label;
    private readonly IconView _icon;
    private readonly HorizontalStackLayout _row;
    private bool _busy;

    public GameButton()
    {
        _label = new Label
        {
            FontFamily = "Display",
            TextColor = Colors.White,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center,
            Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.35f, Offset = new Point(0, 2), Radius = 0 },
        };
        _icon = new IconView { IsVisible = false, VerticalOptions = LayoutOptions.Center };
        _row = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { _icon, _label } };

        _shade = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 18 }, TranslationY = 5 };
        _face = new Border
        {
            StrokeThickness = 3,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(18, 10),
            Content = _row,
        };

        Content = new Grid { Children = { _shade, _face }, Padding = new Thickness(0, 0, 0, 5) };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OnTapped();
        GestureRecognizers.Add(tap);

        ApplyColors();
        ApplySize();
        SemanticProperties.SetHint(this, "Button");
    }

    public event EventHandler? Clicked;

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Color ButtonColor { get => (Color)GetValue(ButtonColorProperty); set => SetValue(ButtonColorProperty, value); }
    public IconKind Icon { get => (IconKind)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public Sfx ClickSound { get => (Sfx)GetValue(ClickSoundProperty); set => SetValue(ClickSoundProperty, value); }

    private void ApplyColors()
    {
        var c = ButtonColor;
        var dark = Color.FromRgba(c.Red * 0.62, c.Green * 0.62, c.Blue * 0.62, 1);
        _face.Background = new LinearGradientBrush(
            [new GradientStop(c.WithLuminosity(Math.Min(1, c.GetLuminosity() + 0.08f)), 0), new GradientStop(c, 1)],
            new Point(0, 0), new Point(0, 1));
        _face.Stroke = dark;
        _shade.BackgroundColor = dark;
    }

    private void ApplyIcon()
    {
        _icon.IsVisible = Icon != IconKind.None;
        _icon.Icon = Icon;
    }

    private void ApplySize()
    {
        _label.FontSize = FontSize;
        _icon.WidthRequest = _icon.HeightRequest = FontSize * 1.3;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Text)) SemanticProperties.SetDescription(this, Text);
    }

    private async Task OnTapped()
    {
        if (_busy || !IsEnabled) return;
        _busy = true;
        try
        {
            ServiceHelper.Get<AudioService>()?.Play(ClickSound);
            ServiceHelper.Get<Haptics>()?.Tap();
            await _face.TranslateToAsync(0, 4, 50, Easing.CubicOut);
            await _face.TranslateToAsync(0, 0, 90, Easing.SpringOut);
            Clicked?.Invoke(this, EventArgs.Empty);
            if (Command?.CanExecute(null) == true) Command.Execute(null);
        }
        finally
        {
            _busy = false;
        }
    }
}
