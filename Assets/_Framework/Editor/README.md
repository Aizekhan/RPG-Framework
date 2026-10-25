# RPG Framework - Unity Editor Tools

Unity Inspector views and Editor Tools for the RPG Framework, providing an easy-to-use interface for Unity developers.

## Overview

This directory contains comprehensive Unity Editor extensions for the RPG Framework, including:
- Custom Editors for Framework data types
- Editor Windows for visual editing
- Property Drawers for enhanced Inspector display
- Gizmos for Scene view visualization
- Utility scripts and menu items

## Directory Structure

```
Assets/_Framework/Editor/
├── RPGFramework.Editor.asmdef      # Assembly definition (Editor-only)
├── PropertyDrawers/                # Custom property drawers
├── CustomEditors/                  # Custom Inspector editors
├── EditorWindows/                  # Editor window tools
├── Gizmos/                         # Scene view gizmos
└── Utilities/                      # Utility scripts and menu items
```

## Files Created

### Assembly Definition
- **RPGFramework.Editor.asmdef** - Editor assembly with references to all Framework runtime assemblies

### Property Drawers (4 files)
- **QuestIdDrawer.cs** - Validation and UI hints for quest IDs
- **ItemRarityDrawer.cs** - Color-coded display for item rarity
- **FactionStandingDrawer.cs** - Color-coded display for faction standing
- **ProgressBarDrawer.cs** - Utility for drawing progress bars

### Custom Editors (9 files)
1. **QuestDataEditor.cs** - Quest configuration with objectives and rewards
2. **DialogueDataEditor.cs** - Dialogue tree visualization and validation
3. **CraftingRecipeDataEditor.cs** - Recipe visualization with materials
4. **FactionDataEditor.cs** - Faction data with reputation bars
5. **ItemDataEditor.cs** - Item properties with visual indicators
6. **AbilityDataEditor.cs** - Ability configuration with cooldowns
7. **SkillDataEditor.cs** - Skill progression visualization
8. **StatusEffectDataEditor.cs** - Status effect modifiers
9. **LootTableDataEditor.cs** - Loot drop chances and weights

### Editor Windows (6 files)
1. **FrameworkManagerWindow.cs** - Central hub for all Framework tools
2. **QuestEditorWindow.cs** - Visual quest creation and editing
3. **DialogueGraphEditorWindow.cs** - Node-based dialogue editor
4. **ItemDatabaseWindow.cs** - Browse and manage items
5. **FactionRelationshipMapWindow.cs** - Faction relationships viewer
6. **LootTableEditorWindow.cs** - Loot table editor with drop visualization

### Gizmos (1 file)
- **FrameworkGizmos.cs** - Scene view visualization utilities
  - Quest markers
  - Interaction radius
  - Loot spawn points
  - Crafting stations
  - Dialogue triggers
  - Faction territories
  - Combat ranges
  - Waypoint paths
  - Trigger zones
  - Ability cast ranges

### Utilities (3 files)
1. **FrameworkMenuItems.cs** - Menu items under "Tools/RPG Framework/"
2. **FrameworkValidation.cs** - Data integrity validation
3. **FrameworkEditorUtility.cs** - General editor utility methods

## Menu Items

Access all tools from **Tools > RPG Framework** menu:

### Main Tools
- **Framework Manager** (Ctrl+Shift+F) - Central hub window
- Quest Editor
- Dialogue Graph Editor
- Item Database
- Faction Relationship Map
- Loot Table Editor

### Utilities
- Validate Framework Data
- Open Framework Folder
- Open Framework Documentation

### Quick Create
- Create Quest Data
- Create Dialogue Data
- Create Faction Data
- Create Crafting Recipe
- Create Status Effect

### Help
- About RPG Framework
- Report Issue

## Key Features

### Visual Editors
- Color-coded rarity and standing indicators
- Progress bars for stats and progression
- Interactive lists and arrays
- Validation with error/warning messages
- Help boxes with documentation

### Custom Property Drawers
- Enhanced display for enums (ItemRarity, FactionStanding)
- Visual validation for IDs and references
- Color-coded backgrounds for better visibility

### Editor Windows
- Multi-panel layouts for browsing and editing
- Search and filter functionality
- Real-time validation
- Quick actions (create, duplicate, delete)

### Gizmos
- Visual markers in Scene view
- Interaction radius display
- Territory and zone visualization
- Path and waypoint display

### Validation Tools
- Check for missing IDs
- Validate references and dependencies
- Detect duplicate entries
- Report configuration issues

## Usage Examples

### Opening Framework Manager
```csharp
// From menu: Tools > RPG Framework > Framework Manager
// Or keyboard shortcut: Ctrl+Shift+F
```

### Creating a Quest
1. Tools > RPG Framework > Quest Editor
2. Click "Create New Quest"
3. Fill in quest details in the Inspector
4. Add objectives and rewards
5. Save

### Validating Framework Data
```csharp
// From menu: Tools > RPG Framework > Utilities > Validate Framework Data
// Check Console for validation results
```

### Using Gizmos
```csharp
using RPGFramework.Editor.Gizmos;

void OnDrawGizmos()
{
    FrameworkGizmos.DrawQuestMarker(transform.position, Color.yellow);
    FrameworkGizmos.DrawInteractionRadius(transform.position, 5f, Color.green);
}
```

## Design Principles

1. **Framework-Only** - No game-specific logic, only Framework systems
2. **Editor-Only** - All scripts use UnityEditor namespace, not included in builds
3. **User-Friendly** - Clear labels, tooltips, and help boxes
4. **Visual Clarity** - Color coding, progress bars, and icons
5. **Validation** - Real-time checks for data integrity
6. **Performance** - Efficient rendering and data loading

## Statistics

- **Total Files**: 23 C# scripts
- **Property Drawers**: 4
- **Custom Editors**: 9
- **Editor Windows**: 6
- **Gizmos**: 10+ types
- **Menu Items**: 15+
- **Utility Methods**: 20+

## Future Enhancements

Potential additions for future development:
- Achievement Browser window
- Save File Inspector window
- Network Stats Viewer
- Housing Layout Preview
- Companion Manager
- Weather System Preview
- Dynamic Event Editor
- Guild System Editor
- Advanced Dialogue Graph with node-based editing
- Item Creation Wizard
- Quest Template System

## Dependencies

This Editor assembly references:
- All RPG Framework Runtime assemblies
- UnityEditor
- UnityEngine

## Notes

- All scripts are in the `RPGFramework.Editor` namespace
- Editor scripts are automatically excluded from builds
- Custom editors use `[CustomEditor]` attribute
- Menu items use `[MenuItem]` attribute
- Gizmos use Unity's Gizmos and Handles APIs

## Support

For issues or questions:
1. Check Framework documentation
2. Review validation results
3. Check Unity Console for errors
4. Use "Tools > RPG Framework > Help > Report Issue"

---

**RPG Framework Editor Tools v1.0**
Easy-to-use Unity Inspector views for Framework developers
