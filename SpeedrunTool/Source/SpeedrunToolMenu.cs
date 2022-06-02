using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Celeste.Mod.SpeedrunTool.DeathStatistics;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.Other;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using FMOD.Studio;

namespace Celeste.Mod.SpeedrunTool;

public static class SpeedrunToolMenu {
    private static readonly Regex RegexFormatName = new(@"([a-z])([A-Z])", RegexOptions.Compiled);
    private static List<EaseInSubMenu> options;

    private static readonly KeyValuePair<FreezeAfterLoadStateType, string>[] FreezeAfterLoadStateTypes = new Dictionary<FreezeAfterLoadStateType, string> {
        {FreezeAfterLoadStateType.Off, Dialog.Clean(DialogIds.Off)},
        {FreezeAfterLoadStateType.On, Dialog.Clean(DialogIds.On)},
        {FreezeAfterLoadStateType.IgnoreHoldingKeys, Dialog.Clean(DialogIds.IgnoreHoldingKeys)},
    }.ToArray();
    
    private static readonly KeyValuePair<TeleportRoomCategory, string>[] TeleportRoomCategories = new Dictionary<TeleportRoomCategory, string> {
        {TeleportRoomCategory.Default, Dialog.Clean(DialogIds.Default)},
        {TeleportRoomCategory.Any, Dialog.Clean(DialogIds.Any)},
    }.ToArray();

