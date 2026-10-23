#!/usr/bin/env python3
"""Verify the 0.5.0 import, release membership, locale, BBCode and packaged file hashes."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
legacy = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else root.parent / 'big-ambitions-hard-mode-patch'
commit = '36e8eec'
# These adapters need lifecycle/UI integration; their exact changes are reviewed separately.
adapters = {'src/Options.cs', 'src/SeriesSettingsButton.cs', 'src/HappinessRebalance.cs'}
# Explicit 1.1.0 follow-up deltas; preserve the rest of each imported implementation byte for byte.
reviewed_updates = {
    'Core/Targets.cs': [('"2f360a365cf7915f", "4f82b4f88f919178"', '"2f360a365cf7915f", "4f82b4f88f919178", "f339319c9ec1ad65", "1f7647f2e188a43e"')],
    'Core/Rules.cs': [
        ('public static readonly Rule UtilityClosureReasons = new Rule("utility_closure_reasons", null, true);',
         'public static readonly Rule UtilityClosureReasons = new Rule("utility_closure_reasons", null, true);\n        public static readonly Rule MuteRloNightChirping = new Rule("mute_rlo_night_chirping", null, true);'),
        ('OfficeIt, UtilityClosureReasons, FactoryDiplomas', 'OfficeIt, UtilityClosureReasons, MuteRloNightChirping, FactoryDiplomas')],
    'Core/SeriesSettings.cs': [('Hmp("office_it", "Economy Expansion running costs on RLO"),',
        'Hmp("office_it", "Economy Expansion running costs on RLO"),\n            Hmp("mute_rlo_night_chirping", "mute only RLO city crickets"),')],
    'src/RloGate.cs': [
        ('owner job pay and the hotel rule can run.', 'owner job pay, utility closure reasons, night chirping mute and the hotel rule can run.'),
        ('+ " and the hotel', '+ ", utility closure reasons, night chirping mute and the hotel'),
        ('|| Rules.OwnerJobFullPay.Enabled || Rules.HappinessRebalance.Enabled;', '|| Rules.OwnerJobFullPay.Enabled || Rules.HappinessRebalance.Enabled || Rules.MuteRloNightChirping.Enabled;')],
    'src/UtilityClosureReasonPatch.cs': [
        ('new[] { "4f82b4f88f919178" }', 'new[] { "4f82b4f88f919178", "f339319c9ec1ad65", "1f7647f2e188a43e" }'),
        ('reviewed for RLO 0.49.0 only', 'reviewed for RLO 0.49.0 and 0.50.0')],
}
# Exact companion follow-up sources, reviewed as ordinary diffs. All other imported sources and defaults stay pinned.
companion_imports = json.loads((root / 'tools/companion-import-digests.json').read_text())
checked = 0
for folder in ('Core', 'src', 'tests'):
    paths = subprocess.check_output(['git', '-C', str(legacy), 'ls-tree', '-r', '--name-only', commit, folder], text=True).splitlines()
    for path in paths:
        if not path.endswith('.cs') or path == 'src/HardModePatchMod.cs':
            continue
        original = subprocess.check_output(['git', '-C', str(legacy), 'show', f'{commit}:{path}'], text=True)
        expected = original.replace('HardModePatchMod.HarmonyId', 'GameplayFeatures.HarmonyId').replace('HART. Mod Pack Patch', 'HART. Mod Pack').replace('[HART. Mode Patch]', '[HART. Mod Pack]')
        if path == 'Core/SeriesSettings.cs':
            expected = expected.replace('Self = "HardModePatch"', 'Self = "HARTModPack"')
        for before, after in reviewed_updates.get(path, []):
            assert before in expected, path
            expected = expected.replace(before, after)
        target = root / folder / 'Gameplay' / Path(path).name
        assert target.is_file(), f'Missing imported file {path}'
        if path in companion_imports:
            assert hashlib.sha256(target.read_bytes()).hexdigest() == companion_imports[path], f'Unrecorded companion delta in {path}'
            checked += 1
        elif path not in adapters:
            assert target.read_text() == expected, f'Unexpected gameplay change in {path}'
            checked += 1
print(f'PASS {checked} imports match 0.5.0 with explicit RLO 0.50.0/option and pinned companion deltas; 3 lifecycle/UI adapters reviewed separately')

manifest = json.loads((root / 'modpack.json').read_text())
series_path = root.parents[1] / 'youtube/hart-group/mods/modpack/modpack.json'
if series_path.exists():
    series = json.loads(series_path.read_text())
    # The series repo retains its original merge snapshot; current release pins live in this repo.
    assert [m['dll'] for m in series['mods']] == [m['assembly'] + '.dll' for m in manifest['members']]
    assert [m['required'] for m in series['mods']] == [m['required'] for m in manifest['members']]
    assert not any(m.get('dll') in ('HardModePatch.dll', 'PersistentCity.dll') for m in series['mods'])
assert manifest['version'] == '1.1.0'
assert len([m for m in manifest['members'] if m['required']]) == 13
assert len([m for m in manifest['members'] if not m['required']]) == 3
assert not any(m['assembly'] == 'HardModePatch' for m in manifest['members'])
locale = json.loads((root / 'config/Locales/en.json').read_text())
old_locale = json.loads(subprocess.check_output(['git', '-C', str(legacy), 'show', f'{commit}:config/Locales/en.json'], text=True))
for key, value in old_locale.items():
    assert locale[key] == value.replace('HART. Mod Pack Patch', 'HART. Mod Pack').replace('HART. Mode Patch', 'HART. Mod Pack'), key
assert all(isinstance(m['workshopId'], int) for m in manifest['members'])
assert next(m for m in manifest['members'] if m['assembly'] == 'RealLifeOverhaul')['testedVersions'][-1] == '0.50.0'
print('PASS series roster agrees; current release pins and numeric Workshop IDs; 13 required, 3 optional; imported locale labels retained')

text = (root / 'workshop/DESCRIPTION.bbcode.txt').read_text()
for heading in ('Still being playtested', 'Series house rules', 'Series settings', 'Our own rules', 'Fixes for other mods', 'Mod updates'):
    assert heading in text
for placeholder in ('THRIVING_RIVALS_ID', 'CITY_PERFORMANCE_ID'):
    assert placeholder not in text
for item_id in ('3825350971', '3825351532'):
    assert text.count('id=' + item_id) == 1
assert 'Supports reviewed RLO 0.50.0' in text and '0.50.0 at release' in text
assert 'Mute RLO night chirping' in text
listing = text.split('[h2]The mods in the pack[/h2]')[1].split('[h2]Optional[/h2]')[0]
assert 'Mod Pack Patch' not in listing
assert listing.count('[*]') == 13
assert 'https://www.paypal.com/donate/?hosted_button_id=Q8KVURCRBFLQN' in text.strip().splitlines()[-1]
assert 'adds nothing to your save' not in text
for tag in ('h2', 'list', 'b', 'url'):
    assert len(re.findall(r'\[' + tag + r'(?:=[^\]]*)?\]', text)) == text.count(f'[/{tag}]'), tag
print('PASS merged Workshop BBCode sections, 13-member list, published IDs, RLO 0.50.0, night mute and final PayPal link')

archive = root / 'build/HARTModPack-1.1.0.zip'
expected_hash = (root / 'build/HARTModPack-1.1.0.zip.sha256').read_text().split()[0]
assert hashlib.sha256(archive.read_bytes()).hexdigest() == expected_hash
file_hashes = dict(line.split('  ', 1)[::-1] for line in (root / 'build/HARTModPack.sha256').read_text().splitlines())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert sorted(z.namelist()) == sorted('HARTModPack/' + n for n in file_hashes)
    for n, digest in file_hashes.items():
        assert hashlib.sha256(z.read('HARTModPack/' + n)).hexdigest() == digest, n
        assert z.read('HARTModPack/' + n) == (root / 'build/HARTModPack' / n).read_bytes()
    assert hashlib.sha256(z.read('HARTModPack/Dependencies/0Harmony.dll')).hexdigest() == '7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c'
    dlls = sorted(n for n in z.namelist() if n.endswith('.dll'))
    assert dlls == ['HARTModPack/Dependencies/0Harmony.dll', 'HARTModPack/HARTModPack.dll'], dlls
    assert z.read('HARTModPack/modpack.json') == (root / 'modpack.json').read_bytes()
print(f'PASS ZIP CRC, exact {len(file_hashes)}-file inventory, every payload hash, one root DLL and pinned Harmony only')
