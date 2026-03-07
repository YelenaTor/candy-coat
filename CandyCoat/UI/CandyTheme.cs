using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Registers all Candy Coat named colors with Una.Drawing's theme system.
/// Call CandyTheme.Apply() once after DrawingLib.Setup().
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
    /// Source colors are defined in 0xAARRGGBB and converted accordingly.
    /// AssignByName internally increments Color.ThemeVersion on each call.
    /// </summary>
    public static void Apply()
    {
        // Backgrounds
        Color.AssignByName(BgWindow,      0xFF200F1A); // #1A0F20
        Color.AssignByName(BgSidebar,     0xFF1C0C14); // #140C1C
        Color.AssignByName(BgCard,        0xFF351B2D); // #2D1B35
        Color.AssignByName(BgCardHover,   0xFF452B3D); // #3D2B45
        Color.AssignByName(BgInput,       0xFF281220); // #201228
        Color.AssignByName(BgTabActive,   0xFFA04F7B); // #7B4FA0
        Color.AssignByName(BgTabInactive, 0xFF351B2D); // #2D1B35

        // Borders
        Color.AssignByName(BorderCard,    0xFFA04F7B); // #7B4FA0
        Color.AssignByName(BorderDivider, 0xFF45203D); // #3D2045
        Color.AssignByName(BorderFocus,   0xFFD9B6FF); // #FFB6D9

        // Text
        Color.AssignByName(TextPrimary,   0xFFF0D6FF); // #FFD6F0
        Color.AssignByName(TextSecondary, 0xFFBF9FB8); // #B89FBF
        Color.AssignByName(TextMuted,     0xFF80607A); // #7A6080
        Color.AssignByName(TextAccent,    0xFFD69DFF); // #FF9DD6
        Color.AssignByName(TextSuccess,   0xFFA0FF9D); // #9DFFA0
        Color.AssignByName(TextWarning,   0xFF70D7FF); // #FFD770
        Color.AssignByName(TextDanger,    0xFF7070FF); // #FF7070

        // Interactive
        Color.AssignByName(BtnPrimary,    0xFFD060B0); // #B060D0
        Color.AssignByName(BtnHover,      0xFFE070C0); // #C070E0
        Color.AssignByName(BtnGhost,      0xFF45203D); // #3D2045
        Color.AssignByName(BtnGhostHover, 0xFF55304D); // #4D3055

        // Status
        Color.AssignByName(StatusOnline,  0xFF70C860); // #60C870
        Color.AssignByName(StatusAway,    0xFF60D0FF); // #FFD060
        Color.AssignByName(StatusOffline, 0xFF80607A); // #7A6080

        // Toolbar + Balloon
        Color.AssignByName("Toolbar.Bg",          0xFF2E1A35); // dark purple
        Color.AssignByName("Toolbar.Border",      0xFF703060); // muted purple
        Color.AssignByName("Toolbar.Icon",        0xFFCEAACE); // soft lavender
        Color.AssignByName("Toolbar.IconActive",  0xFFBF9EFF); // pink-purple
        Color.AssignByName("Toolbar.Label",       0xFFCEAACE); // soft lavender
        Color.AssignByName("Toolbar.ButtonHover", 0xFF50203D); // hover bg
        Color.AssignByName("Toolbar.Glow",        0xFFBF9EFF); // glow ring

        Color.AssignByName("Balloon.Bg",          0xFF231535); // deep purple
        Color.AssignByName("Balloon.Border",      0xFF703060); // muted purple
        Color.AssignByName("Balloon.TabStripBg",  0xFF1E1030); // slightly darker
        Color.AssignByName("Balloon.Separator",   0xFF604060); // divider line

        Color.AssignByName("Tab.Active",          0xFFBF9EFF); // pink-purple text
        Color.AssignByName("Tab.ActiveBg",        0xFF3D1E50); // highlighted bg
        Color.AssignByName("Tab.Inactive",        0xFFBB88AA); // muted
        Color.AssignByName("Tab.InactiveBg",      0x00000000); // transparent
        Color.AssignByName("Tab.HoverBg",         0xFF401840);
        Color.AssignByName("Tab.HoverFg",         0xFFDDBBEE);
    }
}