    public static void Create(TextMenu menu, bool inGame, EventInstance snapshot) {
        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.Enabled), ModSettings.Enabled).Change((value) => {
            ModSettings.Enabled = value;
            foreach (EaseInSubMenu item in options) {
                item.FadeVisible = value;
            }
        }));
        CreateOptions(menu, inGame);
        foreach (EaseInSubMenu item in options) {
            menu.Add(item);
        }
    }

    private static IEnumerable<KeyValuePair<TEnum, string>> CreateEnumerableOptions<TEnum>()
        where TEnum : struct, IComparable, IFormattable, IConvertible {
        List<KeyValuePair<TEnum, string>> results = new();
        foreach (TEnum value in Enum.GetValues(typeof(TEnum))) {
            results.Add(new KeyValuePair<TEnum, string>(value, value.DialogClean()));
        }

        return results;
    }

    public static string DialogClean<TEnum>(this TEnum @enum) where TEnum : struct, IComparable, IFormattable, IConvertible {
        return Dialog.Clean(DialogIds.Prefix + RegexFormatName.Replace(@enum.ToString(), "$1_$2").ToUpper());
    }

    private static void AddDescription(TextMenu.Item subMenuItem, TextMenuExt.SubMenu subMenu, TextMenu containingMenu, string description) {
        TextMenuExt.EaseInSubHeaderExt descriptionText = new(description, false, containingMenu) {
            TextColor = Color.Gray,
            HeightExtra = 0f
        };

        subMenu.Add(descriptionText);

        subMenuItem.OnEnter += () => descriptionText.FadeVisible = true;
        subMenuItem.OnLeave += () => descriptionText.FadeVisible = false;
    }

    private static void CreateOptions(TextMenu menu, bool inGame) {
        options = new List<EaseInSubMenu> {
            new EaseInSubMenu(Dialog.Clean(DialogIds.RoomTimer), false).With(subMenu => {
                subMenu.Add(new TextMenuExt.EnumerableSlider<RoomTimerType>(Dialog.Clean(DialogIds.Enabled),
                    CreateEnumerableOptions<RoomTimerType>(), ModSettings.RoomTimerType).Change(RoomTimerManager.SwitchRoomTimer));

                subMenu.Add(new TextMenuExt.IntSlider(Dialog.Clean(DialogIds.NumberOfRooms), 1, 99, ModSettings.NumberOfRooms).Change(i =>
                    ModSettings.NumberOfRooms = i));

                subMenu.Add(new TextMenuExt.EnumerableSlider<EndPoint.SpriteStyle>(Dialog.Clean(DialogIds.EndPointStyle),
                    CreateEnumerableOptions<EndPoint.SpriteStyle>(), ModSettings.EndPointStyle).Change(value => {
                    ModSettings.EndPointStyle = value;
                    EndPoint.AllResetSprite();
                }));

                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.TimeSummitFlag), ModSettings.TimeSummitFlag).Change(b =>
                    ModSettings.TimeSummitFlag = b));

                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.AutoTurnOffRoomTimer), ModSettings.AutoResetRoomTimer).Change(b =>
                    ModSettings.AutoResetRoomTimer = b));

                if (!string.IsNullOrEmpty(EndPoint.RoomIdEndPoint)) {
                    TextMenu.Button roomIdEndPoint = new (string.Format(Dialog.Get(DialogIds.RoomIdEndPoint), EndPoint.RoomIdEndPoint)) {
                        Selectable = false
                    };
                    subMenu.Add(roomIdEndPoint);
                }
            }),

            new EaseInSubMenu(Dialog.Clean(DialogIds.State), false).With(subMenu => {
                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.AutoLoadStateAfterDeath), ModSettings.AutoLoadStateAfterDeath).Change(b =>
                    ModSettings.AutoLoadStateAfterDeath = b));

                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.AutoClearStateOnScreenTransition), ModSettings.AutoClearStateOnScreenTransition).Change(b =>
                    ModSettings.AutoClearStateOnScreenTransition = b));

                subMenu.Add(new TextMenuExt.EnumerableSlider<FreezeAfterLoadStateType>(Dialog.Clean(DialogIds.FreezeAfterLoadState), FreezeAfterLoadStateTypes,
                    ModSettings.FreezeAfterLoadStateType).Change(b =>
                    ModSettings.FreezeAfterLoadStateType = b));

                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.SaveTimeAndDeaths), ModSettings.SaveTimeAndDeaths).Change(b =>
                    ModSettings.SaveTimeAndDeaths = b));

                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.SaveExtendedVariants), ModSettings.SaveExtendedVariants).Change(b =>
                    ModSettings.SaveExtendedVariants = b));
            }),

            new EaseInSubMenu(Dialog.Clean(DialogIds.DeathStatistics), false).With(subMenu => {
                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.Enabled), ModSettings.DeathStatistics).Change(b =>
                    ModSettings.DeathStatistics = b));

                subMenu.Add(new TextMenu.Slider(
                    DialogIds.MaxNumberOfDeathData.DialogClean(),
                    value => (value * 10).ToString(),
                    1,
                    9,
                    ModSettings.MaxNumberOfDeathData
                ).Change(i => ModSettings.MaxNumberOfDeathData = i));

                subMenu.Add(new TextMenu.Button(Dialog.Clean(DialogIds.CheckDeathStatistics)).Pressed(() => {
                    subMenu.Focused = false;
                    DeathStatisticsUi buttonConfigUi = new() {OnClose = () => subMenu.Focused = true};
                    Engine.Scene.Add(buttonConfigUi);
                    Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
                }));
            }),

            new EaseInSubMenu(Dialog.Clean(DialogIds.MoreOptions), false).With(subMenu => {
                subMenu.Add(new TextMenuExt.EnumerableSlider<TeleportRoomCategory>(Dialog.Clean(DialogIds.TeleportRoomCategory), TeleportRoomCategories,
                    ModSettings.TeleportRoomCategory).Change(b => ModSettings.TeleportRoomCategory = b));
                
                subMenu.Add(new TextMenuExt.IntSlider(Dialog.Clean(DialogIds.RespawnSpeed), 1, 9, ModSettings.RespawnSpeed).Change(i =>
                    ModSettings.RespawnSpeed = i));

                subMenu.Add(new TextMenuExt.IntSlider(Dialog.Clean(DialogIds.RestartChapterSpeed), 1, 9, ModSettings.RestartChapterSpeed).Change(i =>
                    ModSettings.RestartChapterSpeed = i));

                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.SkipRestartChapterScreenWipe), ModSettings.SkipRestartChapterScreenWipe).Change(b =>
                    ModSettings.SkipRestartChapterScreenWipe = b));
                
                subMenu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.AllowPauseDuringDeath), ModSettings.AllowPauseDuringDeath).Change(b =>
                    ModSettings.AllowPauseDuringDeath = b));

                subMenu.Add(
                    new TextMenu.OnOff(Dialog.Clean(DialogIds.MuteInBackground), ModSettings.MuteInBackground).Change(b =>
                        ModSettings.MuteInBackground = b));

                subMenu.Add(new TextMenuExt.EnumerableSlider<PopupMessageStyle>(Dialog.Clean(DialogIds.PopupMessageStyle),
                    CreateEnumerableOptions<PopupMessageStyle>(), ModSettings.PopupMessageStyle).Change(value => {
                    ModSettings.PopupMessageStyle = value;
                }));

                subMenu.Add(
                    new TextMenu.OnOff(Dialog.Clean(DialogIds.Hotkeys), ModSettings.Hotkeys).Change(b =>
                        ModSettings.Hotkeys = b));

                subMenu.Add(new TextMenu.Button(Dialog.Clean(DialogIds.HotkeysConfig)).Pressed(() => {
                    // 修复：在 overworld 界面 hot reload 之后打开按键设置菜单游戏崩溃
                    if (Engine.Scene.Tracker.Entities is { } entities) {
                        Type type = typeof(HotkeyConfigUi);
                        if (!entities.ContainsKey(type)) {
                            entities[type] = new List<Entity>();
                        }
                    }

                    subMenu.Focused = false;
                    HotkeyConfigUi hotkeyConfigUi = new() {OnClose = () => subMenu.Focused = true};
                    Engine.Scene.Add(hotkeyConfigUi);
                    Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
                }));
            }),
        };
    }
}

