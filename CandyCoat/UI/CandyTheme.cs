using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Registers all Candy Coat named colors with Una.Drawing's theme system.
/// Call CandyTheme.Apply() once after DrawingLib.Setup().
/// Palette: Backstage dark charcoal + steel blue + ice blue.
/// </summary>
internal static class CandyTheme
{
    // Backgrounds
    public const string BgWindow      = "CandyBgWindow";
    public const string BgSidebar     = "CandyBgSidebar";
    public const string BgCard        = "CandyBgCard";
    public const string BgCardHover   = "CandyBgCardHover";
    public const string BgInput       = "CandyBgInput";
    public const string BgTabActive   = "CandyBgTabActive";
    public const string BgTabInactive = "CandyBgTabInactive";

    // Borders
    public const string BorderCard    = "CandyBorderCard";
    public const string BorderDivider = "CandyBorderDivider";
    public const string BorderFocus   = "CandyBorderFocus";

    // Text
    public const string TextPrimary   = "CandyTextPrimary";
    public const string TextSecondary = "CandyTextSecondary";
    public const string TextMuted     = "CandyTextMuted";
    public const string TextAccent    = "CandyTextAccent";
    public const string TextSuccess   = "CandyTextSuccess";
    public const string TextWarning   = "CandyTextWarning";
    public const string TextDanger    = "CandyTextDanger";

    // Interactive
    public const string BtnPrimary    = "CandyBtnPrimary";
    public const string BtnHover      = "CandyBtnHover";
    public const string BtnGhost      = "CandyBtnGhost";
    public const string BtnGhostHover = "CandyBtnGhostHover";

    // Status
    public const string StatusOnline  = "CandyStatusOnline";
    public const string StatusAway    = "CandyStatusAway";
    public const string StatusOffline = "CandyStatusOffline";

    /// <summary>
    /// Registers all named colors. Color.AssignByName takes a uint in 0xAABBGGRR format.
    /// AssignByName internally increments Color.ThemeVersion on each call.
    /// </summary>
    public static void Apply()
    {
        // Backgrounds — backstage dark charcoal/navy
        Color.AssignByName(BgWindow,      0xFF17110D); // #0D1117 near-black navy
        Color.AssignByName(BgSidebar,     0xFF140E0A); // #0A0E14 almost black
        Color.AssignByName(BgCard,        0xFF2A1C14); // #141C2A dark slate
        Color.AssignByName(BgCardHover,   0xFF38281C); // #1C2838 hover slate
        Color.AssignByName(BgInput,       0xFF20150F); // #0F1520 input field
        Color.AssignByName(BgTabActive,   0xFF50301A); // #1A3050 highlight bg
        Color.AssignByName(BgTabInactive, 0x00000000); // transparent

        // Borders — steel blue
        Color.AssignByName(BorderCard,    0xFF6A4A2A); // #2A4A6A steel blue border
        Color.AssignByName(BorderDivider, 0xFF302018); // #182030 subtle divider
        Color.AssignByName(BorderFocus,   0xFFE8C070); // #70C0E8 ice blue focus ring

        // Text
        Color.AssignByName(TextPrimary,   0xFFF0E8E0); // #E0E8F0 near-white
        Color.AssignByName(TextSecondary, 0xFFA09080); // #8090A0 muted slate
        Color.AssignByName(TextMuted,     0xFF706050); // #506070 dim
        Color.AssignByName(TextAccent,    0xFFE8C070); // #70C0E8 ice blue
        Color.AssignByName(TextSuccess,   0xFF80C040); // #40C080 green
        Color.AssignByName(TextWarning,   0xFF40A8E0); // #E0A840 amber
        Color.AssignByName(TextDanger,    0xFF5050E0); // #E05050 red

        // Interactive — steel blue buttons
        Color.AssignByName(BtnPrimary,    0xFFB8804A); // #4A80B8 steel blue
        Color.AssignByName(BtnHover,      0xFFC8905A); // #5A90C8 lighter steel
        Color.AssignByName(BtnGhost,      0xFF38281A); // #1A2838 dark ghost
        Color.AssignByName(BtnGhostHover, 0xFF403024); // #243040 ghost hover

        // Status
        Color.AssignByName(StatusOnline,  0xFF80C840); // #40C880
        Color.AssignByName(StatusAway,    0xFF40A8C8); // #C8A840
        Color.AssignByName(StatusOffline, 0xFF706050); // #506070

        // Toolbar
        Color.AssignByName("Toolbar.Bg",          0xFF18100A); // #0A1018 near-black
        Color.AssignByName("Toolbar.Border",       0xFF6A4A2A); // #2A4A6A steel border
        Color.AssignByName("Toolbar.Icon",         0xFFB89060); // #6090B8 muted steel
        Color.AssignByName("Toolbar.IconActive",   0xFFE8C070); // #70C0E8 ice blue
        Color.AssignByName("Toolbar.Label",        0xFFA09080); // #8090A0 muted
        Color.AssignByName("Toolbar.ButtonHover",  0xFF38281A); // #1A2838
        Color.AssignByName("Toolbar.Glow",         0xFFE8C070); // #70C0E8 ice blue glow

        // Balloon
        Color.AssignByName("Balloon.Bg",           0xFF20150D); // #0D1520 deep dark
        Color.AssignByName("Balloon.Border",       0xFF6A4A2A); // #2A4A6A steel border
        Color.AssignByName("Balloon.TabStripBg",   0xFF18100A); // #0A1018 strip bg
        Color.AssignByName("Balloon.Separator",    0xFF38281A); // #1A2838

        // Tabs
        Color.AssignByName("Tab.Active",           0xFFE8C070); // #70C0E8 ice blue
        Color.AssignByName("Tab.ActiveBg",         0xFF50301A); // #1A3050 highlight
        Color.AssignByName("Tab.Inactive",         0xFF706050); // #506070 muted
        Color.AssignByName("Tab.InactiveBg",       0x00000000); // transparent
        Color.AssignByName("Tab.HoverBg",          0xFF38281A); // #1A2838
        Color.AssignByName("Tab.HoverFg",          0xFFD8C090); // #90C0D8
    }
}
