using System.Linq;
using System.Numerics;
using Content.Shared._Warlock.Grimoire;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Warlock.Grimoire;

/// <summary>
/// _Warlock
/// Окно гримуара: сколько очков осталось и что за них можно взять.
///
/// Собрано кодом, а не XAML, намеренно: список полностью динамический — строки приходят
/// с сервера и у каждого читателя свои, потому что доступ к разделам зависит от должности.
/// Размечать в XAML тут нечего, кроме рамки.
/// </summary>
public sealed class WarlockGrimoireWindow : DefaultWindow
{
    /// <summary>
    /// Игрок выбрал строку. Наверх уходит идентификатор записи каталога.
    /// </summary>
    public event Action<string>? OnLearn;

    private readonly Label _points;
    private readonly BoxContainer _list;

    public WarlockGrimoireWindow()
    {
        // MinSize и SetSize — Vector2 из System.Numerics, а не Vector2i.
        MinSize = new Vector2(460, 420);
        SetSize = new Vector2(460, 520);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
        };

        _points = new Label
        {
            Margin = new Thickness(4, 4, 4, 8),
        };

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
        };

        root.AddChild(_points);
        root.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _list },
        });

        Contents.AddChild(root);
    }

    public void Update(WarlockGrimoireState state)
    {
        _points.Text = Loc.GetString("warlock-grimoire-ui-points", ("points", state.Points));

        _list.RemoveAllChildren();

        // Строки приходят уже отсортированными по разделу; здесь только расставляем заголовки.
        WarlockSpellSection? current = null;

        foreach (var entry in state.Entries)
        {
            if (current != entry.Section)
            {
                current = entry.Section;

                _list.AddChild(new Label
                {
                    Text = Loc.GetString(SectionLoc(entry.Section)),
                    StyleClasses = { "LabelHeading" },
                    Margin = new Thickness(4, 10, 4, 2),
                });
            }

            _list.AddChild(BuildRow(entry, state.Points));
        }

        if (!state.Entries.Any())
        {
            _list.AddChild(new Label
            {
                Text = Loc.GetString("warlock-grimoire-ui-empty"),
                Margin = new Thickness(4),
            });
        }
    }

    private Control BuildRow(WarlockGrimoireEntry entry, int points)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 2, 4, 2),
            HorizontalExpand = true,
        };

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        text.AddChild(new Label { Text = entry.Name });
        text.AddChild(new Label
        {
            Text = entry.Description,
            FontColorOverride = Color.DarkGray,
        });

        // Причина отказа важнее самой кнопки: игрок должен понимать, почему нельзя.
        string label;
        var enabled = false;

        if (entry.Taken)
        {
            label = Loc.GetString("warlock-grimoire-ui-taken");
        }
        else if (!entry.Allowed)
        {
            label = Loc.GetString("warlock-grimoire-ui-forbidden");
        }
        else if (entry.Cost > points)
        {
            label = Loc.GetString("warlock-grimoire-ui-cost", ("cost", entry.Cost));
        }
        else
        {
            label = Loc.GetString("warlock-grimoire-ui-cost", ("cost", entry.Cost));
            enabled = true;
        }

        var button = new Button
        {
            Text = label,
            Disabled = !enabled,
            MinWidth = 110,
            VerticalAlignment = VAlignment.Center,
        };

        var id = entry.Id;
        button.OnPressed += _ => OnLearn?.Invoke(id);

        row.AddChild(text);
        row.AddChild(button);

        return row;
    }

    private static string SectionLoc(WarlockSpellSection section)
    {
        return section switch
        {
            WarlockSpellSection.Common => "warlock-grimoire-section-common",
            WarlockSpellSection.Combat => "warlock-grimoire-section-combat",
            WarlockSpellSection.Chaplain => "warlock-grimoire-section-chaplain",
            _ => "warlock-grimoire-section-command",
        };
    }
}
