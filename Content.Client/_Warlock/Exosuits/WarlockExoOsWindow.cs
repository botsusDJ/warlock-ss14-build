using System.Numerics;
using Content.Shared._Warlock.Exosuits;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Warlock.Exosuits;

/// <summary>
/// _Warlock
/// Окно ОС экзоскелета.
///
/// Собрано кодом, а не XAML, по той же причине, что и окно гримуара: размечать тут
/// нечего, кроме рамки, а состояние целиком приходит с сервера.
///
/// Настройки применяются кнопкой, а не сразу при движении ползунка. Ползунок шлёт
/// событие на каждый пиксель, и без кнопки рама пересчитывала бы прибавки носителю
/// десятки раз за одно перетаскивание.
/// </summary>
public sealed class WarlockExoOsWindow : DefaultWindow
{
    /// <summary>
    /// Игрок нажал «применить»: доля мощности в кулаки, ограничитель, охлаждение.
    /// </summary>
    public event Action<float, bool, WarlockExoCooling>? OnApply;

    /// <summary>
    /// Пуск или останов приводов. Отдельно от настроек: настройки применяются
    /// кнопкой целиком, а приводы должны отзываться сразу.
    /// </summary>
    public event Action? OnToggle;

    private readonly Label _frame;
    private readonly Label _state;
    private readonly ProgressBar _heat;
    private readonly Label _heatLabel;
    private readonly ProgressBar _charge;
    private readonly Label _chargeLabel;

    private readonly Slider _share;
    private readonly Label _shareLabel;
    private readonly CheckBox _limiter;
    private readonly Button _cooling;
    private readonly Button _power;

    private WarlockExoCooling _coolingMode = WarlockExoCooling.Passive;

    public WarlockExoOsWindow()
    {
        Title = Loc.GetString("warlock-exo-os-title");

        // MinSize и SetSize — Vector2 из System.Numerics, а не Vector2i.
        MinSize = new Vector2(400, 340);
        SetSize = new Vector2(400, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            Margin = new Thickness(6),
        };

        _frame = new Label();
        _state = new Label();
        root.AddChild(_frame);
        root.AddChild(_state);

        // Пуск приводов — первым делом и крупно. Это то, ради чего ОС открывают
        // в бою, и искать её среди настроек распределения нельзя.
        _power = new Button { Margin = new Thickness(0, 6, 0, 8) };
        _power.OnPressed += _ => OnToggle?.Invoke();
        root.AddChild(_power);

        // --- жар
        _heatLabel = new Label();
        _heat = new ProgressBar { MinValue = 0f, MaxValue = 1f, MinHeight = 14 };
        root.AddChild(_heatLabel);
        root.AddChild(_heat);

        // --- заряд
        _chargeLabel = new Label { Margin = new Thickness(0, 6, 0, 0) };
        _charge = new ProgressBar { MinValue = 0f, MaxValue = 1f, MinHeight = 14 };
        root.AddChild(_chargeLabel);
        root.AddChild(_charge);

        // --- распределение мощности
        _shareLabel = new Label { Margin = new Thickness(0, 10, 0, 0) };
        _share = new Slider { MinValue = 0f, MaxValue = 1f, Value = 0.5f, HorizontalExpand = true };
        _share.OnValueChanged += _ => UpdateShareLabel();
        root.AddChild(_shareLabel);
        root.AddChild(_share);

        // --- охлаждение и ограничитель
        _cooling = new Button { Margin = new Thickness(0, 10, 0, 0) };
        _cooling.OnPressed += _ =>
        {
            _coolingMode = _coolingMode == WarlockExoCooling.Passive
                ? WarlockExoCooling.Active
                : WarlockExoCooling.Passive;
            UpdateCoolingLabel();
        };
        root.AddChild(_cooling);

        _limiter = new CheckBox
        {
            Text = Loc.GetString("warlock-exo-os-limiter"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        root.AddChild(_limiter);

        var apply = new Button
        {
            Text = Loc.GetString("warlock-exo-os-apply"),
            Margin = new Thickness(0, 12, 0, 0),
        };
        apply.OnPressed += _ => OnApply?.Invoke(_share.Value, _limiter.Pressed, _coolingMode);
        root.AddChild(apply);

        Contents.AddChild(root);

        UpdateShareLabel();
        UpdateCoolingLabel();
    }

    private void UpdateShareLabel()
    {
        var fist = (int) MathF.Round(_share.Value * 100f);
        _shareLabel.Text = Loc.GetString("warlock-exo-os-share",
            ("fist", fist), ("tool", 100 - fist));
    }

    private void UpdateCoolingLabel()
    {
        _cooling.Text = Loc.GetString(_coolingMode == WarlockExoCooling.Active
            ? "warlock-exo-os-cooling-active"
            : "warlock-exo-os-cooling-passive");
    }

    public void Update(WarlockExoOsState state)
    {
        _frame.Text = Loc.GetString("warlock-exo-os-frame",
            ("frame", Loc.GetString($"warlock-exo-frame-{state.Frame.ToString().ToLowerInvariant()}")));

        _state.Text = Loc.GetString(state.Active
            ? "warlock-exo-os-state-live"
            : "warlock-exo-os-state-dead");

        _power.Text = Loc.GetString(state.Active
            ? "warlock-exo-os-stop"
            : "warlock-exo-os-start");
        // Без ячейки запускать нечего, и кнопка не должна врать, что можно.
        _power.Disabled = state.MaxCharge <= 0f;

        var heat = state.MaxHeat > 0f ? state.Heat / state.MaxHeat : 0f;
        _heat.Value = heat;
        _heatLabel.Text = Loc.GetString("warlock-exo-os-heat", ("pct", (int) MathF.Round(heat * 100f)));

        var charge = state.MaxCharge > 0f ? state.Charge / state.MaxCharge : 0f;
        _charge.Value = charge;
        _chargeLabel.Text = state.MaxCharge > 0f
            ? Loc.GetString("warlock-exo-os-charge", ("pct", (int) MathF.Round(charge * 100f)))
            : Loc.GetString("warlock-exo-os-nocell");

        // Ползунок трогаем только пока его не тащат: иначе значение прыгало бы
        // под пальцем каждый раз, когда сервер присылает свежее состояние.
        if (!_share.Grabbed)
            _share.Value = state.FistShare;

        _limiter.Pressed = state.Limiter;
        _coolingMode = state.Cooling;

        UpdateShareLabel();
        UpdateCoolingLabel();
    }
}