internal class EaseInSubMenu : TextMenuExt.SubMenu {
    public bool FadeVisible { get; set; }
    private float alpha;
    private float unEasedAlpha;
    private readonly MTexture icon;

    public EaseInSubMenu(string label, bool enterOnSelect) : base(label, enterOnSelect) {
        alpha = unEasedAlpha = ModSettings.Enabled ? 1f : 0f;
        FadeVisible = Visible = ModSettings.Enabled;
        icon = GFX.Gui["downarrow"];
    }

    public override float Height() => MathHelper.Lerp(-Container.ItemSpacing, base.Height(), alpha);

    public override void Update() {
        base.Update();

        float targetAlpha = FadeVisible ? 1 : 0;
        if (Math.Abs(unEasedAlpha - targetAlpha) > 0.001f) {
            unEasedAlpha = Calc.Approach(unEasedAlpha, targetAlpha, Engine.RawDeltaTime * 3f);
            alpha = FadeVisible ? Ease.SineOut(unEasedAlpha) : Ease.SineIn(unEasedAlpha);
        }

        Visible = alpha != 0;
    }

    public override void Render(Vector2 position, bool highlighted) {
        Vector2 top = new(position.X, position.Y - (Height() / 2));

        float currentAlpha = Container.Alpha * alpha;
        Color color = Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : Color.White) * currentAlpha);
        Color strokeColor = Color.Black * (currentAlpha * currentAlpha * currentAlpha);

        bool unCentered = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;

        Vector2 titlePosition = top + (Vector2.UnitY * TitleHeight / 2) + (unCentered ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
        Vector2 justify = unCentered ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
        Vector2 iconJustify = unCentered
            ? new Vector2(ActiveFont.Measure(Label).X + icon.Width, 5f)
            : new Vector2(ActiveFont.Measure(Label).X / 2 + icon.Width, 5f);
        DrawIcon(titlePosition, iconJustify, true, Items.Count < 1 ? Color.DarkSlateGray : color, alpha);
        ActiveFont.DrawOutline(Label, titlePosition, justify, Vector2.One, color, 2f, strokeColor);

        if (Focused && this.GetFieldValue<float>("ease") > 0.9f) {
            Vector2 menuPosition = new(top.X + ItemIndent, top.Y + TitleHeight + ItemSpacing);
            RecalculateSize();
            foreach (TextMenu.Item item in Items) {
                if (item.Visible) {
                    float height = item.Height();
                    Vector2 itemPosition = menuPosition + new Vector2(0f, height * 0.5f + item.SelectWiggler.Value * 8f);
                    if (itemPosition.Y + height * 0.5f > 0f && itemPosition.Y - height * 0.5f < Engine.Height) {
                        item.Render(itemPosition, Focused && Current == item);
                    }

                    menuPosition.Y += height + ItemSpacing;
                }
            }
        }
    }

    private void DrawIcon(Vector2 position, Vector2 justify, bool outline, Color color, float scale) {
        if (outline) {
            icon.DrawOutlineCentered(position + justify, color, scale);
        } else {
            icon.DrawCentered(position + justify, color, scale);
        }
    }
}