using Microsoft.Xna.Framework;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Central palette for the redesigned Crafting UI (deep navy mock).
    /// Keeping colors in one place makes it easier to tune the whole panel
    /// without chasing inline `new Color(...)` literals scattered across elements.
    /// </summary>
    internal static class UIPalette
    {
        // Window + panels
        public static readonly Color WindowBorder = new(74, 80, 144);
        public static readonly Color PanelBg = new(28, 30, 54, 195);
        public static readonly Color CatColumnBg = new(22, 24, 46, 160);
        public static readonly Color GridColumnBg = new(20, 22, 44, 150);
        public static readonly Color RecipeColumnBg = new(18, 20, 40, 155);

        // Titlebar
        public static readonly Color TitleBarBg = new(42, 48, 90, 210);
        public static readonly Color TitleBarAccent = new(120, 140, 220, 128);
        public static readonly Color TitleBarStatusBg = new(20, 25, 50, 178);
        public static readonly Color TitleBarStatusBorder = new(58, 64, 112);
        public static readonly Color TitleBarStatusText = new(128, 160, 208);

        // Dividers
        public static readonly Color Divider = new(58, 64, 128);

        // Category rows
        public static readonly Color CatRowLabel = new(112, 128, 176);
        public static readonly Color CatRowLabelActive = new(192, 208, 240);
        public static readonly Color CatRowBadge = new(64, 80, 160);
        public static readonly Color CatRowBadgeActive = new(96, 112, 192);
        public static readonly Color CatRowActiveBg = new(50, 80, 160, 128);
        public static readonly Color CatRowHoverBg = new(60, 70, 120, 102);
        public static readonly Color CatRowBorder = new(58, 64, 112);
        public static readonly Color CatRowBorderActive = new(80, 112, 192);
        public static readonly Color CatRowAccent = new(80, 112, 192);
        public static readonly Color CatCheckBg = new(20, 22, 44, 204);
        public static readonly Color CatCheckBorder = new(64, 80, 160);
        public static readonly Color CatCheckActiveBg = new(64, 96, 176);
        public static readonly Color CatCheckActiveBorder = new(112, 144, 224);
        public static readonly Color CatCheckInner = new(160, 192, 255);

        // Search box
        public static readonly Color SearchBg = new(14, 16, 34, 230);
        public static readonly Color SearchBorder = new(58, 64, 112);
        public static readonly Color SearchBorderFocused = new(80, 112, 192);
        public static readonly Color SearchPlaceholder = new(74, 82, 128);
        public static readonly Color SearchText = new(176, 192, 224);

        // Item grid cells
        public static readonly Color CellBg = new(24, 28, 54, 230);
        public static readonly Color CellBgHover = new(36, 46, 96, 230);
        public static readonly Color CellBgSelected = new(40, 72, 160, 204);
        public static readonly Color CellBorder = new(42, 48, 96);
        public static readonly Color CellBorderHover = new(96, 112, 176);
        public static readonly Color CellBorderSelected = new(96, 144, 224);
        public static readonly Color CellNameText = new(96, 112, 160);
        public static readonly Color CellNameTextSelected = new(160, 184, 232);

        // Pagination
        public static readonly Color PageBtnBg = new(30, 35, 70, 230);
        public static readonly Color PageBtnBgHover = new(50, 60, 120, 230);
        public static readonly Color PageBtnBorder = new(58, 64, 112);
        public static readonly Color PageBtnBorderHover = new(80, 96, 160);
        public static readonly Color PageBtnArrow = new(96, 112, 160);
        public static readonly Color PageBtnArrowHover = new(160, 176, 224);
        public static readonly Color PageText = new(80, 96, 160);

        // Stock indicator
        public static readonly Color StockOk = new(80, 192, 112);
        public static readonly Color StockWarn = new(208, 160, 48);
        public static readonly Color StockBad = new(192, 80, 80);

        // Ingredient rows
        public static readonly Color IngRowBg = new(14, 16, 34, 128);
        public static readonly Color IngRowSeparator = new(30, 35, 70, 128);
        public static readonly Color IngIconBg = new(22, 25, 50, 204);
        public static readonly Color IngIconBorder = new(42, 48, 96);
        public static readonly Color IngName = new(128, 144, 184);
        public static readonly Color IngSeparator = new(58, 64, 112);
        public static readonly Color IngNeed = new(74, 80, 128);

        // Chips
        public static readonly Color ChipCraftableBg = new(20, 70, 30, 128);
        public static readonly Color ChipCraftableBorder = new(40, 140, 60, 128);
        public static readonly Color ChipCraftableText = new(80, 192, 112);

        public static readonly Color ChipMissingBg = new(70, 20, 20, 128);
        public static readonly Color ChipMissingBorder = new(140, 40, 40, 128);
        public static readonly Color ChipMissingText = new(192, 80, 80);

        public static readonly Color ChipOwnedBg = new(20, 40, 70, 128);
        public static readonly Color ChipOwnedBorder = new(40, 90, 160, 128);
        public static readonly Color ChipOwnedText = new(120, 168, 232);

        // Tree station badge
        public static readonly Color StationBg = new(20, 25, 55, 204);
        public static readonly Color StationBorder = new(42, 53, 101);
        public static readonly Color StationHoverBg = new(40, 56, 104, 222);
        public static readonly Color StationText = new(112, 136, 184);

        // Recipe tree rows
        public static readonly Color TreeIconBg = new(25, 28, 58, 204);
        public static readonly Color TreeIconBorder = new(42, 48, 96);
        public static readonly Color TreeArrow = new(64, 80, 160);
        public static readonly Color TreeArrowHover = new(160, 176, 224);
        public static readonly Color TreeOwnedCount = new(120, 128, 160);
        public static readonly Color TreeConnector = new(46, 58, 120);
    }
}
