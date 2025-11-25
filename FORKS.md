Fork Analysis Summary

## Other forks

Below is a curated analysis of all forks with commits ahead of kvakulo/Switcheroo. Forks are organized by feature categories to help identify interesting improvements.

### Modern Framework & Code Quality

- **@GrantByrne** [GrantByrne/Switcheroo](https://github.com/GrantByrne/Switcheroo) (11 commits)
  - **.NET 6 upgrade** with modern C# features (file-scoped namespaces)
  - Code cleanup, removed unused references, fixed all build warnings
  - HttpClient instead of deprecated WebClient
  - 📥 **Worth importing:** .NET 6 migration, code modernization

- **@MichiBaum** [MichiBaum/Switcheroo](https://github.com/MichiBaum/Switcheroo) (42 commits)
  - **.NET Core/6 migration** with dependency injection
  - Comprehensive code cleanup and modern practices
  - CI/CD with GitHub Actions
  - 📥 **Worth importing:** .NET Core migration approach

- **@r-Larch** [r-Larch/Switcheroo](https://github.com/r-Larch/Switcheroo) (19 commits)
  - **.NET 6**, based on elig0n's features
  - **Virtual desktop support** - shows only windows from current virtual desktop
  - **Dialog window fixes** - fixes issue with open/save dialogs not showing
  - Warmup window on start for faster first invocation
  - Alt+Tab hook improvements
  - 📥 **Worth importing:** Virtual desktop isolation, dialog fixes, warmup

### Themes & UI Improvements

- **@crar01** [crar01/Switcheroo](https://github.com/crar01/Switcheroo) (10 commits)
  - **System theme support** - Dark/Light/System modes with auto-switching
  - **Modern UI** - Rounded corners, blur effects, removed borders
  - .NET 4.8 upgrade
  - 📥 **Worth importing:** System theme auto-detection, modern UI styling

- **@dvygolov** [dvygolov/Switcheroo](https://github.com/dvygolov/Switcheroo) (5 commits)
  - Based on crar01's theme work
  - **Multi-monitor + DPI scaling fixes**
  - 📥 **Worth importing:** Multi-monitor DPI fixes

- **@byguid** [byguid/switcheroo](https://github.com/byguid/switcheroo) (3 commits)
  - Dark mode, .NET 4.8 upgrade

- **@koglerch13** [koglerch13/Switcheroo](https://github.com/koglerch13/Switcheroo) (1 commit)
  - Custom visual style

- **@hahv** [hahv/HaHV_Switcheroo](https://github.com/hahv/HaHV_Switcheroo) (1 commit)
  - Increased font size for better readability

### Major Feature Additions

- **@elig0n** [elig0n/Switcheroo](https://github.com/elig0n/Switcheroo) (98 commits) - 🌟 **Most feature-rich**
  - **Alt+1-9, Alt+0** for quick access to first 10 windows (numbered in bold)
  - **Alt+S** to toggle alphabetical sorting
  - **Alt+W** to export window list to JSON
  - **Single click** to switch windows
  - **Alt+J/K** for vim-style navigation (in addition to Up/Down)
  - **Home/End/PageUp/PageDown** support
  - **Context menu** with shortcuts (Alt+O for options)
  - **Tray icon click** opens search-enabled Switcheroo
  - **Speed improvements** for UWP apps
  - Dark/light themes
  - 📥 **Worth importing:** Numerical access, sorting, JSON export, navigation improvements

- **@daanzu** [daanzu/Switcheroo](https://github.com/daanzu/Switcheroo) (72 commits)
  - Most of elig0n's features (numbered entries, sorting, single click, navigation)
  - **Topmost windows moved to bottom** of list
  - **Wider window** for better visibility
  - 📥 **Worth importing:** Similar to elig0n

- **@jsonMartin** [jsonMartin/Switcheroo](https://github.com/jsonMartin/Switcheroo) (13 commits)
  - Based on daanzu + crar01 themes
  - **Precache data on startup** for faster first invocation
  - **Font size adjustments** for better fit
  - Consistent search bar width
  - 📥 **Worth importing:** Performance improvements via precaching

- **@tversteeg** [tversteeg/Switcheroo](https://github.com/tversteeg/Switcheroo) (12 commits)
  - Collection of elig0n's features: numerical access, single click, Alt+nav, speed improvements

### Virtual Desktop & Window Management

- **@bakus522** [bakus522/Switcheroo](https://github.com/bakus522/Switcheroo) (4 commits)
  - **Virtual desktop isolation** - show only windows from current virtual desktop
  - **"First window only" mode** - show only one window per process
  - **Alt+`** (grave/tick) to switch between windows of same process
  - .NET upgrade
  - 📥 **Worth importing:** Virtual desktop support, first-window-only mode, Alt+` switching

- **@cutecycle** [cutecycle/Switcheroo](https://github.com/cutecycle/Switcheroo) (8 commits)
  - **Ctrl+Shift+W** to close all windows of selected process
  - Concurrent window closing for better performance
  - 📥 **Worth importing:** Bulk window closer

### Browser Integration

- **@boisenme** [boisenme/Switcheroo](https://github.com/boisenme/Switcheroo) (1 commit)
  - **Chrome tab selection** support
  - 📥 **Worth importing:** If Chrome tab switching is desired

### Keyboard & Shortcuts

- **@insertt** [insertt/Switcheroo](https://github.com/insertt/Switcheroo) (2 commits)
  - **Instant release switch** option
  - **Maximum query result count** option
  - 📥 **Worth importing:** Result limiting for performance

- **@raymond-w-ko** [raymond-w-ko/Switcheroo](https://github.com/raymond-w-ko/Switcheroo) (7 commits)
  - **Quick key indicators** displayed in UI
  - Additional hotkeys
  - Faster window switching method
  - Font size tweaks

- **@ryuslash** [ryuslash/Switcheroo](https://github.com/ryuslash/Switcheroo) (7 commits)
  - **Emacs keybindings** (C-g to close, etc.)
  - CI/CD setup

- **@georgeyu** [georgeyu/Switcheroo](https://github.com/georgeyu/Switcheroo) (1 commit)
  - **Right Shift key** support in shift key checks
  - 📥 **Worth importing:** Minor keyboard fix

### Internationalization

- **@advx9600** [advx9600/Switcheroo](https://github.com/advx9600/Switcheroo) (11 commits) - Chinese
  - **Numpad 1-9** support for window selection
  - **Space+{key}** to open new window
  - Custom keybindings and Chinese interface
  - Ignore window feature

- **@valuex** [valuex/Switcheroo](https://github.com/valuex/Switcheroo) (6 commits) - Chinese
  - **Chinese character search** support (with NPinyin.dll)
  - **INI file configuration** for predefined abbreviations
  - **Hotkey for same-process window switching**
  - 📥 **Worth importing:** Chinese search, INI config system

- **@joonofafa** [joonofafa/Switcheroo](https://github.com/joonofafa/Switcheroo) (11 commits) - Korean
  - **Launcher functionality** with recent programs (top 3)
  - **Async icon loading** with caching
  - Icon path and index management
  - UI improvements for launcher mode

- **@MuffinK** [MuffinK/Switcheroo](https://github.com/MuffinK/Switcheroo) (3 commits) - Chinese
  - Modified shortcuts

- **@WizaXxX** [WizaXxX/Switcheroo_1C](https://github.com/WizaXxX/Switcheroo_1C) (6 commits)
  - Russian/1C-specific customizations

### Integration & Special Use Cases

- **@lances101** [lances101/Switcheroo-Edited-For-Wox](https://github.com/lances101/Switcheroo-Edited-For-Wox) (3 commits)
  - **Wox launcher integration**
  - Memory cache workarounds
  - Downgraded to .NET 3.5 for compatibility

### Minor Forks (Based on major forks above)

- **@Celend**, **@fc1943s**, **@rawbeans**, **@windedge**, **@meixger** - Based on elig0n
- **@nqbao1234** - Based on daanzu
- **@yuriiwanchev**, **@Jijjy**, **@schMarXman**, **@sohaibz-leaders**, **@szym1991**, **@trond-snekvik** - Minor variations

### 🎯 Recommended Forks to Review for Import

1. **r-Larch** - .NET 6 + virtual desktop support + dialog fixes
2. **elig0n** - Most comprehensive feature set (numerical access, sorting, export, navigation)
3. **crar01/dvygolov** - Modern UI with system theme + DPI fixes
4. **bakus522** - Virtual desktop + first-window-only + Alt+` switching
5. **jsonMartin** - Performance via precaching
6. **valuex** - Chinese search + INI config system
7. **GrantByrne/MichiBaum** - .NET 6 migration approaches

=====================
Generated on: Sun Nov  9 19:24:47 CET 2025
Original repo: kvakulo/Switcheroo

Forks analyzed: 37

Files generated:
----------------
- advx9600/Switcheroo (Ahead: 11, Behind: 0)
  Commits analyzed: 11
  Files: advx9600_Switcheroo_*

- bakus522/Switcheroo (Ahead: 4, Behind: 0)
  Commits analyzed: 4
  Files: bakus522_Switcheroo_*

- boisenme/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: boisenme_Switcheroo_*

- byguid/switcheroo (Ahead: 3, Behind: 0)
  Commits analyzed: 3
  Files: byguid_switcheroo_*

- crar01/Switcheroo (Ahead: 10, Behind: 0)
  Commits analyzed: 10
  Files: crar01_Switcheroo_*

- dvygolov/Switcheroo (Ahead: 5, Behind: 0)
  Commits analyzed: 15
  Files: dvygolov_Switcheroo_*

- cutecycle/Switcheroo (Ahead: 8, Behind: 28)
  Commits analyzed: 8
  Files: cutecycle_Switcheroo_*

- daanzu/Switcheroo (Ahead: 72, Behind: 0)
  Commits analyzed: 72
  Files: daanzu_Switcheroo_*

- jsonMartin/Switcheroo (Ahead: 13, Behind: 0)
  Commits analyzed: 85
  Files: jsonMartin_Switcheroo_*

- nqbao1234/Switcheroo (Ahead: 3, Behind: 0)
  Commits analyzed: 88
  Files: nqbao1234_Switcheroo_*

- elig0n/Switcheroo (Ahead: 98, Behind: 0)
  Commits analyzed: 98
  Files: elig0n_Switcheroo_*

- Celend/Switcheroo (Ahead: 3, Behind: 0)
  Commits analyzed: 101
  Files: Celend_Switcheroo_*

- fc1943s/Switcheroo (Ahead: 22, Behind: 3)
  Commits analyzed: 117
  Files: fc1943s_Switcheroo_*

- rawbeans/Switcheroo (Ahead: 4, Behind: 0)
  Commits analyzed: 102
  Files: rawbeans_Switcheroo_*

- windedge/Switcheroo (Ahead: 6, Behind: 98)
  Commits analyzed: 6
  Files: windedge_Switcheroo_*

- meixger/Switcheroo (Ahead: 104, Behind: 6)
  Commits analyzed: 104
  Files: meixger_Switcheroo_*

- r-Larch/Switcheroo (Ahead: 19, Behind: 0)
  Commits analyzed: 123
  Files: r-Larch_Switcheroo_*

- yuriiwanchev/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 99
  Files: yuriiwanchev_Switcheroo_*

- georgeyu/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: georgeyu_Switcheroo_*

- GrantByrne/Switcheroo (Ahead: 11, Behind: 0)
  Commits analyzed: 11
  Files: GrantByrne_Switcheroo_*

- hahv/HaHV_Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: hahv_HaHV_Switcheroo_*

- insertt/Switcheroo (Ahead: 2, Behind: 0)
  Commits analyzed: 2
  Files: insertt_Switcheroo_*

- Jijjy/Switcheroo (Ahead: 15, Behind: 19)
  Commits analyzed: 15
  Files: Jijjy_Switcheroo_*

- joonofafa/Switcheroo (Ahead: 11, Behind: 0)
  Commits analyzed: 11
  Files: joonofafa_Switcheroo_*

- koglerch13/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: koglerch13_Switcheroo_*

- lances101/Switcheroo-Edited-For-Wox (Ahead: 3, Behind: 19)
  Commits analyzed: 3
  Files: lances101_Switcheroo-Edited-For-Wox_*

- MichiBaum/Switcheroo (Ahead: 42, Behind: 0)
  Commits analyzed: 42
  Files: MichiBaum_Switcheroo_*

- MuffinK/Switcheroo (Ahead: 3, Behind: 0)
  Commits analyzed: 3
  Files: MuffinK_Switcheroo_*

- raymond-w-ko/Switcheroo (Ahead: 7, Behind: 0)
  Commits analyzed: 7
  Files: raymond-w-ko_Switcheroo_*

- ryuslash/Switcheroo (Ahead: 7, Behind: 0)
  Commits analyzed: 7
  Files: ryuslash_Switcheroo_*

- schMarXman/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: schMarXman_Switcheroo_*

- sohaibz-leaders/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: sohaibz-leaders_Switcheroo_*

- szym1991/Switcheroo (Ahead: 1, Behind: 0)
  Commits analyzed: 1
  Files: szym1991_Switcheroo_*

- trond-snekvik/Switcheroo (Ahead: 1, Behind: 19)
  Commits analyzed: 1
  Files: trond-snekvik_Switcheroo_*

- tversteeg/Switcheroo (Ahead: 12, Behind: 0)
  Commits analyzed: 12
  Files: tversteeg_Switcheroo_*

- valuex/Switcheroo (Ahead: 6, Behind: 0)
  Commits analyzed: 6
  Files: valuex_Switcheroo_*

- WizaXxX/Switcheroo_1C (Ahead: 6, Behind: 0)
  Commits analyzed: 6
  Files: WizaXxX_Switcheroo_1C_*

