using System.Linq;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.UserInterface.Controls.Chat;
using Content.Client._RF.UserInterface.Controls.TreeMenu;
using Content.Client.ContextMenu.UI;
using Content.Client.Examine;
using Content.Client.PDA;
using Content.Client.Resources;
using Content.Client.Silicons.Laws.SiliconLawEditUi;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls.FancyTree;
using Content.Client.Verbs.UI;
using Content.Shared.Verbs;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._RF.Stylesheets;

public sealed class StyleFortress : StyleBase
{
    #region Colors

    public static readonly Color GoldFortress = Color.FromHex("#B07D2B");
    public static readonly Color RimSilver = Color.FromHex("#C0C0C0");
    public static readonly Color GraySilver = Color.FromHex("#7F7F7F");

    public static readonly Color SignalBlack = Color.FromHex("#2D2A23");
    public static readonly Color WoodenFortress = Color.FromHex("#5E4B3A");

    public static readonly Color BlackAmber = Color.FromHex("#0B0C0B");
    public static readonly Color GraphiteBlack = Color.FromHex("#1A1D1A");
    public static readonly Color DarkForest = Color.FromHex("#293329");
    public static readonly Color BrightGreen = Color.FromHex("#4E544E");

    public static readonly Color LightGood = Color.FromHex("#6EAD54");
    public static readonly Color Good = Color.FromHex("#61A53E");
    public static readonly Color DarkGood = Color.FromHex("#589338");
    public static readonly Color DarkestGood = Color.FromHex("#416D29");

    public static readonly Color LightBad = Color.FromHex("#BF2233");
    public static readonly Color Bad = Color.FromHex("#9B111E");
    public static readonly Color DarkBad = Color.FromHex("#8B0000");
    public static readonly Color DarkestBad = Color.FromHex("#681C23");

    // Button colors
    public static readonly Color ButtonColorDefault = WoodenFortress;
    public static readonly Color ButtonColorHovered = Color.FromHex("#7A614B");
    public static readonly Color ButtonColorPressed = Color.FromHex("#737A52");
    public static readonly Color ButtonColorDisabled = Color.FromHex("#352A21");

    public static readonly Color GoldButtonColorDefault = GoldFortress;
    public static readonly Color GoldButtonColorHovered = Color.FromHex("#B28A52");
    public static readonly Color GoldButtonColorPressed = Color.FromHex("#CE9033");
    public static readonly Color GoldButtonColorDisabled = Color.FromHex("#997746");

    public static readonly Color TreeMenuButtonColorDefault = Color.Transparent;
    public static readonly Color TreeMenuButtonColorHovered = SignalBlack;
    public static readonly Color TreeMenuButtonColorPressed = GraphiteBlack;
    //

    public static readonly Color ChatBackgroundColor = DarkForest.WithAlpha(0.8f);
    public static readonly Color DefaultPaperBackgroundColor = Color.FromHex("#eaedde");

    public static readonly Color DisabledButtonFontColor = RimSilver.WithAlpha(0.51f);
    public static readonly Color PlaceholderFontColor = Color.FromHex("#7C7763");

    public static readonly Color GoodGreenFore = Color.FromHex("#31843E");

    #endregion

    #region StyleClasses

    public const string StyleClassBorderedWindowPanel = "BorderedWindowPanel";
    public const string StyleClassInventorySlotBackground = "InventorySlotBackground";
    public const string StyleClassHandSlotHighlight = "HandSlotHighlight";
    public const string StyleClassChatPanel = "ChatPanel";
    public const string StyleClassTransparentBorderedWindowPanel = "TransparentBorderedWindowPanel";
    public const string StyleClassHotbarPanel = "HotbarPanel";
    public const string StyleClassTooltipPanel = "tooltipBox";
    public const string StyleClassTooltipAlertTitle = "tooltipAlertTitle";
    public const string StyleClassTooltipAlertDescription = "tooltipAlertDesc";
    public const string StyleClassTooltipAlertCooldown = "tooltipAlertCooldown";
    public const string StyleClassTooltipActionTitle = "tooltipActionTitle";
    public const string StyleClassTooltipActionDescription = "tooltipActionDesc";
    public const string StyleClassTooltipActionCooldown = "tooltipActionCooldown";
    public const string StyleClassTooltipActionRequirements = "tooltipActionCooldown";
    public const string StyleClassTooltipActionCharges = "tooltipActionCharges";
    public const string StyleClassHotbarSlotNumber = "hotbarSlotNumber";
    public const string StyleClassActionSearchBox = "actionSearchBox";
    public const string StyleClassChatLineEdit = "chatLineEdit";

    public const string StyleClassSliderRed = "Red";
    public const string StyleClassSliderGreen = "Green";
    public const string StyleClassSliderBlue = "Blue";
    public const string StyleClassSliderWhite = "White";

    public const string StyleClassLabelHeadingBigger = "LabelHeadingBigger";
    public const string StyleClassLabelKeyText = "LabelKeyText";
    public const string StyleClassLabelSecondaryColor = "LabelSecondaryColor";
    public const string StyleClassLabelBig = "LabelBig";
    public const string StyleClassLabelSmall = "LabelSmall";
    public const string StyleClassLabelSmallest = "LabelSmallest";

    public const string StyleClassButtonHelp = "HelpButton";

    public const string StyleClassPopupMessageSmall = "PopupMessageSmall";
    public const string StyleClassPopupMessageSmallCaution = "PopupMessageSmallCaution";
    public const string StyleClassPopupMessageMedium = "PopupMessageMedium";
    public const string StyleClassPopupMessageMediumCaution = "PopupMessageMediumCaution";
    public const string StyleClassPopupMessageLarge = "PopupMessageLarge";
    public const string StyleClassPopupMessageLargeCaution = "PopupMessageLargeCaution";

    // Used by the APC and SMES menus
    public const string StyleClassPowerStateNone = "PowerStateNone";
    public const string StyleClassPowerStateLow = "PowerStateLow";
    public const string StyleClassPowerStateGood = "PowerStateGood";

    public const string StyleClassItemStatus = "ItemStatus";
    public const string StyleClassItemStatusNotHeld = "ItemStatusNotHeld";

    // Background
    public const string StyleClassPanelLight = "PanelLight";
    public const string StyleClassPanelLightTransparent = "PanelLightTransparent";

    public const string StyleClassPanelLightBordered = "PanelLightBordered";
    public const string StyleClassPanelLightBorderedTransparent = "PanelLightBorderedTransparent";

    public const string StyleClassPanelDark = "PanelDark";
    public const string StyleClassPanelDarkTransparent = "PanelDarkTransparent";

    public const string StyleClassPanelDarkBordered = "PanelDarkBordered";
    public const string StyleClassPanelDarkBorderedTransparent = "PanelDarkBorderedTransparent";

    public const string StyleClassPanelHighlighted = "PanelHighlighted";
    public const string StyleClassPanelHighlightedTransparent = "PanelHighlightedTransparent";

    public const string StyleClassPanelAngleRectTransparent = "AngleRectTransparent";

    public const string StyleClassTopInfoPanel = "TopInfoPanel";
    public const string StyleClassTopInfoCellPanel = "TopInfoCellPanel";

    public const string StyleClassFoldableChatPanel = "FoldableChatPanel";

    // FancyBack
    public const string StyleClassFancyBackNone = "NoneBack";
    public const string StyleClassFancyBackWooden = "WoodenBack";

    // Dividers
    public const string StyleClassHighDividerDark = "HighDividerDark";
    public const string StyleClassLowDividerDark = "LowDividerDark";

    // Buttons
    public const string StyleClassChatChannelSelectorButton = "chatSelectorOptionButton";
    public const string StyleClassChatFilterOptionButton = "chatFilterOptionButton";
    public const string StyleClassStorageButton = "storageButton";

    public const string StyleClassCrossButtonRed = "CrossButtonRed";
    public const string StyleClassButtonColorRed = "ButtonColorRed";
    public const string StyleClassButtonColorGreen = "ButtonColorGreen";
    public const string StyleClassButtonColorGold = "ButtonColorGold";
    public const string StyleClassButtonTransparent = "ButtonTransparent";
    public const string StyleClassButtonBig = "ButtonBig";

