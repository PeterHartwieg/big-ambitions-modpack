#!/usr/bin/env python3
"""Packages build/HARTModPack after tools/build.sh compiled the DLL. Writes per-file hashes and a zip with its hash."""
import hashlib, pathlib, re, shutil, sys, zipfile

root = pathlib.Path(__file__).resolve().parent.parent
core = (root / 'Core/Pack.cs').read_text()
version = re.search(r'Version = "([^"]+)"', core).group(1)
src = (root / 'src/HARTModPackMod.cs').read_text()
info = re.search(r'AssemblyInformationalVersion\("([^"]+)"\)', src).group(1)
assembly = re.search(r'AssemblyVersion\("([^"]+)"\)', src).group(1)
changelog = (root / 'CHANGELOG.md').read_text()
workshop = (root / 'workshop/WORKSHOP.md').read_text()
if info != version: sys.exit(f'Version mismatch: Core {version}, informational {info}')
if assembly != version + '.0': sys.exit(f'Version mismatch: Core {version}, AssemblyVersion {assembly}')
if f'## {version} ' not in changelog: sys.exit(f'CHANGELOG.md has no entry for {version}')
if f'**Version:** `{version}`' not in workshop or f'HARTModPack-{version}.zip' not in workshop: sys.exit(f'workshop/WORKSHOP.md does not name version {version}')

out = root / 'build' / 'HARTModPack'
dll = out / 'HARTModPack.dll'
if not dll.is_file(): sys.exit('Run tools/build.sh first')
files = {
    'Locales/en.json': root / 'config/Locales/en.json',
    'README.md': root / 'README.md',
    'CHANGELOG.md': root / 'CHANGELOG.md',
    'LICENSE.txt': root / 'LICENSE.txt',
}
# The Mod Creator uses the first image in the mod folder's root as the Workshop thumbnail.
preview = root / 'workshop' / 'preview.jpg'
if preview.is_file(): files['preview.jpg'] = preview
for name, source in files.items():
    dest = out / name
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, dest)
names = sorted(['HARTModPack.dll', *files])
if len(list(out.glob('*.dll'))) != 1: sys.exit('The package root must hold exactly one DLL')

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
(root / 'build' / 'HARTModPack.sha256').write_text(''.join(f'{sha(out / n)}  {n}\n' for n in names))
archive = root / 'build' / f'HARTModPack-{version}.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for n in names:
        # Fixed timestamps, so the same inputs give the same zip.
        entry = zipfile.ZipInfo('HARTModPack/' + n, date_time=(2026, 1, 1, 0, 0, 0))
        entry.compress_type = zipfile.ZIP_DEFLATED
        entry.external_attr = 0o644 << 16
        z.writestr(entry, (out / n).read_bytes())
digest = sha(archive)
(root / 'build' / f'HARTModPack-{version}.zip.sha256').write_text(f'{digest}  {archive.name}\n')
print(f'Packaged {out}\n{archive.name} {digest}\nHARTModPack.dll {sha(dll)}')
