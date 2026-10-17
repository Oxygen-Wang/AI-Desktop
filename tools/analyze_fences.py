"""Read-only PE, CLR metadata and selected native entry-point inspection."""
import sys, json, hashlib, re, importlib.util, argparse
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools' / 'python'))
import pefile, capstone, dnfile

def inspect(path):
    data = path.read_bytes()
    pe = pefile.PE(data=data)
    imports = {entry.dll.decode(errors='replace'): [
        item.name.decode(errors='replace') if item.name else f'ordinal:{item.ordinal}'
        for item in entry.imports] for entry in getattr(pe, 'DIRECTORY_ENTRY_IMPORT', [])}
    exports = [{'name': symbol.name.decode(errors='replace') if symbol.name else None,
                'ordinal': symbol.ordinal, 'rva': hex(symbol.address)}
               for symbol in getattr(getattr(pe, 'DIRECTORY_ENTRY_EXPORT', None), 'symbols', [])]
    patterns = re.compile(r'fence|portal|rollup|roll.up|desktop|snapshot|layout|rule|workerw|progman|defview|IShell|IContext|DllRegister|SetParent|SetWindow|SetWindowsHook', re.I)
    strings = re.findall(rb'[\x20-\x7e]{6,}', data)
    strings += [x[0::2] for x in re.findall(rb'(?:[\x20-\x7e]\x00){6,}', data)]
    clues = sorted(set(s.decode(errors='replace') for s in strings if patterns.search(s.decode(errors='replace'))))
    result = {'path': str(path), 'sha256': hashlib.sha256(data).hexdigest(),
              'size': len(data), 'machine': hex(pe.FILE_HEADER.Machine),
              'imports': imports, 'exports': exports, 'relevant_strings': clues}
    managed = dnfile.dnPE(data=data)
    result['managed'] = bool(managed.net)
    if managed.net and managed.net.mdtables:
        types = []
        for row in managed.net.mdtables.TypeDef.rows:
            name = str(row.TypeNamespace) + '.' + str(row.TypeName)
            methods = [str(item.row.Name) for item in row.MethodList]
            if patterns.search(name) or any(patterns.search(m) for m in methods):
                types.append({'type': name, 'methods': methods})
        result['relevant_managed_types'] = types
    else:
        decoder = capstone.Cs(capstone.CS_ARCH_X86,
                              capstone.CS_MODE_64 if pe.FILE_HEADER.Machine == 0x8664 else capstone.CS_MODE_32)
        targets = [{'name': 'PE_entrypoint', 'rva': hex(pe.OPTIONAL_HEADER.AddressOfEntryPoint)}]
        targets += [s for s in exports if s['name'] and patterns.search(s['name'])][:12]
        result['disassembly'] = []
        for symbol in targets:
            rva = int(symbol['rva'], 16)
            if not rva:
                continue
            code = pe.get_data(rva, 128)
            instructions = [f'{i.address:#x}: {i.mnemonic} {i.op_str}' for i in decoder.disasm(code, pe.OPTIONAL_HEADER.ImageBase + rva)][:30]
            result['disassembly'].append(dict(symbol, instructions=instructions))
    return result

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--install-dir', default=r'C:\Program Files (x86)\Stardock\Fences')
    args = parser.parse_args()
    output = ROOT / 'research' / 'evidence'
    output.mkdir(parents=True, exist_ok=True)
    skill = Path.home() / '.codex/skills/reverse-engineering/scripts/triage.py'
    spec = importlib.util.spec_from_file_location('triage', skill)
    triage = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(triage)
    summaries = []
    for name in ['Fences.exe', 'DesktopDock.dll', 'DesktopDock64.dll', 'FencesMenu64.dll']:
        path = Path(args.install_dir) / name
        result = inspect(path)
        result['skill_triage'] = triage.parse_pe(path.read_bytes())
        (output / (name + '.json')).write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
        summaries.append({'file': name, 'sha256': result['sha256'], 'managed': result['managed'],
                          'exports': len(result['exports']), 'import_libraries': list(result['imports']),
                          'relevant_types': len(result.get('relevant_managed_types', []))})
    (output / 'summary.json').write_text(json.dumps(summaries, indent=2), encoding='utf-8')
    print(json.dumps(summaries, indent=2))

if __name__ == '__main__':
    main()