    // Bwoink
    public const string StyleClassPinButtonPinned = "pinButtonPinned";
    public const string StyleClassPinButtonUnpinned = "pinButtonUnpinned";

    #endregion

    public static readonly ProtoId<ShaderPrototype> TileShader = "TiledTexture";

    public override Stylesheet Stylesheet { get; }

    public StyleFortress(IResourceCache resCache) : base(resCache)
    {
        Texture GetTex(string path)
        {
            return resCache.GetTexture($"/Textures/_RF/Interface/Style/{path}");
        }

        StyleBoxTexture StyleBoxTex(string path, StyleBoxTexture? other = null)
        {
            return other == null
                ? new StyleBoxTexture { Texture = GetTex(path) }
                : new StyleBoxTexture(other) { Texture = GetTex(path) };
        }

        #region Fonts

        var boxFont13 = resCache.GetFont("/Fonts/Boxfont-round/Boxfont Round.ttf", 13);
        var sourceCode8 = resCache.SourceCodeStack(size: 8);
        var sourceCode10 = resCache.SourceCodeStack(size: 10);
        var sourceCode12 = resCache.SourceCodeStack(size: 12);
        var sourceCode15 = resCache.SourceCodeStack(size: 15);
        var sourceCode16 = resCache.SourceCodeStack(size: 16);
        var sourceCodeBold12 = resCache.SourceCodeStack(SourceCodeVariant.Bold, 12);
        var sourceCodeBold14 = resCache.SourceCodeStack(SourceCodeVariant.Bold, 14);
        var sourceCodeBold16 = resCache.SourceCodeStack(SourceCodeVariant.Bold, 16);
        var sourceCodeBold18 = resCache.SourceCodeStack(SourceCodeVariant.Bold, 18);
        var sourceCodeBold20 = resCache.SourceCodeStack(SourceCodeVariant.Bold, 20);
        var sourceCodeItalic10 = resCache.SourceCodeStack(SourceCodeVariant.Italic);
        var sourceCodeItalic12 = resCache.SourceCodeStack(SourceCodeVariant.Italic, 12);
        var sourceCodeBoldItalic12 = resCache.SourceCodeStack(SourceCodeVariant.BoldItalic, 12);

        #endregion

        #region Textures

        #region Window

        var windowHeader = new StyleBoxTexture
        {
            Texture = GetTex("window_header.png"),
            PatchMarginBottom = 3,
            ExpandMarginBottom = 3,
            ContentMarginBottomOverride = 0,
        };

        var windowHeaderAlert = new StyleBoxTexture
        {
            Texture = GetTex("window_header_alert.png"),
            PatchMarginBottom = 3,
            ExpandMarginBottom = 3,
            ContentMarginBottomOverride = 0,
        };

        var windowBackground = StyleBoxTex("window_background.png");
        windowBackground.SetPatchMargin(StyleBox.Margin.Horizontal | StyleBox.Margin.Bottom, 2);
        windowBackground.SetExpandMargin(StyleBox.Margin.Horizontal | StyleBox.Margin.Bottom, 2);

        var borderedWindowBackground = StyleBoxTex("window_background_bordered.png");
        borderedWindowBackground.SetPatchMargin(StyleBox.Margin.All, 2);

        var borderedTransparentWindowBackground = StyleBoxTex("transparent_window_background_bordered.png");
        borderedTransparentWindowBackground.SetPatchMargin(StyleBox.Margin.All, 2);

        var hotbarBackground = new StyleBoxTexture(borderedTransparentWindowBackground);
        hotbarBackground.SetExpandMargin(StyleBox.Margin.All, 4);

        var contextMenuBackground = StyleBoxTex("window_background_bordered.png");
        contextMenuBackground.SetPatchMargin(StyleBox.Margin.All, ContextMenuElement.ElementMargin);

        var fancyWindowHeader = StyleBoxTex("fancy_window_header.png");
        fancyWindowHeader.SetPatchMargin(StyleBox.Margin.Top, 3);
        fancyWindowHeader.SetPatchMargin(StyleBox.Margin.Bottom, 1);

        var lightFancyWindowHeader = StyleBoxTex("light_fancy_window_header.png", fancyWindowHeader);

        #endregion

        #region Buttons

        var button = StyleBoxTex("button.png");
        button.SetPatchMargin(StyleBox.Margin.All, 5);
        button.SetPadding(StyleBox.Margin.All, 1);
        button.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        button.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);

        var buttonOpenRight = StyleBoxTex("button_open_right.png", button);
        buttonOpenRight.SetPatchMargin(StyleBox.Margin.All, 11);
        buttonOpenRight.SetPatchMargin(StyleBox.Margin.Right, 5);
        buttonOpenRight.SetContentMarginOverride(StyleBox.Margin.Left, 11);
        buttonOpenRight.SetPadding(StyleBox.Margin.Right, 2);

        var buttonOpenLeft = StyleBoxTex("button_open_left.png", button);
        buttonOpenLeft.SetPatchMargin(StyleBox.Margin.All, 11);
        buttonOpenLeft.SetPatchMargin(StyleBox.Margin.Left, 5);
        buttonOpenLeft.SetContentMarginOverride(StyleBox.Margin.Right, 11);

