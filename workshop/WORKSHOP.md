# HART. Mod Pack: Workshop upload

Paste-ready fields for the game's Mod Creator (Main menu > Mods > Mod Creator). This folder is not part of the package, except `preview.png`. Nothing here has been uploaded.

Upload this item last. The release plan's order: HART. Thriving Rivals, HART. Mod Pack Patch, City Performance, Campaign Objectives 1.2.1, then HART. Mod Pack. The three new items still need their actual IDs in Required items after Peter publishes them. The paste-ready description names them without placeholder links; add their real links after publication.

## Fields

**Mod folder:** `HARTModPack`. Unzip `build/HARTModPack-1.0.0.zip` into `ModsLocal` on the PC that uploads; the Mod Creator lists the folders in `ModsLocal`. Remove the folder from `ModsLocal` after the upload, or the pack loads twice once you subscribe.

**Title:** `HART. Mod Pack`

**Target build:** `3682`, the Windows build whose DLLs the mod compiles against (`BigAmbitions.dll` SHA-256 `9427561b…e911`).

**Version:** `1.0.0`. Package: `build/HARTModPack/`; archive: `build/HARTModPack-1.0.0.zip` with `build/HARTModPack-1.0.0.zip.sha256`; file hashes: `build/HARTModPack.sha256`.

**Thumbnail:** `workshop/preview.png`, the only image in the package root. Approved artwork; the build script copies it automatically. Gallery cards stay outside the runtime package.

**Bug reports:** use the Workshop comments or discussion for the relevant item; no Discord invitation is configured here.

**Donations:** the PayPal hosted button `Q8KVURCRBFLQN`, which covers all of Peter's Big Ambitions projects.

**Short description** (for a link post or the first line of a share): `Every mod from the HART modded Big Ambitions series in one subscription. It turns the pack on for you and tells you what's missing.`

## Before pasting

- Add real Workshop links for Thriving Rivals, Mod Pack Patch and City Performance once their items exist. The description is usable without those links; Required items must still be configured manually.
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

[h2]Series house rules[/h2]
The series button in HART. Mod Pack Patch enables Economy Expansion-inspired running costs on RLO: equipment and opening hours affect utilities, and offices need internet and pay $150 per computer each game month at starting prices. It explicitly turns the old flat 2x/3x rent increase off. Existing leases keep their stored rent. Economy Expansion itself is not included or required.

[h2]The mods in the pack[/h2]
Every mod here is someone's own work, and the pack wouldn't exist without them. Please rate and thank them on their own pages. The ones marked (mine) are my mods.
[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3816536560]Real Life Overhaul[/url] by Adult Cudi
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3801170388]Dynamic Rivals[/url] by Andre1408
[*]HART. Thriving Rivals (mine): keeps rivals in business and competing on hard settings
[*]HART. Mod Pack Patch (mine): the pack's companion mod, with the series settings and the pack's balance changes
[*]City Performance (mine): experimental, all behavior features on by default and logging off. If problems occur, enable logging and disable features one by one to find the best setup
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
Post problems with the pack in this item's Workshop comments or discussions, one post per problem. Include the game build and what happened. The lines in Player.log that start with [HART. Mod Pack] list every mod's state and help most. The file is in %USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions on Windows and in ~/Library/Logs/Hovgaard Games/Big Ambitions on a Mac. Bugs in a single mod go to that mod's page.

[h2]Support[/h2]
The mod is free and stays free. If you'd like to say thanks, you can [url=https://www.paypal.com/donate/?hosted_button_id=Q8KVURCRBFLQN]support my Big Ambitions projects via PayPal[/url].
```

**Change log** (first upload):

```
[b]HART. Mod Pack 1.0.0[/b]

[list]
[*]First release. Checks required pack members, turns on subscribed required mods the game left off, and reports missing, failed, duplicate or untested members.
[*]Uses the approved HART. Thriving Rivals and HART. Mod Pack Patch names.
[*]Apply HART series settings in Mod Pack Patch to enable Economy Expansion-inspired equipment utilities and office IT on RLO, replacing the flat 2x/3x rent increase. Existing signed leases retain their stored rent.
[*]Recognizes Campaign Objectives 1.2.1 after the documented Mac integration pass. Windows/Workshop acceptance remains pending.
[*]Restart when prompted, then apply HART series settings and start New game > Custom.
[/list]
```
