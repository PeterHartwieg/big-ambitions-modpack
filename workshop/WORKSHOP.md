# HART. Mod Pack: Workshop upload

Paste-ready fields for the game's Mod Creator (Main menu > Mods > Mod Creator). This folder is not part of the package, except `preview.jpg` once it exists. Nothing here has been uploaded.

Upload this item last. The release plan's order: HART. Thriving Rivals, HART. Mod Pack Patch, City Performance, Campaign Objectives 1.2.0, then HART. Mod Pack. The three new items need their Workshop links in the description and their IDs in the Required items before this page is complete.

## Fields

**Mod folder:** `HARTModPack`. Unzip `build/HARTModPack-1.0.0.zip` into `ModsLocal` on the PC that uploads; the Mod Creator lists the folders in `ModsLocal`. Remove the folder from `ModsLocal` after the upload, or the pack loads twice once you subscribe.

**Title:** `HART. Mod Pack`

**Target build:** `3682`, the Windows build whose DLLs the mod compiles against (`BigAmbitions.dll` SHA-256 `9427561b…e911`).

**Version:** `1.0.0`. Package: `build/HARTModPack/`; archive: `build/HARTModPack-1.0.0.zip` with `build/HARTModPack-1.0.0.zip.sha256`; file hashes: `build/HARTModPack.sha256`.

**Thumbnail:** open. `hart-group/tools/build_brand.py` has no mode for a mod pack thumbnail; its only mod thumbnail mode is `--campaign-thumbnail`, for Campaign Objectives. The Mod Creator takes the first image in the mod folder's root, so put a `preview.jpg` under 1 MB in `workshop/` and rerun `tools/build.sh`; it copies the file into the package and the hashes change.

**Bug reports:** open. There is no pack channel on the Discord yet; the description has a placeholder, `DISCORD-LINK`.

**Donations:** the PayPal hosted button `Q8KVURCRBFLQN`, which covers all of Peter's Big Ambitions projects.

**Short description** (for a link post or the first line of a share): `Every mod from the HART modded Big Ambitions series in one subscription. It turns the pack on for you and tells you what's missing.`

## Before pasting

- Replace `LINK-RIVAL-BALANCE`, `LINK-MODE-PATCH` and `LINK-CITY-PERFORMANCE` with the new items' Workshop links, and `DISCORD-LINK` with a channel invite.
- POLYMODS HYPERREAL's author is `123456` in `hart-group/mods/modpack/modpack.json`. Check the name on its Workshop page.
- Food Trucks has no Workshop ID in `modpack.json`; 3822355169 is from the release plan and the Mac Workshop folder, where it holds `FoodTrucks.dll`.
- `modpack.json` calls the Hyperreal bundles optional in its `platform` and `compatibilityCheck` notes, while the release plan makes POLYMODS HYPERREAL required. The pack treats it as required; move it to the optional list in `Core/Pack.cs` and here if that's wrong.
- `modpack.json` also has a launch routine (open Settings > Mods once so saved options apply, load the save within 240 s for Dynamic Rivals, restart before loading a different save). The description leaves it out. Add it to "Good to know" if it still holds.

## Required items

Set these on the Workshop page (Edit page > Add/Remove Required Items). Not the optional ones.

- [ ] Real Life Overhaul, 3816536560
- [ ] Dynamic Rivals, 3801170388
- [ ] HART. Thriving Rivals (formerly HART Rival Balance), new item ID after its upload
- [ ] HART. Mod Pack Patch (formerly HART. Mode Patch), new item ID after its upload
- [ ] City Performance, new item ID after its upload
- [ ] HART. Street Food (Food Trucks), 3822355169
- [ ] HART. Self Driving (Driven Rides), 3819853552
- [ ] HART. RDSTR (Roadster), 3821783833
- [ ] Alcware - Seasons, 3803289067
- [ ] Alcware - Retail Expansion, 3801675050
- [ ] Internet Cafe, 3796860957
- [ ] Bakery - V1.2, 3810927743
- [ ] POLYMODS HYPERREAL, 3803756901
- [ ] Campaign Objectives, 3807225297

Leave out Camera Tools (3743579410), HART Free Camera (3806807684), Big Copilot Link (3806322395) and Persistent City.

## Description (Steam formatting)