        var buttonOpenBoth = StyleBoxTex("button_open_both.png", button);
        buttonOpenBoth.SetPatchMargin(StyleBox.Margin.All, 11);
        buttonOpenBoth.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);

        var buttonSquare = StyleBoxTex("button_square.png", button);
        buttonSquare.SetPatchMargin(StyleBox.Margin.All, 2);
        buttonSquare.SetContentMarginOverride(StyleBox.Margin.All, 5);

        var buttonStorage = new StyleBoxTexture(button);
        buttonStorage.SetPatchMargin(StyleBox.Margin.All, 0);
        buttonStorage.SetPadding(StyleBox.Margin.All, 0);
        buttonStorage.SetContentMarginOverride(StyleBox.Margin.Vertical, 0);
        buttonStorage.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);

        var buttonContext = new StyleBoxTexture { Texture = Texture.White };

        var chatChannelButton = StyleBoxTex("rounded_button.png");
        chatChannelButton.SetPatchMargin(StyleBox.Margin.All, 5);
        chatChannelButton.SetPadding(StyleBox.Margin.All, 2);

        var chatFilterButton = StyleBoxTex("rounded_button_bordered.png");
        chatFilterButton.SetPatchMargin(StyleBox.Margin.All, 5);
        chatFilterButton.SetPadding(StyleBox.Margin.All, 2);

        var treeMenuButton = StyleBoxTex("rounded_button_bordered.png");
        treeMenuButton.SetPatchMargin(StyleBox.Margin.All, 5);

        #endregion

        #region Scrolls

        var vScrollBarGrabberNormal = new StyleBoxFlat
        {
            BackgroundColor = ButtonColorDefault.WithAlpha(0.35f),
            ContentMarginLeftOverride = DefaultGrabberSize,
            ContentMarginTopOverride = DefaultGrabberSize,
        };
        var vScrollBarGrabberHover = new StyleBoxFlat(vScrollBarGrabberNormal)
            { BackgroundColor = ButtonColorHovered.WithAlpha(0.35f) };
        var vScrollBarGrabberGrabbed = new StyleBoxFlat(vScrollBarGrabberNormal)
            { BackgroundColor = ButtonColorPressed.WithAlpha(0.35f) };

        var hScrollBarGrabberNormal = new StyleBoxFlat
        {
            BackgroundColor = ButtonColorDefault.WithAlpha(0.35f),
            ContentMarginTopOverride = DefaultGrabberSize,
        };
        var hScrollBarGrabberHover = new StyleBoxFlat(hScrollBarGrabberNormal)
            { BackgroundColor = ButtonColorHovered.WithAlpha(0.35f) };
        var hScrollBarGrabberGrabbed = new StyleBoxFlat(hScrollBarGrabberNormal)
            { BackgroundColor = ButtonColorPressed.WithAlpha(0.35f) };

        #endregion

        var textureInvertedTriangle = GetTex("inverted_triangle.png");

        var lineEdit = StyleBoxTex("lineedit.png");
        lineEdit.SetPatchMargin(StyleBox.Margin.All, 3);
        lineEdit.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

        var actionSearchBox = StyleBoxTex("dark_panel_dark_thin_border.png");
        actionSearchBox.SetPatchMargin(StyleBox.Margin.All, 3);
        actionSearchBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

        #region Tab Container

        var tabContainerPanel = new StyleBoxTexture { Texture = GetTex("tabcontainer_panel.png") };
        tabContainerPanel.SetPatchMargin(StyleBox.Margin.All, 2);
        tabContainerPanel.SetContentMarginOverride(StyleBox.Margin.Top, 2);

        var tabContainerBoxActive = new StyleBoxFlat { BackgroundColor = DarkForest };
        tabContainerBoxActive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

        var tabContainerBoxInactive = new StyleBoxFlat { BackgroundColor = GraphiteBlack };
        tabContainerBoxInactive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

        #endregion

        #region Progress bar

        var progressBarBackground = StyleBoxTex("dark_panel_light_thin_border.png");
        progressBarBackground.SetPatchMargin(StyleBox.Margin.All, 1f);
        progressBarBackground.SetExpandMargin(StyleBox.Margin.All, 1f);
        progressBarBackground.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);

        var progressBarForeground = new StyleBoxFlat { BackgroundColor = DarkGood };
        progressBarForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);

        #endregion

        // Tooltip box
        var tooltipBox = new StyleBoxTexture { Texture = GetTex("tooltip.png") };
        tooltipBox.SetPatchMargin(StyleBox.Margin.All, 2);
        tooltipBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);

        // Whisper box
        var whisperBox = new StyleBoxTexture { Texture = GetTex("whisper.png") };
        whisperBox.SetPatchMargin(StyleBox.Margin.All, 2);
        whisperBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);

        // Placeholder
        var placeholder = new StyleBoxTexture
        {
            Texture = GetTex("placeholder.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        placeholder.SetPatchMargin(StyleBox.Margin.All, 19);
        placeholder.SetExpandMargin(StyleBox.Margin.All, -5);

        #region Item list

        var itemListItemBackground = new StyleBoxFlat { BackgroundColor = DarkForest };
        itemListItemBackground.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        itemListItemBackground.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);

        var itemListBackgroundSelected =
            new StyleBoxFlat(itemListItemBackground) { BackgroundColor = BrightGreen };
        var itemListItemBackgroundDisabled =
            new StyleBoxFlat(itemListItemBackground) { BackgroundColor = GraphiteBlack };
        var itemListItemBackgroundTransparent =
            new StyleBoxFlat(itemListItemBackground) { BackgroundColor = Color.Transparent };

        var listContainerButton = new StyleBoxTexture
        {
            Texture = resCache.GetTexture("/Textures/Interface/Nano/square.png"),
            ContentMarginLeftOverride = 10,
        };

        #endregion

        // RimHeading
        var rimHeadingBox = new StyleBoxTexture
        {
            Texture = GetTex("rimheading.png"),
            ContentMarginTopOverride = 2,
            PaddingTop = 4,
        };
        rimHeadingBox.SetPatchMargin(StyleBox.Margin.All, 2);
        rimHeadingBox.SetPatchMargin(StyleBox.Margin.Top, 10);
        rimHeadingBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);

        #region Slider

        var sliderFillBox = new StyleBoxTexture
        {
            Texture = GetTex("slider_fill.png"),
            Modulate = Good,
        };

        var sliderBackBox = new StyleBoxTexture
        {
            Texture = GetTex("slider_fill.png"),
            Modulate = GraphiteBlack,
        };

        var sliderForeBox = new StyleBoxTexture
        {
            Texture = GetTex("slider_outline.png"),
            Modulate = DarkGood,
        };

        var sliderGrabBox = new StyleBoxTexture
        {
            Texture = GetTex("slider_grabber.png"),
            Modulate = ButtonColorDefault,
        };

        sliderFillBox.SetPatchMargin(StyleBox.Margin.All, 12);
        sliderBackBox.SetPatchMargin(StyleBox.Margin.All, 12);
        sliderForeBox.SetPatchMargin(StyleBox.Margin.All, 12);
        sliderGrabBox.SetPatchMargin(StyleBox.Margin.All, 12);

        var sliderFillGreen = new StyleBoxTexture(sliderFillBox) { Modulate = Good };
        var sliderFillRed = new StyleBoxTexture(sliderFillBox) { Modulate = Bad };
        var sliderFillBlue = new StyleBoxTexture(sliderFillBox) { Modulate = Color.Blue };
        var sliderFillWhite = new StyleBoxTexture(sliderFillBox) { Modulate = RimSilver };

        #endregion

        // Default paper background
        var paperBackground = new StyleBoxTexture
        {
            Texture = resCache.GetTexture("/Textures/Interface/Paper/paper_background_default.svg.96dpi.png"),
            Modulate = DefaultPaperBackgroundColor,
        };
        paperBackground.SetPatchMargin(StyleBox.Margin.All, 16.0f);

        var contextMenuExpansionTexture = resCache.GetTexture("/Textures/Interface/VerbIcons/group.svg.192dpi.png");
        var verbMenuConfirmationTexture = resCache.GetTexture("/Textures/Interface/VerbIcons/group.svg.192dpi.png");

        // south-facing arrow:
        var directionIconArrowTex = resCache.GetTexture("/Textures/Interface/VerbIcons/drop.svg.192dpi.png");
        var directionIconQuestionTex = resCache.GetTexture("/Textures/Interface/VerbIcons/information.svg.192dpi.png");
        var directionIconHereTex = resCache.GetTexture("/Textures/Interface/VerbIcons/dot.svg.192dpi.png");

        // Inventory
        var invSlotBgTex = resCache.GetTexture("/Textures/Interface/Inventory/inv_slot_background.png");
        var invSlotBg = new StyleBoxTexture { Texture = invSlotBgTex };
        invSlotBg.SetPatchMargin(StyleBox.Margin.All, 2);
        invSlotBg.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var handSlotHighlightTex = resCache.GetTexture("/Textures/Interface/Inventory/hand_slot_highlight.png");
        var handSlotHighlight = new StyleBoxTexture { Texture = handSlotHighlightTex };
        handSlotHighlight.SetPatchMargin(StyleBox.Margin.All, 2);

        #region PanelContainer

        var lightBorderedPanel = new StyleBoxFlat
        {
            BackgroundColor = DarkForest,
            BorderColor = WoodenFortress,
            BorderThickness = new Thickness(1),
        };
        lightBorderedPanel.SetContentMarginOverride(StyleBox.Margin.All, 10);

        var lightBorderedPanelTransparent = new StyleBoxFlat(lightBorderedPanel)
            { BackgroundColor = DarkForest.WithAlpha(0.8f) };

        var darkBorderedPanel = new StyleBoxFlat(lightBorderedPanel)
            { BackgroundColor = GraphiteBlack};
        var darkBorderedPanelTransparent = new StyleBoxFlat(lightBorderedPanel)
            { BackgroundColor = GraphiteBlack.WithAlpha(0.8f) };

        var lightPanel = new StyleBoxFlat { BackgroundColor = DarkForest };
        lightPanel.SetContentMarginOverride(StyleBox.Margin.All, 10);

        var lightPanelTransparent = new StyleBoxFlat(lightPanel) { BackgroundColor = DarkForest.WithAlpha(0.8f) };

        var darkPanel = new StyleBoxFlat(lightPanel) { BackgroundColor = GraphiteBlack };
        var darkPanelTransparent = new StyleBoxFlat(lightPanel) { BackgroundColor = GraphiteBlack.WithAlpha(0.8f) };

        var highlightedPanel = new StyleBoxFlat
        {
            BackgroundColor = GraphiteBlack,
            BorderThickness = new Thickness(1),
            BorderColor = BlackAmber,
        };
        highlightedPanel.SetContentMarginOverride(StyleBox.Margin.All, 10);

        var highlightedPanelTransparent = new StyleBoxFlat(highlightedPanel)
            { BackgroundColor = GraphiteBlack.WithAlpha(0.8f) };

        var angleRect = StyleBoxTex("angle_rect_panel.png");
        angleRect.SetPatchMargin(StyleBox.Margin.All, 11);
        angleRect.SetContentMarginOverride(StyleBox.Margin.All, 3);

        var angleRectTransparent = StyleBoxTex("angle_rect_panel_transparent.png", angleRect);

        var topInfoPanel = new StyleBoxTexture
        {
            Texture = GetTex("top_info_panel.png"),
            PatchMarginLeft = 4,
            PatchMarginTop = 4,
            PatchMarginRight = 8,
            PatchMarginBottom = 9,
        };
        topInfoPanel.SetContentMarginOverride(StyleBox.Margin.All, 10);
        topInfoPanel.SetContentMarginOverride(StyleBox.Margin.Bottom, 9);

        var topInfoPanelCell = new StyleBoxFlat
        {
            BackgroundColor = GraphiteBlack,
            BorderColor = BlackAmber,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        var chatBubble = new StyleBoxTexture
        {
            Texture = GetTex("chat_messages_bubble.png"),
            Modulate = LightBad,
        };
        chatBubble.SetPatchMargin(StyleBox.Margin.All, 3);
        chatBubble.SetContentMarginOverride(StyleBox.Margin.Horizontal, 3);

        var foldableChatPanel = new StyleBoxFlat(highlightedPanel);
        foldableChatPanel.SetContentMarginOverride(StyleBox.Margin.All, 3);

        #endregion

        #endregion

        #region StyleRules

        Stylesheet = new Stylesheet(BaseRules.Concat(new[]
        {
            Element()
                .Prop("font", sourceCode12),

            Element()
                .Class(StyleClassItalic)
                .Prop("font", sourceCodeItalic12),

            #region Scroll bars

            Element<VScrollBar>()
                .Prop(ScrollBar.StylePropertyGrabber, vScrollBarGrabberNormal),

            Element<VScrollBar>()
                .Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, vScrollBarGrabberHover),

            Element<VScrollBar>()
                .Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, vScrollBarGrabberGrabbed),

            Element<HScrollBar>()
                .Prop(ScrollBar.StylePropertyGrabber, hScrollBarGrabberNormal),

            Element<HScrollBar>()
                .Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, hScrollBarGrabberHover),

            Element<HScrollBar>()
                .Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, hScrollBarGrabberGrabbed),

            #endregion

            #region Window

            // Window title.
            Element<Label>()
                .Class(DefaultWindow.StyleClassWindowTitle)
                .Prop(Label.StylePropertyFontColor, GoldFortress)
                .Prop(Label.StylePropertyFont, sourceCodeBold14),

            // Alert (white) window title.
            Element<Label>()
                .Class("windowTitleAlert")
                .Prop(Label.StylePropertyFontColor, RimSilver)
                .Prop(Label.StylePropertyFont, sourceCodeBold14),

            // Window background.
            Element()
                .Class(DefaultWindow.StyleClassWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, windowBackground),

            // bordered window background
            Element()
                .Class(StyleClassBorderedWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, borderedWindowBackground),
            Element()
                .Class(StyleClassTransparentBorderedWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, borderedTransparentWindowBackground),

            // Window header.
            Element<PanelContainer>()
                .Class(DefaultWindow.StyleClassWindowHeader)
                .Prop(PanelContainer.StylePropertyPanel, windowHeader),

            // Alert (red) window header.
            Element<PanelContainer>()
                .Class("windowHeaderAlert")
                .Prop(PanelContainer.StylePropertyPanel, windowHeaderAlert),

            // Window Header Help Button
            Element<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Nano/help.png"))
                .Prop(Control.StylePropertyModulateSelf, BrightGreen),

            Element<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, DarkBad),

            Element<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, DarkestBad),

            // Window Close Button
            Element<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .Prop(TextureButton.StylePropertyTexture, GetTex("cross.png"))
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDefault),

            Element<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .Pseudo(TextureButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorHovered),

            Element<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .Pseudo(TextureButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorPressed),

            // Window Headers
            Element<Label>()
                .Class("FancyWindowTitle")
                .Prop("font", boxFont13)
                .Prop("font-color", GoldFortress),

            Element<PanelContainer>()
                .Class("WindowHeadingBackground")
                .Prop(PanelContainer.StylePropertyPanel, fancyWindowHeader),

            Element<PanelContainer>()
                .Class("WindowHeadingBackgroundLight")
                .Prop(PanelContainer.StylePropertyPanel, lightFancyWindowHeader),

            #endregion

            #region Button

            // Shapes for the buttons.
            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Prop(ContainerButton.StylePropertyStyleBox, button),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonOpenRight)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenRight),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonOpenLeft)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenLeft),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonOpenBoth)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenBoth),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonSquare)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonSquare),

            Element<Label>()
                .Class(ContainerButton.StyleClassButton)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

            // Colors for the buttons.
            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled),

            // Colors for the transparent buttons.
            Element<ContainerButton>()
                .Class(StyleClassButtonTransparent)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault.WithAlpha(200)),

            Element<ContainerButton>()
                .Class(StyleClassButtonTransparent)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered.WithAlpha(200)),

            Element<ContainerButton>()
                .Class(StyleClassButtonTransparent)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed.WithAlpha(200)),

            Element<ContainerButton>()
                .Class(StyleClassButtonTransparent)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled.WithAlpha(200)),

            // Colors for the caution buttons.
            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonCaution)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonCaution)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightBad),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonCaution)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, DarkBad),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, ButtonCaution)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, DarkestBad),

            // Colors for confirm buttons confirm states.
            Element<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightBad),

            Element<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, DarkBad),

            Element<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, DarkestBad),

            new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(Button),
                        null,
                        null,
                        new[] { ContainerButton.StylePseudoClassDisabled }),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty("font-color", DisabledButtonFontColor) }),

            // Examine buttons
            Element<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),

            Element<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDefault),

            Element<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorHovered),

            Element<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorPressed),

            Element<ExamineButton>()
                .Class(ExamineButton.StyleClassExamineButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDisabled),

            // Tree menu
            Element<TreeMenuButton>()
                .Class(TreeMenuButton.StyleClassTreeMenuButton)
                .Prop(ContainerButton.StylePropertyStyleBox, treeMenuButton)
                .Prop(TreeMenuButton.StylePropertyMarkerTexture, GetTex("tree_menu_button_marker.png"))
                .Prop(TreeMenuButton.StylePropertyMarkerColor, GoldButtonColorDefault),

            Element<TreeMenuButton>()
                .Class(TreeMenuButton.StyleClassTreeMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox,
                    new StyleBoxTexture(treeMenuButton) { Modulate = TreeMenuButtonColorDefault }),

            Element<TreeMenuButton>()
                .Class(TreeMenuButton.StyleClassTreeMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, TreeMenuButtonColorHovered)
                .Prop(TreeMenuButton.StylePropertyMarkerTexture, GetTex("tree_menu_button_marker_hovered.png"))
                .Prop(TreeMenuButton.StylePropertyMarkerColor, GoldButtonColorDefault),

            Element<TreeMenuButton>()
                .Class(TreeMenuButton.StyleClassTreeMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, TreeMenuButtonColorPressed),

            #region Gold Button

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDefault),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorHovered),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorPressed),

            Element<ContainerButton>()
                .Class(ContainerButton.StyleClassButton, StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDisabled),

            Element<TextureButton>()
                .Class(StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDefault),

            Element<TextureButton>()
                .Class(StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorHovered),

            Element<TextureButton>()
                .Class(StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorPressed),

            Element<TextureButton>()
                .Class(StyleClassButtonColorGold)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, GoldButtonColorDisabled),

            #endregion

            #endregion

            #region Context Menu

            // Context Menu window
            Element<PanelContainer>()
                .Class(ContextMenuPopup.StyleClassContextMenuPopup)
                .Prop(PanelContainer.StylePropertyPanel, contextMenuBackground),

            // Context menu buttons
            Element<ContextMenuElement>()
                .Class(ContextMenuElement.StyleClassContextMenuButton)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),

            Element<ContextMenuElement>()
                .Class(ContextMenuElement.StyleClassContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, GraphiteBlack),

            Element<ContextMenuElement>()
                .Class(ContextMenuElement.StyleClassContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, SignalBlack),

            Element<ContextMenuElement>()
                .Class(ContextMenuElement.StyleClassContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, WoodenFortress),

            Element<ContextMenuElement>()
                .Class(ContextMenuElement.StyleClassContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, BlackAmber),

            // Context Menu Labels
            Element<RichTextLabel>()
                .Class(InteractionVerb.DefaultTextStyleClass)
                .Prop(Label.StylePropertyFont, sourceCodeBoldItalic12),

            Element<RichTextLabel>()
                .Class(ActivationVerb.DefaultTextStyleClass)
                .Prop(Label.StylePropertyFont, sourceCodeBold12),

            Element<RichTextLabel>()
                .Class(AlternativeVerb.DefaultTextStyleClass)
                .Prop(Label.StylePropertyFont, sourceCodeItalic12),

            Element<RichTextLabel>()
                .Class(Verb.DefaultTextStyleClass)
                .Prop(Label.StylePropertyFont, sourceCode12),

            Element<TextureRect>()
                .Class(ContextMenuElement.StyleClassContextMenuExpansionTexture)
                .Prop(TextureRect.StylePropertyTexture, contextMenuExpansionTexture),

            Element<TextureRect>()
                .Class(VerbMenuElement.StyleClassVerbMenuConfirmationTexture)
                .Prop(TextureRect.StylePropertyTexture, verbMenuConfirmationTexture),

            // Context menu confirm buttons
            Element<ContextMenuElement>()
                .Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),

            Element<ContextMenuElement>()
                .Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<ContextMenuElement>()
                .Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightBad),

            Element<ContextMenuElement>()
                .Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, DarkBad),

            Element<ContextMenuElement>()
                .Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, DarkestBad),

            #endregion

            // Direction / arrow icon
            Element<DirectionIcon>()
                .Class(DirectionIcon.StyleClassDirectionIconArrow)
                .Prop(TextureRect.StylePropertyTexture, directionIconArrowTex),

            Element<DirectionIcon>()
                .Class(DirectionIcon.StyleClassDirectionIconUnknown)
                .Prop(TextureRect.StylePropertyTexture, directionIconQuestionTex),

            Element<DirectionIcon>()
                .Class(DirectionIcon.StyleClassDirectionIconHere)
                .Prop(TextureRect.StylePropertyTexture, directionIconHereTex),

            #region Storage Button

            // Thin buttons (No padding nor vertical margin)
            Element<ContainerButton>()
                .Class(StyleClassStorageButton)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonStorage),

            Element<ContainerButton>()
                .Class(StyleClassStorageButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

            Element<ContainerButton>()
                .Class(StyleClassStorageButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

            Element<ContainerButton>()
                .Class(StyleClassStorageButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

            Element<ContainerButton>()
                .Class(StyleClassStorageButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled),

            #endregion

            #region ListContainer

            Element<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Prop(ContainerButton.StylePropertyStyleBox, listContainerButton),

            Element<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, new Color(55, 55, 68)),

            Element<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, new Color(75, 75, 86)),

            Element<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, new Color(75, 75, 86)),

            Element<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, new Color(10, 10, 12)),

            #endregion

            #region Main menu

            // Make those buttons bigger.
            new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(Button), null, "mainMenu", null),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty("font", sourceCodeBold16) }),

            Element<Button>()
                .Identifier("mainMenu")
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenBoth),

            //  also make those buttons slightly more separated.
            Element<BoxContainer>()
                .Class("mainMenuVBox")
                .Prop(BoxContainer.StylePropertySeparation, 2),

            #endregion

            #region LineEdit

            // Fancy LineEdit
            Element<LineEdit>()
                .Prop(LineEdit.StylePropertyStyleBox, lineEdit),

            Element<LineEdit>()
                .Prop(LineEdit.StyleClassLineEditNotEditable, GraySilver),

            Element<LineEdit>()
                .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", PlaceholderFontColor),

            Element<TextEdit>()
                .Pseudo(TextEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", PlaceholderFontColor),

            #endregion

            // chat subpanels (chat LineEdit backing, popup backings)
            Element<PanelContainer>()
                .Class(StyleClassChatPanel)
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat { BackgroundColor = ChatBackgroundColor }),

            // Chat LineEdit - we don't actually draw a StyleBox around the LineEdit itself, we put it around the
            // input + other buttons, so we must clear the default StyleBox
            Element<LineEdit>()
                .Class(StyleClassChatLineEdit)
                .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty()),

            // Action SearchBox LineEdit
            Element<LineEdit>()
                .Class(StyleClassActionSearchBox)
                .Prop(LineEdit.StylePropertyStyleBox, actionSearchBox),

            // TabContainer
            Element<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, tabContainerPanel)
                .Prop(TabContainer.StylePropertyTabStyleBox, tabContainerBoxActive)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabContainerBoxInactive),

            // ProgressBar
            Element<ProgressBar>()
                .Prop(ProgressBar.StylePropertyBackground, progressBarBackground)
                .Prop(ProgressBar.StylePropertyForeground, progressBarForeground),

            #region CheckBox

            Element<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Prop(TextureRect.StylePropertyTexture, GetTex("checkbox_unchecked.png")),

            Element<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox, CheckBox.StyleClassCheckBoxChecked)
                .Prop(TextureRect.StylePropertyTexture, GetTex("checkbox_checked.png")),

            Element<BoxContainer>()
                .Class(CheckBox.StyleClassCheckBox)
                .Prop(BoxContainer.StylePropertySeparation, 10),

            #endregion

            Element<PanelContainer>()
                .Class("speechBox", "sayBox")
                .Prop(PanelContainer.StylePropertyPanel, tooltipBox),

            Element<PanelContainer>()
                .Class("speechBox", "whisperBox")
                .Prop(PanelContainer.StylePropertyPanel, whisperBox),

            new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(PanelContainer), new[] { "speechBox", "whisperBox" }, null, null),
                    new SelectorElement(typeof(RichTextLabel), new[] { "bubbleContent" }, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFont, sourceCodeItalic12) }),

            new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(PanelContainer), new[] { "speechBox", "emoteBox" }, null, null),
                    new SelectorElement(typeof(RichTextLabel), null, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFont, sourceCodeItalic12) }),

            Element<RichTextLabel>()
                .Class(StyleClassLabelKeyText)
                .Prop(Label.StylePropertyFont, sourceCodeBold12)
                .Prop(Control.StylePropertyModulateSelf, GoldFortress),

            #region Tooltip

            Element<Tooltip>()
                .Prop(PanelContainer.StylePropertyPanel, tooltipBox),
            Element<PanelContainer>()
                .Class(StyleClassTooltipPanel)
                .Prop(PanelContainer.StylePropertyPanel, tooltipBox),

            // alert tooltip
            Element<RichTextLabel>()
                .Class(StyleClassTooltipAlertTitle)
                .Prop("font", sourceCodeBold18),
            Element<RichTextLabel>()
                .Class(StyleClassTooltipAlertDescription)
                .Prop("font", sourceCode16),
            Element<RichTextLabel>()
                .Class(StyleClassTooltipAlertCooldown)
                .Prop("font", sourceCode16),

            // action tooltip
            Element<RichTextLabel>()
                .Class(StyleClassTooltipActionTitle)
                .Prop("font", sourceCodeBold16),
            Element<RichTextLabel>()
                .Class(StyleClassTooltipActionDescription)
                .Prop("font", sourceCode15),
            Element<RichTextLabel>()
                .Class(StyleClassTooltipActionCooldown)
                .Prop("font", sourceCode15),
            Element<RichTextLabel>()
                .Class(StyleClassTooltipActionRequirements)
                .Prop("font", sourceCode15),
            Element<RichTextLabel>()
                .Class(StyleClassTooltipActionCharges)
                .Prop("font", sourceCode15),

            // Entity tooltip
            Element<PanelContainer>()
                .Class(ExamineSystem.StyleClassEntityTooltip)
                .Prop(PanelContainer.StylePropertyPanel, tooltipBox),

            #endregion

            // small number for the entity counter in the entity menu
            Element<Label>()
                .Class(ContextMenuElement.StyleClassEntityMenuIconLabel)
                .Prop(Label.StylePropertyFont, sourceCode10)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Right),

            #region ItemList

            Element<ItemList>()
                .Prop(ItemList.StylePropertyBackground, new StyleBoxFlat { BackgroundColor = DarkForest })
                .Prop(ItemList.StylePropertyItemBackground, itemListItemBackground)
                .Prop(ItemList.StylePropertyDisabledItemBackground, itemListItemBackgroundDisabled)
                .Prop(ItemList.StylePropertySelectedItemBackground, itemListBackgroundSelected),

            Element<ItemList>()
                .Class("transparentItemList")
                .Prop(ItemList.StylePropertyBackground, new StyleBoxFlat { BackgroundColor = Color.Transparent })
                .Prop(ItemList.StylePropertyItemBackground, itemListItemBackgroundTransparent)
                .Prop(ItemList.StylePropertyDisabledItemBackground, itemListItemBackgroundDisabled)
                .Prop(ItemList.StylePropertySelectedItemBackground, itemListBackgroundSelected),

            Element<ItemList>()
                .Class("transparentBackgroundItemList")
                .Prop(ItemList.StylePropertyBackground, new StyleBoxFlat { BackgroundColor = Color.Transparent })
                .Prop(ItemList.StylePropertyItemBackground, itemListItemBackground)
                .Prop(ItemList.StylePropertyDisabledItemBackground, itemListItemBackgroundDisabled)
                .Prop(ItemList.StylePropertySelectedItemBackground, itemListBackgroundSelected),

            #endregion

            // Tree
            Element<Tree>()
                .Prop(Tree.StylePropertyBackground, new StyleBoxFlat { BackgroundColor = DarkForest })
                .Prop(Tree.StylePropertyItemBoxSelected,
                    new StyleBoxFlat
                    {
                        BackgroundColor = GraphiteBlack,
                        ContentMarginLeftOverride = 4,
                    }),

            // Placeholder
            Element<Placeholder>()
                .Prop(PanelContainer.StylePropertyPanel, placeholder),

            Element<Label>()
                .Class(Placeholder.StyleClassPlaceholderText)
                .Prop(Label.StylePropertyFont, sourceCode16)
                .Prop(Label.StylePropertyFontColor, PlaceholderFontColor),

            #region Labels

            // Big Label
            Element<Label>()
                .Class(StyleClassLabelHeading)
                .Prop(Label.StylePropertyFont, sourceCodeBold16)
                .Prop(Label.StylePropertyFontColor, GoldFortress),

            // Bigger Label
            Element<Label>()
                .Class(StyleClassLabelHeadingBigger)
                .Prop(Label.StylePropertyFont, sourceCodeBold20)
                .Prop(Label.StylePropertyFontColor, GoldFortress),

            // Small Label
            Element<Label>()
                .Class(StyleClassLabelSubText)
                .Prop(Label.StylePropertyFont, sourceCode10)
                .Prop(Label.StylePropertyFontColor, GraySilver),

            // Label Key
            Element<Label>()
                .Class(StyleClassLabelKeyText)
                .Prop(Label.StylePropertyFont, sourceCodeBold12)
                .Prop(Label.StylePropertyFontColor, GoldFortress),

            Element<Label>()
                .Class(StyleClassLabelSecondaryColor)
                .Prop(Label.StylePropertyFont, sourceCode12)
                .Prop(Label.StylePropertyFontColor, GraySilver),

            Element<Label>()
                .Class(StyleClassLabelBig)
                .Prop(Label.StylePropertyFont, sourceCode16),

            Element<Label>()
                .Class(StyleClassLabelSmall)
                .Prop(Label.StylePropertyFont, sourceCode10),

            Element<Label>()
                .Class(StyleClassLabelSmallest)
                .Prop(Label.StylePropertyFont, sourceCode8),

            Element<Label>()
                .Class("StatusFieldTitle")
                .Prop(Label.StylePropertyFontColor, GoldFortress),

            Element<Label>()
                .Class("Good")
                .Prop(Label.StylePropertyFontColor, GoodGreenFore),

            Element<Label>()
                .Class("Caution")
                .Prop(Label.StylePropertyFontColor, GoldFortress),

            Element<Label>()
                .Class("Danger")
                .Prop(Label.StylePropertyFontColor, LightBad),

            Element<Label>()
                .Class("Disabled")
                .Prop(Label.StylePropertyFontColor, DisabledButtonFontColor),

            #endregion

            // Big Button
            new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(Button), new[] { StyleClassButtonBig }, null, null),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFont, sourceCode16) }),

            #region Top Menu

            // Those top menu buttons.
            // these use slight variations on the various BaseButton styles so that the content within them appears centered,
            // which is NOT the case for the default BaseButton styles (OpenLeft/OpenRight adds extra padding on one of the sides
            // which makes the TopButton icons appear off-center, which we don't want).
            Element<MenuButton>()
                .Class(ButtonSquare)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonSquare),

            Element<MenuButton>()
                .Class(ButtonOpenLeft)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenLeft),

            Element<MenuButton>()
                .Class(ButtonOpenRight)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenRight),

            Element<MenuButton>()
                .Class(ButtonOpenBoth)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonOpenBoth),

            Element<MenuButton>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

            Element<MenuButton>()
                .Class(MenuButton.StyleClassRedTopButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<MenuButton>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

            Element<MenuButton>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

            Element<MenuButton>()
                .Class(MenuButton.StyleClassRedTopButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightBad),

            Element<Label>()
                .Class(MenuButton.StyleClassLabelTopButton)
                .Prop(Label.StylePropertyFont, sourceCodeBold14),

            #endregion

            // RimHeading
            new StyleRule(
                new SelectorChild(
                    SelectorElement.Type(typeof(NanoHeading)),
                    SelectorElement.Type(typeof(PanelContainer))),
                new[] { new StyleProperty(PanelContainer.StylePropertyPanel, rimHeadingBox), }),

            #region FancyBack

            Element<FancyBack>()
                .Prop(FancyBack.StylePropertyEdgeColor, GoldFortress)
                .Prop(ShaderPanelContainer.StylePropertyTexture, GetTex("stripeback.png"))
                .Prop(ShaderPanelContainer.StylePropertyShader, TileShader),

            Element<FancyBack>()
                .Class(StyleClassFancyBackNone)
                .Prop(StripeBack.StylePropertyBackground, Texture.Transparent),

            Element<FancyBack>()
                .Class(StyleClassFancyBackWooden)
                .Prop(ShaderPanelContainer.StylePropertyTexture, GetTex("wooden_back.png")),

            #endregion

            // StyleClassItemStatus
            Element()
                .Class(StyleClassItemStatus)
                .Prop("font", sourceCode10),

            Element()
                .Class(StyleClassItemStatusNotHeld)
                .Prop("font", sourceCodeItalic10)
                .Prop("font-color", GraySilver),

            Element<RichTextLabel>()
                .Class(StyleClassItemStatus)
                .Prop(nameof(RichTextLabel.LineHeightScale), 0.7f)
                .Prop(nameof(Control.Margin), new Thickness(0, 0, 0, -6)),

            #region Slider

            Element<Slider>()
                .Prop(Slider.StylePropertyBackground, sliderBackBox)
                .Prop(Slider.StylePropertyForeground, sliderForeBox)
                .Prop(Slider.StylePropertyGrabber, sliderGrabBox)
                .Prop(Slider.StylePropertyFill, sliderFillBox),

            Element<ColorableSlider>()
                .Prop(ColorableSlider.StylePropertyFillWhite, sliderFillWhite)
                .Prop(ColorableSlider.StylePropertyBackgroundWhite, sliderFillWhite),

            Element<Slider>()
                .Class(StyleClassSliderRed)
                .Prop(Slider.StylePropertyFill, sliderFillRed),

            Element<Slider>()
                .Class(StyleClassSliderGreen)
                .Prop(Slider.StylePropertyFill, sliderFillGreen),

            Element<Slider>()
                .Class(StyleClassSliderBlue)
                .Prop(Slider.StylePropertyFill, sliderFillBlue),

            Element<Slider>()
                .Class(StyleClassSliderWhite)
                .Prop(Slider.StylePropertyFill, sliderFillWhite),

            #endregion

            #region Chat Channel Selector

            // chat channel option selector
            Element<Button>()
                .Class(StyleClassChatChannelSelectorButton)
                .Prop(ContainerButton.StylePropertyStyleBox, chatChannelButton),

            // chat filter button
            Element<ContainerButton>()
                .Class(StyleClassChatFilterOptionButton)
                .Prop(ContainerButton.StylePropertyStyleBox, chatFilterButton),

            Element<ContainerButton>()
                .Class(StyleClassChatFilterOptionButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

            Element<ContainerButton>()
                .Class(StyleClassChatFilterOptionButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

            Element<ContainerButton>()
                .Class(StyleClassChatFilterOptionButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

            Element<ContainerButton>()
                .Class(StyleClassChatFilterOptionButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled),

            #endregion

            #region OptionButton

            Element<OptionButton>()
                .Prop(ContainerButton.StylePropertyStyleBox, button),

            Element<OptionButton>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

            Element<OptionButton>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

            Element<OptionButton>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

            Element<OptionButton>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled),

            Element<TextureRect>()
                .Class(OptionButton.StyleClassOptionTriangle)
                .Prop(TextureRect.StylePropertyTexture, textureInvertedTriangle),

            Element<Label>()
                .Class(OptionButton.StyleClassOptionButton)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

            Element<PanelContainer>()
                .Class(OptionButton.StyleClassOptionsBackground)
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(GraphiteBlack)),

            #endregion

            Element<TextureButton>()
                .Class(StyleClassButtonHelp)
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),

            #region Dividers

            Element<PanelContainer>()
                .Class(ClassLowDivider)
                .Prop(PanelContainer.StylePropertyPanel,
                    new StyleBoxFlat
                    {
                        BackgroundColor = GoldFortress,
                        ContentMarginLeftOverride = 2,
                        ContentMarginBottomOverride = 2,
                    }),

            Element<PanelContainer>()
                .Class(ClassHighDivider)
                .Prop(PanelContainer.StylePropertyPanel,
                    new StyleBoxFlat
                    {
                        BackgroundColor = GoldFortress,
                        ContentMarginLeftOverride = 3,
                        ContentMarginBottomOverride = 3,
                    }),

            Element<PanelContainer>()
                .Class(StyleClassLowDividerDark)
                .Prop(PanelContainer.StylePropertyPanel,
                    new StyleBoxFlat
                    {
                        BackgroundColor = BlackAmber,
                        ContentMarginLeftOverride = 2,
                        ContentMarginBottomOverride = 2,
                    }),

            Element<PanelContainer>()
                .Class(StyleClassHighDividerDark)
                .Prop(PanelContainer.StylePropertyPanel,
                    new StyleBoxFlat
                    {
                        BackgroundColor = BlackAmber,
                        ContentMarginLeftOverride = 3,
                        ContentMarginBottomOverride = 3,
                    }),

            #endregion

            #region PanelContainer

            Element<PanelContainer>()
                .Class(StyleClassPanelDark)
                .Prop(PanelContainer.StylePropertyPanel, darkPanel),
            Element<PanelContainer>()
                .Class(StyleClassPanelDarkTransparent)
                .Prop(PanelContainer.StylePropertyPanel, darkPanelTransparent),

            Element<PanelContainer>()
                .Class(StyleClassPanelLight)
                .Prop(PanelContainer.StylePropertyPanel, lightPanel),
            Element<PanelContainer>()
                .Class(StyleClassPanelLightTransparent)
                .Prop(PanelContainer.StylePropertyPanel, lightPanelTransparent),

            Element<PanelContainer>()
                .Class(StyleClassPanelDarkBordered)
                .Prop(PanelContainer.StylePropertyPanel, darkBorderedPanel),
            Element<PanelContainer>()
                .Class(StyleClassPanelDarkBorderedTransparent)
                .Prop(PanelContainer.StylePropertyPanel, darkBorderedPanelTransparent),

            Element<PanelContainer>()
                .Class(StyleClassPanelLightBordered)
                .Prop(PanelContainer.StylePropertyPanel, lightBorderedPanel),
            Element<PanelContainer>()
                .Class(StyleClassPanelLightBorderedTransparent)
                .Prop(PanelContainer.StylePropertyPanel, lightBorderedPanelTransparent),

            Element<PanelContainer>()
                .Class(StyleClassPanelHighlighted)
                .Prop(PanelContainer.StylePropertyPanel, highlightedPanel),
            Element<PanelContainer>()
                .Class(StyleClassPanelHighlightedTransparent)
                .Prop(PanelContainer.StylePropertyPanel, highlightedPanelTransparent),

            Element<PanelContainer>()
                .Class(ClassAngleRect)
                .Prop(PanelContainer.StylePropertyPanel, angleRect),

            Element<PanelContainer>()
                .Class(StyleClassPanelAngleRectTransparent)
                .Prop(PanelContainer.StylePropertyPanel, angleRectTransparent),

            Element<PanelContainer>()
                .Class(StyleClassTopInfoPanel)
                .Prop(PanelContainer.StylePropertyPanel, topInfoPanel),

            Element<PanelContainer>()
                .Class(StyleClassTopInfoCellPanel)
                .Prop(PanelContainer.StylePropertyPanel, topInfoPanelCell),

            Element<ChatMessagesBubble>()
                .Class(ChatMessagesBubble.StyleClassChatMessagesBubble)
                .Prop(PanelContainer.StylePropertyPanel, chatBubble),

            Element<PanelContainer>()
                .Class(StyleClassFoldableChatPanel)
                .Prop(PanelContainer.StylePropertyPanel, foldableChatPanel),

            #endregion

            // Window Footer
            Element<Label>()
                .Class("WindowFooterText")
                .Prop(Label.StylePropertyFont, sourceCode8)
                .Prop(Label.StylePropertyFontColor, PlaceholderFontColor),

            // X Texture button ---
            Element<TextureButton>()
                .Class(StyleClassCrossButtonRed)
                .Prop(TextureButton.StylePropertyTexture, GetTex("cross.png"))
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<TextureButton>()
                .Class(StyleClassCrossButtonRed)
                .Pseudo(TextureButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightBad),

            Element<TextureButton>()
                .Class(StyleClassCrossButtonRed)
                .Pseudo(TextureButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, DarkestBad),
            // ---

            // Profile Editor
            Element<TextureButton>()
                .Class("SpeciesInfoDefault")
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),

            Element<TextureButton>()
                .Class("SpeciesInfoWarning")
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/info.svg.192dpi.png"))
                .Prop(Control.StylePropertyModulateSelf, GoldFortress),

            // The default look of paper in UIs. Pages can have components which override this
            Element<PanelContainer>()
                .Class("PaperDefaultBorder")
                .Prop(PanelContainer.StylePropertyPanel, paperBackground),
            Element<RichTextLabel>()
                .Class("PaperWrittenText")
                .Prop(Label.StylePropertyFont, sourceCode12)
                .Prop(Control.StylePropertyModulateSelf, BlackAmber),

            Element<RichTextLabel>()
                .Class("LabelSubText")
                .Prop(Label.StylePropertyFont, sourceCode10)
                .Prop(Label.StylePropertyFontColor, GraphiteBlack),

            Element<LineEdit>()
                .Class("PaperLineEdit")
                .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty()),

            // Red Button ---
            Element<Button>()
                .Class(StyleClassButtonColorRed)
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<Button>()
                .Class(StyleClassButtonColorRed)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Bad),

            Element<Button>()
                .Class(StyleClassButtonColorRed)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightBad),
            // ---

            // Green Button ---
            Element<Button>()
                .Class(StyleClassButtonColorGreen)
                .Prop(Control.StylePropertyModulateSelf, Good),

            Element<Button>()
                .Class(StyleClassButtonColorGreen)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Good),

            Element<Button>()
                .Class(StyleClassButtonColorGreen)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightGood),

            // Accept button (merge with green button?) ---
            Element<Button>()
                .Class("ButtonAccept")
                .Prop(Control.StylePropertyModulateSelf, Good),

            Element<Button>()
                .Class("ButtonAccept")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Good),

            Element<Button>()
                .Class("ButtonAccept")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, LightGood),

            Element<Button>()
                .Class("ButtonAccept")
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, DarkestGood),
            // ---

            // Small Button ---
            /*
            Element<Button>()
                .Class("ButtonSmall")
                .Prop(ContainerButton.StylePropertyStyleBox, smallButtonBase),
            */

            Child()
                .Parent(Element<Button>().Class("ButtonSmall"))
                .Child(Element<Label>())
                .Prop(Label.StylePropertyFont, sourceCode8),
            // ---

            #region Radial menu

            // Radial menu buttons
            Element<TextureButton>()
                .Class("RadialMenuButton")
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Radial/button_normal.png")),

            Element<TextureButton>()
                .Class("RadialMenuButton")
                .Pseudo(TextureButton.StylePseudoClassHover)
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Radial/button_hover.png")),

            Element<TextureButton>()
                .Class("RadialMenuCloseButton")
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Radial/close_normal.png")),

            Element<TextureButton>()
                .Class("RadialMenuCloseButton")
                .Pseudo(TextureButton.StylePseudoClassHover)
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Radial/close_hover.png")),

            Element<TextureButton>()
                .Class("RadialMenuBackButton")
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Radial/back_normal.png")),

            Element<TextureButton>()
                .Class("RadialMenuBackButton")
                .Pseudo(TextureButton.StylePseudoClassHover)
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Radial/back_hover.png")),

            #endregion

            #region Fancy Tree

            Element<FancyTree>()
                .Prop(FancyTree.StylePropertyIconColor, GoldFortress)
                .Prop(FancyTree.StylePropertyLineColor, GoldFortress),

            Element<ContainerButton>()
                .Identifier(TreeItem.StyleIdentifierTreeButton)
                .Class(TreeItem.StyleClassEvenRow)
                .Prop(ContainerButton.StylePropertyStyleBox,
                    new StyleBoxFlat { BackgroundColor = GraphiteBlack }),

            Element<ContainerButton>()
                .Identifier(TreeItem.StyleIdentifierTreeButton)
                .Class(TreeItem.StyleClassOddRow)
                .Prop(ContainerButton.StylePropertyStyleBox,
                    new StyleBoxFlat { BackgroundColor = DarkForest }),

            Element<ContainerButton>()
                .Identifier(TreeItem.StyleIdentifierTreeButton)
                .Class(TreeItem.StyleClassSelected)
                .Prop(ContainerButton.StylePropertyStyleBox,
                    new StyleBoxFlat { BackgroundColor = BrightGreen }),

            Element<ContainerButton>()
                .Identifier(TreeItem.StyleIdentifierTreeButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox,
                    new StyleBoxFlat { BackgroundColor = BrightGreen }),

            #endregion

            // Pinned button style
            Element<TextureButton>()
                .Class(StyleClassPinButtonPinned)
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Bwoink/pinned.png")),

            // Unpinned button style
            Element<TextureButton>()
                .Class(StyleClassPinButtonUnpinned)
                .Prop(TextureButton.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Bwoink/un_pinned.png")),

            #region StyleNano legacy stuff

            Element<TextureRect>()
                .Class("NTLogoDark")
                .Prop(TextureRect.StylePropertyTexture,
                    resCache.GetTexture("/Textures/Interface/Nano/ntlogo.svg.png"))
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#757575")),

            // Silicon law edit ui
            Element<Label>()
                .Class(SiliconLawContainer.StyleClassSiliconLawPositionLabel)
                .Prop(Label.StylePropertyFontColor, GoldFortress),

            // hotbar slot
            Element<RichTextLabel>()
                .Class(StyleClassHotbarSlotNumber)
                .Prop(Label.StylePropertyFont, sourceCodeBold16),

            // inventory slot background
            Element()
                .Class(StyleClassInventorySlotBackground)
                .Prop(PanelContainer.StylePropertyPanel, invSlotBg),

            // hand slot highlight
            Element()
                .Class(StyleClassHandSlotHighlight)
                .Prop(PanelContainer.StylePropertyPanel, handSlotHighlight),

            // Hotbar background
            Element<PanelContainer>()
                .Class(StyleClassHotbarPanel)
                .Prop(PanelContainer.StylePropertyPanel, hotbarBackground),

            // ItemStatus for hands
            Element()
                .Class(StyleClassItemStatusNotHeld)
                .Prop("font", sourceCodeItalic10)
                .Prop("font-color", GraySilver)
                .Prop(nameof(Control.Margin), new Thickness(4, 0, 0, 2)),

            Element()
                .Class(StyleClassItemStatus)
                .Prop(nameof(RichTextLabel.LineHeightScale), 0.7f)
                .Prop(nameof(Control.Margin), new Thickness(4, 0, 0, 2)),

            // APC and SMES power state label colors
            Element<Label>()
                .Class(StyleClassPowerStateNone)
                .Prop(Label.StylePropertyFontColor, new Color(0.8f, 0.0f, 0.0f)),

            Element<Label>()
                .Class(StyleClassPowerStateLow)
                .Prop(Label.StylePropertyFontColor, new Color(0.9f, 0.36f, 0.0f)),

            Element<Label>()
                .Class(StyleClassPowerStateGood)
                .Prop(Label.StylePropertyFontColor, new Color(0.024f, 0.8f, 0.0f)),

            Element<PanelContainer>()
                .Class("BackgroundOpenRight")
                .Prop(PanelContainer.StylePropertyPanel, buttonOpenRight)
                .Prop(Control.StylePropertyModulateSelf, GraphiteBlack),
            Element<PanelContainer>()
                .Class("BackgroundOpenLeft")
                .Prop(PanelContainer.StylePropertyPanel, buttonOpenLeft)
                .Prop(Control.StylePropertyModulateSelf, GraphiteBlack),

            #region PDA

            //PDA - Backgrounds
            Element<PanelContainer>()
                .Class("PdaContentBackground")
                .Prop(PanelContainer.StylePropertyPanel, buttonOpenBoth)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#25252a")),

            Element<PanelContainer>()
                .Class("PdaBackground")
                .Prop(PanelContainer.StylePropertyPanel, buttonOpenBoth)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#000000")),

            Element<PanelContainer>()
                .Class("PdaBackgroundRect")
                .Prop(PanelContainer.StylePropertyPanel, BaseAngleRect)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#717059")),

            Element<PanelContainer>()
                .Class("PdaBorderRect")
                .Prop(PanelContainer.StylePropertyPanel, AngleBorderRect),

            Element<PanelContainer>()
                .Class("BackgroundDark")
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(Color.FromHex("#25252A"))),

            //PDA - Buttons
            Element<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.NormalBgColor))
                .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.EnabledFgColor)),

            Element<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.HoverColor))
                .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.EnabledFgColor)),

            Element<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.PressedColor))
                .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.EnabledFgColor)),

            Element<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.NormalBgColor))
                .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.DisabledFgColor)),

            Element<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(PdaProgramItem.StylePropertyBgColor, Color.FromHex(PdaProgramItem.NormalBgColor)),

            Element<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(PdaProgramItem.StylePropertyBgColor, Color.FromHex(PdaProgramItem.HoverColor)),

            Element<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(PdaProgramItem.StylePropertyBgColor, Color.FromHex(PdaProgramItem.HoverColor)),

            //PDA - Text
            Element<Label>()
                .Class("PdaContentFooterText")
                .Prop(Label.StylePropertyFont, sourceCode10)
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#757575")),

            Element<Label>()
                .Class("PdaWindowFooterText")
                .Prop(Label.StylePropertyFont, sourceCode10)
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#333d3b")),

            #endregion

            #endregion
        })
        .ToList());

        #endregion
    }
}

public static class ResCacheExtension
{
    public static VectorFont SourceCodeStack(
        this IResourceCache resCache,
        SourceCodeVariant variation = SourceCodeVariant.Regular,
        int size = 10)
    {
        return new VectorFont(resCache.GetResource<FontResource>($"/Fonts/SourceCodePro/SourceCodePro-{variation.ToString()}.ttf"), size);
    }
}

public enum SourceCodeVariant : byte
{
    Black,
    BlackItalic,
    Bold,
    BoldItalic,
    ExtraBold,
    ExtraBoldItalic,
    ExtraLight,
    ExtraLightItalic,
    Italic,
    Light,
    LightItalic,
    Medium,
    MediumItalic,
    Regular,
    SemiBold,
    SemiBoldItalic,
}
