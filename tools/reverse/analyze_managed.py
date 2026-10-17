from pathlib import Path
import json,hashlib
from mcp_client import Client

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'research/decompiled/managed'
OUT.mkdir(parents=True,exist_ok=True)
ASSEMBLY=r'C:\Program Files (x86)\Stardock\Fences\Fences.exe'
TYPES=['DesktopDockUI.Layouts.Layout','DesktopDockUI.Layouts.LayoutArea','DesktopDockUI.Layouts.LayoutManager','DesktopDockUI.Layouts.LayoutBuilder','DesktopDockUI.Simulation.RuleSettings','DesktopDockUI.Simulation.Rule']

def save(name,result):
    (OUT/(name+'.json')).write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
    text='\n'.join(c.get('text','') for c in result.get('content',[]) if c.get('type')=='text')
    (OUT/(name+'.txt')).write_text(text,encoding='utf-8')
    if result.get('isError'):print('ERROR '+name+': '+text[:200],flush=True)
    else:print('OK '+name+' '+str(len(text))+' chars',flush=True)
    return text

if __name__=='__main__':
    c=Client('http://127.0.0.1:3001/mcp')
    info=c.initialize();(OUT/'server-info.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
    save('assembly-metadata',c.call('get_assembly_metadata',{'assemblyPath':ASSEMBLY}))
    save('layout-types',c.call('list_namespace_types',{'assemblyPath':ASSEMBLY,'namespaceName':'DesktopDockUI.Layouts','maxResults':100}))
    save('snapshot-members',c.call('search_members_by_name',{'assemblyPath':ASSEMBLY,'searchTerm':'snapshot','maxResults':100}))
    save('config-strings',c.call('search_strings',{'assemblyPath':ASSEMBLY,'pattern':r'Software\\Stardock|fencelayout|Groups|Rules|Monitors','maxResults':100}))
    for type_name in TYPES:
        save(type_name,c.call('decompile_type',{'assemblyPath':ASSEMBLY,'typeName':type_name}))
    (OUT/'target.json').write_text(json.dumps({'path':ASSEMBLY,'sha256':hashlib.sha256(Path(ASSEMBLY).read_bytes()).hexdigest()},indent=2),encoding='utf-8')
    c.close()
