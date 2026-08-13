using Content.Shared._Warlock.Grimoire;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._Warlock.Grimoire;

/// <summary>
/// _Warlock — связка окна гримуара с сервером.
///
/// Клиент не решает ничего: он только показывает присланные строки и отправляет обратно
/// идентификатор выбранной. Все проверки — доступ, цена, повтор — заново делаются на сервере,
/// потому что сообщение от клиента можно подделать.
/// </summary>
[UsedImplicitly]
public sealed class WarlockGrimoireBoundUserInterface : BoundUserInterface
{
    private WarlockGrimoireWindow? _window;

    public WarlockGrimoireBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new WarlockGrimoireWindow();
        _window.OnClose += Close;
        _window.OnLearn += entry => SendMessage(new WarlockGrimoireLearnMessage(entry));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is WarlockGrimoireState grimoire)
            _window?.Update(grimoire);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
