using Content.Server._Warlock.Access.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Warlock.Access;

/// <summary>
/// _Warlock
/// Одноразовые ключ-карты Братства Стали.
///
/// У Техномагического Союза есть КПК и нормальные пропуска — они умеют делать электронику.
/// У Братства электроники нет, есть трофейные карты, которые режут на одноразовые ключи.
/// Такой карты нет в списке считывателя: её физически суют в дверь, дверь открывается,
/// карта остаётся внутри. На следующую дверь нужна следующая карта.
/// </summary>
public sealed partial class WarlockAccessKeySystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundPathSpecifier SuccessSound = new("/Audio/Machines/airlock_open.ogg");
    private static readonly SoundPathSpecifier FailSound = new("/Audio/Machines/airlock_deny.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockAccessKeyComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<WarlockAccessKeyComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // Карта работает только по дверям: в стену её совать бессмысленно.
        if (!TryComp<DoorComponent>(target, out var door))
            return;

        args.Handled = true;

        // У двери может не быть считывателя вообще — тогда она и так открывается руками.
        if (!TryComp<AccessReaderComponent>(target, out var reader))
        {
            _popup.PopupEntity(Loc.GetString("warlock-access-key-not-needed"), target, args.User);
            return;
        }

        if (!_accessReader.AreAccessTagsAllowed(ent.Comp.Tags, reader))
        {
            _audio.PlayPvs(FailSound, target);
            _popup.PopupEntity(Loc.GetString("warlock-access-key-wrong"), target, args.User, PopupType.MediumCaution);
            return;
        }

        if (!_door.TryOpen(target, door, args.User))
        {
            _popup.PopupEntity(Loc.GetString("warlock-access-key-stuck"), target, args.User, PopupType.MediumCaution);
            return;
        }

        _audio.PlayPvs(SuccessSound, target);
        _popup.PopupEntity(Loc.GetString("warlock-access-key-used"), target, args.User);

        // Карта остаётся в приёмнике двери и больше ни на что не годится.
        if (ent.Comp.Consume)
            QueueDel(ent.Owner);
    }
}
