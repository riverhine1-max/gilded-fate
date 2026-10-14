# UI style guide and visual theme

The game's screens are drawn with Unity's immediate-mode GUI (IMGUI) inside the large `GildedMainMenu` partial class. The **new look** is a shared, engine-free theme layer plus a thin bridge, so screens can be restyled screen by screen without rewriting their logic, and the original drawing stays in place next to the new one.

## The switch

* `UiTheme.NewLook` (in `Assets/Scripts/UI/GildedThemeBridge.cs`) is **on by default**.
* Launch the game with `-gfClassicUi` to draw the original look. The screenshot tool uses this to photograph before and after.
* Every use of the new look in an existing screen is `if (UiTheme.NewLook) …new… else …original…`. Nothing from the original drawing code was removed.
* The Chronicle book, writing, timing and recordings are not part of the theme. The **event** and **Fateweave** screens keep their own authored panels (`ThemeKeepsClassicScreen`); only shared chrome (top bar, tooltips, buttons) is themed there.

## Where things live

| What | Where |
| --- | --- |
| Engine-free theme (no Unity types) | `Assets/Scripts/Theme/` (assembly `GildedFate.Theme`) |
| Generated images (frames, glows, rims, medallions, plates, glass) | `ThemeImages.cs` (`ThemeImages` generators, `ThemeAssets` instances) |
| Colours | `ThemePalette.cs` |
| 9-slice, tiling, text effects (shadow, outline, metal gradient) | `ThemeDraw.cs` |
| Widgets: panel, plaque, button, bars, titles, HUD strip, rows, sliders, tooltips, End Turn plate, Block badge, outline | `ThemeWidgets.cs` |
| Act map: threads, room sockets and rings, embers, light shafts, header, summit crown | `ThemeMap.cs` |
| Drawing through Unity's GUI, and the bridge methods the existing screens call | `Assets/Scripts/UI/GildedThemeBridge.cs` |
| Tests (run in Unity EditMode and in the harness) | `Assets/Tests/EditMode/ThemeTests.cs` |

Everything the theme draws goes through `IThemeCanvas` (`Fill`, `Image`, `ImageUV`, `TextRaw`, `MeasureText`, `PushClip`, `PopClip`, `Line`). The game implements it over IMGUI (`UnityThemeCanvas`); the preview tool implements it over a bitmap. Widgets are pure functions of their inputs and the canvas clock, which is why they can be previewed as stills and unit-tested.

## Look

Dark gilded gothic: obsidian glass lit from the **upper left**, bevelled gold and bronze metal, one warm ember glow as the light source, and a colour cast per kind of thing (red attacks and combat, azure Block, violet arcane, jade growth). Soft shadows give depth; hairlines stay crisp; text over art sits on a plate or has a shadow so it is readable on any backdrop.

Rules the widgets follow:

* **Hover and press ease** (about 9 and 20 per second) instead of snapping; Reduce Motion snaps.
* **Reduce Motion** holds the theme clock at zero: nothing moves, but nothing disappears. **Reduced effects** draws fewer particles and rays. **Reduce Flashing** removes the low-health pulse.
* Emblems and art (room emblems, cards, relics, hero portraits) are the game's own images, drawn between a widget's "under" and "over" layers. The theme never replaces them.
* Combat readability is unchanged: bars, intents and Block read exactly as before, only better lit.

## Adding or restyling something

1. Put the drawing in `ThemeWidgets.cs` (or `ThemeMap.cs` for the map) as a function of inputs and the canvas. No Unity types.
2. Add a small bridge method in `GildedThemeBridge.cs` that guards on `Event.current.type == EventType.Repaint`, resets `GUI.color` to white if the original ignored it, and calls the widget.
3. In the existing screen, wrap the old drawing: `if (UiTheme.NewLook) Bridge(...); else { original }`.
4. Add a test in `ThemeTests.cs` and a scene to the preview tool if the look needs judging.

## Seeing it without Unity

```bash
# render the theme (menu, components, images, act map with the real emblems) to PNG
dotnet run --project Tools/ThemePreview -- /tmp/theme-preview

# unit tests for the theme (and the Chronicle) with no Unity
dotnet run --project Tools/ChronicleHarness

# compile every game script against Unity reference assemblies, no Unity (about a minute)
bash Tools/UnityTypeCheck/check.sh
```

The preview needs the art it paints over (`Art/RunMap_LowerVault.png`, `Art/Identity/RoomEmblems.png`, fonts); in a checkout without Git LFS, `git lfs pull --include="Assets/Resources/Art/*,Assets/Resources/Fonts/*"` fetches them.

`Tools/UnityTypeCheck` uses the community `UnityEngine.Modules` 2021.3 reference package and Input System stand-ins, so it reports a few Unity 6 APIs as missing; `check.sh` filters those known gaps and prints only real errors. Unity's own compile remains the source of truth.

## Seeing it in the real game

Run **Build Gilded Fate** (Actions) with targets `linux-captures`. It builds the Linux player, runs the game's own capture mode on a virtual display with software OpenGL, photographs about 32 screens in the new look (1920x1080 and 1280x720) and the original look (1920x1080) and publishes them as JPEGs on the `ui-captures` branch (and as the `ui-captures` run artifact). Locally: `bash ci/capture-screens.sh <player folder> <output folder> [WxH ...]`.

These are stills only. They say nothing about motion, sound or real-GPU performance, and a person still needs to play the result.
