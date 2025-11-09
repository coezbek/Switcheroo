<img src="logo.png" alt="Switcheroo" width="48px" height="48px"> Switcheroo  [![Build Status](https://img.shields.io/appveyor/ci/kvakulo/switcheroo.svg)](https://ci.appveyor.com/project/kvakulo/switcheroo) ![GPL License](https://img.shields.io/badge/license-GPL-brightgreen.svg)
==========

Switcheroo is for anyone who spends more time using a keyboard than a mouse.
Instead of alt-tabbing through a (long) list of open windows, Switcheroo allows
you to quickly switch to any window by typing in just a few characters of its title.

## Screenshot

<img src="screenshot.png" alt="Screenshot of Switcheroo in action" width="540px" height="372px">


## Download

**[Download Switcheroo here](https://github.com/coezbek/Switcheroo/releases)**


## Usage

Action                         | Shortcut          | Remarks
------------------------------ | ----------------- | ----------
Activate Switcheroo            | `Alt + Space`     | This shortcut can be customized in _Options_
Activate Switcheroo            | `Alt + Tab`       | Only works if enabled under _Options_
_When Switcheroo is open_      |                   |
Switch to selected window      | `Enter`           | Or release `Alt` if opened with `Alt + Tab` and `_altTabAutoSwitch` is enabled in _Options_. Note: independently from `_altTabAutoSwitch`, pressing and release `Alt` will select the currently highlighted window and switch to it.
Close selected window          | `Alt or Ctrl + W` |
Dismiss Switcheroo             | `Esc or Alt + Q`  |
Up and down in list            | `Up` / `Down`     | Also works with `Alt + J` / `Alt + K` and when holding `Alt`. Also works with mouse wheel.
Navigate columns               | `Left` / `Right`  | Also works when holding `Alt`. `Alt+Shift+Wheel` to scroll columns.
Cycle most common apps         | `Alt + ~`         |
Keep Switcheroo open           | `Ctrl`            | Hold while releasing `Alt` to keep Switcheroo open if in `_altTabAutoSwitch` mode.

### Mouse Usage

- Double click a window to switch to it. (Configurable in _Options_ if single click is also possible.)
- Mouse wheel scrolls through the current active list.
- `Shift + Mouse Wheel` scrolls through columns.
- `Shift + Click` and `Ctrl + Click` select windows for multi-selection (for closing multiple windows at once).

## Column Mode

Switcheroo displays windows in five columns:

- The center column shows the 10 most recently used windows and all windows not shown in other columns.
- The three columns to the left show windows from the three most common applications.
- The rightmost column shows pinned windows (defined in _Options_).

## TODO
- [x] Ensure the 10 most recent windows remain in the center column, even if duplicated in other columns.
- [x] Implement a fixed, identical width for all columns.
- [x] Center the entire window so the middle column is perfectly centered on the screen.
- [x] When less than 5 columns are necessary, the central column must still remain on the same exact spot.
- [x] Use mouse-wheel to scroll through the current active list.
- [x] During search results are shown in the center column, while the other columns stay visible.
- [x] Automatically remove common suffixes (e.g., " - Google Chrome") if they appear in more than half of all open windows of the same process.
- [x] Fix navigation with `Alt + Left`/`Right` arrow keys.
- [x] Resolve focus and selection issues when using the mouse to select an item.
- [x] After pressing Alt+W the focus must be moved to the next item in the list.
- [x] Make it user configurable if single or double click is needed to switch to a window.
- [x] Holding Ctrl while releasing Alt in `_altTabAutoSwitch` mode keeps Switcheroo open.
- [ ] Make pinned windows configurable from the UI / Settings.
- [ ] Allow to highlight some of the windows based on regex rules.
- [x] For left/app columns allow Alt+Shift+W to close all windows in that column.
- [ ] Add right click menu to windows for "Close", "Pin/Unpin", "Bring to front", "Open File Location", "Copy Window Title".
- [x] Add Right Shift key support in shift key checks - from fork georgeyu/Switcheroo

## Forks

Relative to [@kvakulo kvakulo / Switcheroo](https://github.com/kvakulo/Switcheroo)

*   [@advx9600 advx9600 / Switcheroo](https://github.com/advx9600/Switcheroo) - Ahead: 11, Behind: 0
*   [@bakus522 bakus522 / Switcheroo](https://github.com/bakus522/Switcheroo) - Ahead: 4, Behind: 0
*   [@boisenme boisenme / Switcheroo](https://github.com/boisenme/Switcheroo) - Ahead: 1, Behind: 0
*   [@byguid byguid / switcheroo](https://github.com/byguid/switcheroo) - Ahead: 3, Behind: 0
*   [@crar01 crar01 / Switcheroo](https://github.com/crar01/Switcheroo) - Ahead: 10, Behind: 0
    *   [@dvygolov dvygolov / Switcheroo](https://github.com/dvygolov/Switcheroo) - Ahead: 5, Behind: 0
*   [@cutecycle cutecycle / Switcheroo](https://github.com/cutecycle/Switcheroo) - Ahead: 8, Behind: 28
*   [@daanzu daanzu / Switcheroo](https://github.com/daanzu/Switcheroo) - Ahead: 72, Behind: 0
    *   [@jsonMartin jsonMartin / Switcheroo](https://github.com/jsonMartin/Switcheroo) - Ahead: 13, Behind: 0
        *   [@nqbao1234 nqbao1234 / Switcheroo](https://github.com/nqbao1234/Switcheroo) - Ahead: 3, Behind: 0
*   [@elig0n elig0n / Switcheroo](https://github.com/elig0n/Switcheroo) - Ahead: 98, Behind: 0
    *   [@Celend Celend / Switcheroo](https://github.com/Celend/Switcheroo) - Ahead: 3, Behind: 0
    *   [@fc1943s fc1943s / Switcheroo](https://github.com/fc1943s/Switcheroo) - Ahead: 22, Behind: 3
    *   [@rawbeans rawbeans / Switcheroo](https://github.com/rawbeans/Switcheroo) - Ahead: 4, Behind: 0
    *   [@windedge windedge / Switcheroo](https://github.com/windedge/Switcheroo) - Ahead: 6, Behind: 98
        *   [@meixger meixger / Switcheroo](https://github.com/meixger/Switcheroo) - Ahead: 104, Behind: 6
        *   [@r-Larch r-Larch / Switcheroo](https://github.com/r-Larch/Switcheroo) - Ahead: 19, Behind: 0
    *   [@yuriiwanchev yuriiwanchev / Switcheroo](https://github.com/yuriiwanchev/Switcheroo) - Ahead: 1, Behind: 0
* [x] [@georgeyu georgeyu / Switcheroo](https://github.com/georgeyu/Switcheroo) - Ahead: 1, Behind: 0
*   [@GrantByrne GrantByrne / Switcheroo](https://github.com/GrantByrne/Switcheroo) - Ahead: 11, Behind: 0
*   [@hahv hahv / HaHV_Switcheroo](https://github.com/hahv/HaHV_Switcheroo) - Ahead: 1, Behind: 0
*   [@insertt insertt / Switcheroo](https://github.com/insertt/Switcheroo) - Ahead: 2, Behind: 0
*   [@Jijjy Jijjy / Switcheroo](https://github.com/Jijjy/Switcheroo) - Ahead: 15, Behind: 19
*   [@joonofafa joonofafa / Switcheroo](https://github.com/joonofafa/Switcheroo) - Ahead: 11, Behind: 0
*   [@koglerch13 koglerch13 / Switcheroo](https://github.com/koglerch13/Switcheroo) - Ahead: 1, Behind: 0
*   [@lances101 lances101 / Switcheroo-Edited-For-Wox](https://github.com/lances101/Switcheroo-Edited-For-Wox) - Ahead: 3, Behind: 19
*   [@MichiBaum MichiBaum / Switcheroo](https://github.com/MichiBaum/Switcheroo) - Ahead: 42, Behind: 0
*   [@MuffinK MuffinK / Switcheroo](https://github.com/MuffinK/Switcheroo) - Ahead: 3, Behind: 0
*   [@raymond-w-ko raymond-w-ko / Switcheroo](https://github.com/raymond-w-ko/Switcheroo) - Ahead: 7, Behind: 0
*   [@ryuslash ryuslash / Switcheroo](https://github.com/ryuslash/Switcheroo) - Ahead: 7, Behind: 0
*   [@schMarXman schMarXman / Switcheroo](https://github.com/schMarXman/Switcheroo) - Ahead: 1, Behind: 0
*   [@sohaibz-leaders sohaibz-leaders / Switcheroo](https://github.com/sohaibz-leaders/Switcheroo) - Ahead: 1, Behind: 0
*   [@szym1991 szym1991 / Switcheroo](https://github.com/szym1991/Switcheroo) - Ahead: 1, Behind: 0
*   [@trond-snekvik trond-snekvik / Switcheroo](https://github.com/trond-snekvik/Switcheroo) - Ahead: 1, Behind: 19
*   [@tversteeg tversteeg / Switcheroo](https://github.com/tversteeg/Switcheroo) - Ahead: 12, Behind: 0
*   [@valuex valuex / Switcheroo](https://github.com/valuex/Switcheroo) - Ahead: 6, Behind: 0
*   [@WizaXxX WizaXxX / Switcheroo_1C](https://github.com/WizaXxX/Switcheroo_1C) - Ahead: 6, Behind: 0

## History

Switcheroo was originally developed by [James Sulak](https://github.com/jsulak).

[Regin Larsen](https://github.com/kvakulo) took over the project in 2014 and is the current maintainer.

This fork is maintained by [Christopher Özbek](https://github.com/coezbek).

## Other projects

- [Alt-Tab Terminator]() - Commercial alt-tab replacement with window previews.
- https://github.com/hdlx/AltAppSwitcher - If you want Alt+Tab to be like MacOS's app switcher.


How to contribute
-----------------

Please report any bug you encounter by [submitting an issue](https://github.com/kvakulo/Switcheroo/issues/new).

If you have an idea how to improve Switcheroo, then don't be shy to submit it as well.

Pull requests are greatly appreciated. If you plan a larger feature, then please get in contact, so we can coordinate the efforts.

How to build
------------

```
nuget.exe restore Switcheroo.sln
msbuild.exe Switcheroo.sln /p:Configuration=Release
```


License
-------

Switcheroo is open source and is licensed under the [GNU GPL v. 3](http://www.gnu.org/licenses/gpl.html).

```
Copyright 2014, 2015 Regin Larsen
Copyright 2009, 2010 James Sulak
 
Switcheroo is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Switcheroo is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
 
You should have received a copy of the GNU General Public License
along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
```


Credits
-------

[HellBrick](https://github.com/HellBrick), [ovesen](https://github.com/ovesen), [philippotto](https://github.com/philippotto), [tarikguney](https://github.com/tarikguney), [holymoo](https://github.com/holymoo), [elig0n](https://github.com/elig0n) and [trond-snekvik](https://github.com/trond-snekvik) have contributed to Switcheroo.

Switcheroo makes use of these great open source projects:

* [Managed Windows API](http://mwinapi.sourceforge.net), Copyright © 2006 Michael Schier, GNU Lesser General Public License (LGPL)
* [PortableSettingsProvider](https://github.com/crdx/PortableSettingsProvider), Copyright © crdx, The MIT License (MIT)