```
All the mods from my HART modded Big Ambitions series in one subscription. HART. Mod Pack lists every mod of the series as a required item, so Steam offers to subscribe to all of them with this one. The small mod inside turns the pack on for you and tells you if something is missing.

[h2]How to install[/h2]
[list]
[*][b]Subscribe[/b] to HART. Mod Pack and accept Steam's offer to subscribe to the required items. If Steam doesn't ask, subscribe to each required mod in the list below.
[*][b]Start the game.[/b] If HART. Mod Pack is off in Main menu > Mods, turn it on and restart. It then turns on every pack mod the game left off and asks you to restart once more.
[*][b]Apply the series settings:[/b] Settings > Mods > HART. Mod Pack Patch > [b]Apply HART series settings[/b], then restart. Start a New game > Custom.
[/list]

[h2]The mods in the pack[/h2]
Every mod here is someone's own work, and the pack wouldn't exist without them. Please rate and thank them on their own pages. The ones marked (mine) are my mods.
[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3816536560]Real Life Overhaul[/url] by Adult Cudi
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3801170388]Dynamic Rivals[/url] by Andre1408
[*][url=LINK-RIVAL-BALANCE]HART. Thriving Rivals[/url] (mine): keeps rivals in business and competing on hard settings
[*][url=LINK-MODE-PATCH]HART. Mod Pack Patch[/url] (mine): the pack's companion mod, with the series settings and the pack's balance changes
[*][url=LINK-CITY-PERFORMANCE]City Performance[/url] (mine)
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3822355169]HART. Street Food[/url] (mine)
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3819853552]HART. Self Driving[/url] (mine)
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3821783833]HART. RDSTR[/url] (mine)
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3803289067]Alcware - Seasons[/url] by Alcsaar
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3801675050]Alcware - Retail Expansion[/url] by Alcsaar
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3796860957]Internet Cafe[/url] by Micolash
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3810927743]Bakery - V1.2[/url] by Rogue
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3803756901]POLYMODS HYPERREAL[/url] by 123456
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807225297]Campaign Objectives[/url] (mine)
[/list]

[h2]Optional[/h2]
Recording tools I use for the videos. The pack works without them and doesn't turn them on.
[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3743579410]Camera Tools[/url] by Noctirix
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3806807684]HART Free Camera[/url] (mine)
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3806322395]Big Copilot Link[/url] (mine)
[/list]

[h2]What HART. Mod Pack does[/h2]
[list]
[*]At startup it finds each pack mod by its DLL name and turns on the ones that are subscribed but off. Then it asks you to restart.
[*]Once per launch it checks the pack and shows one notice if a mod is missing, switched off, failed to load, loaded twice or on a version the pack wasn't tested with. Click the notice to hide it until something changes.
[*]Settings > Mods > HART. Mod Pack lists every mod with its state and version, and has a [b]Check again[/b] button.
[/list]

[h2]Good to know[/h2]
[list]
[*]It only ever turns mods on, never off. It turns each mod on once; if you switch one off afterwards, it stays off and the pack tells you.
[*]It leaves the optional mods and your local mods alone. If you have a mod in ModsLocal, its Workshop copy stays off so it doesn't load twice.
[*]It recognises each mod by its DLL name. Another mod that uses the same DLL name counts as that pack mod.
[*]Bakery and POLYMODS HYPERREAL ship their art for Windows only. On a Mac the Bakery's business assets and Hyperreal's effects are missing.
[*]The pack was put together on game build 3682. When a mod in it updates, the check may say the pack wasn't tested with the new version. That's a heads-up, not an error.
[*]HART. Mod Pack adds nothing to your save.
[/list]

[h2]Bug reports[/h2]
Post problems with the pack in [url=DISCORD-LINK]my Discord[/url], one post per problem. Include the game build and what happened. The lines in Player.log that start with [HART. Mod Pack] list every mod's state and help most. The file is in %USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions on Windows and in ~/Library/Logs/Hovgaard Games/Big Ambitions on a Mac. Bugs in a single mod go to that mod's page.

[h2]Support[/h2]
The pack is free and stays free. If you'd like to say thanks, you can [url=https://www.paypal.com/donate/?hosted_button_id=Q8KVURCRBFLQN]support my Big Ambitions projects via PayPal[/url].
```

**Change log** (first upload):

```
1.0.0: first release

- Turns on the pack's mods that Steam downloaded but the game left off, then asks for a restart.
- Checks the pack once per launch and shows one notice if something is missing, off, failed, loaded twice or untested.
- Settings > Mods > HART. Mod Pack lists every mod with a Check again button.
```
