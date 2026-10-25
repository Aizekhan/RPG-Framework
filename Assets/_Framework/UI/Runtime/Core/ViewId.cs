namespace RPGFramework.UI
{
    /// <summary>
    /// Unique identifiers for UI views.
    /// Used to register, show, and hide specific views.
    /// </summary>
    public enum ViewId
    {
        None = 0,

        // Character & Stats
        CharacterPanel = 100,
        StatsPanel = 101,
        SkillsPanel = 102,
        AbilitiesPanel = 103,

        // Inventory & Items
        InventoryPanel = 200,
        EquipmentPanel = 201,
        LootPanel = 202,
        ShopPanel = 203,

        // Quests & Dialogue
        QuestLogPanel = 300,
        QuestTrackerPanel = 301,
        DialoguePanel = 302,

        // Combat & HUD
        HealthBarHUD = 400,
        ManaBarHUD = 401,
        AbilityBarHUD = 402,
        BuffsDebuffsHUD = 403,

        // Social & Progression
        LevelUpNotification = 500,
        AchievementNotification = 501,
        GuildPanel = 502,
        TradingPanel = 503,

        // Crafting & Housing
        CraftingPanel = 600,
        HousingPanel = 601,

        // Miscellaneous
        SettingsPanel = 700,
        MapPanel = 701,
        JournalPanel = 702,

        // Custom/Game-specific panels start from 1000
        CustomPanelStart = 1000
    }
}
