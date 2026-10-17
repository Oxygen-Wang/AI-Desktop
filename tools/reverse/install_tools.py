from pathlib import Path
import json, urllib.request, hashlib, zipfile, stat, concurrent.futures

ROOT=Path.home()/'.codex/tools/fences-re-tools'
ROOT.mkdir(parents=True,exist_ok=True)
RELEASES=json.loads((Path(__file__).parent/'releases.json').read_text())

def install(repo,asset_name,name):
    asset=next(a for a in RELEASES[repo]['assets'] if a['name']==asset_name)
    archive=ROOT/asset_name
    if not archive.exists():
        print('Downloading '+name,flush=True)
        req=urllib.request.Request(asset['browser_download_url'],headers={'User-Agent':'Codex-Fences-RE'})
        with urllib.request.urlopen(req,timeout=120) as r, archive.with_suffix('.partial').open('wb') as out:
            while b:=r.read(1024*1024):out.write(b)
        archive.with_suffix('.partial').replace(archive)
    with archive.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    expected=asset.get('digest','').removeprefix('sha256:')
    if not expected or expected!=digest:raise ValueError('Missing or mismatched official SHA256 for '+name)
    destination=ROOT/name
    destination.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        for entry in z.infolist():
            target=(destination/entry.filename).resolve()
            if not target.is_relative_to(destination.resolve()) or stat.S_ISLNK(entry.external_attr>>16):raise ValueError('Unsafe archive entry')
        z.extractall(destination)
    print('Verified and extracted '+name,flush=True)
    return {'repo':repo,'version':RELEASES[repo]['tag_name'],'archive':str(archive),'sha256':digest,'destination':str(destination)}

if __name__=='__main__':
    ghidra_asset=RELEASES['NationalSecurityAgency/ghidra']['assets'][0]['name']
    tasks=[('cervonwong/ILSpy-MCP','ilspy-mcp-win-x64.zip','ilspy'),('LaurieWired/GhidraMCP','GhidraMCP-release-2-0.zip','ghidra-mcp-release'),('NationalSecurityAgency/ghidra',ghidra_asset,'ghidra')]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:result=list(pool.map(lambda task:install(*task),tasks))
    (ROOT/'install-manifest.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print('Tool installation complete',flush=True)
