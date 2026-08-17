using Content.Shared._Warlock.Exosuits;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._Warlock.Exosuits;

/// <summary>
/// _Warlock — связка окна ОС экзоскелета с сервером.
///
/// Клиент не решает ничего: он показывает присланное состояние и отправляет обратно
/// три настройки. Все проверки заново делаются на сервере, потому что сообщение
/// от клиента можно подделать.
/// </summary>
[UsedImplicitly]
public sealed class WarlockExoOsBoundUserInterface : BoundUserInterface
{
    private WarlockExoOsWindow? _window;

    public WarlockExoOsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new WarlockExoOsWindow();
        _window.OnClose += Close;
        _window.OnApply += (share, limiter, cooling) =>
            SendMessage(new WarlockExoOsSetMessage(share, limiter, cooling));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is WarlockExoOsState os)
            _window?.Update(os);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
