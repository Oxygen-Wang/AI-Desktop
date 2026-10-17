from pathlib import Path
import json
from mcp_client import Client

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'research/decompiled/native'
OUT.mkdir(parents=True,exist_ok=True)

def save(name,result):
    (OUT/(name+'.json')).write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
    text='\n'.join(c.get('text','') for c in result.get('content',[]) if c.get('type')=='text')
    (OUT/(name+'.txt')).write_text(text,encoding='utf-8',newline='\n')
    if result.get('isError'):raise RuntimeError(name+': '+text)
    print('OK '+name+' '+str(len(text))+' chars',flush=True)
    return result

if __name__=='__main__':
    c=Client('http://127.0.0.1:8081/mcp');c.initialize()
    save('DesktopDock64-program-info',c.call('get_program_info',{}))
    save('DesktopDock64-exports',c.call('list_exports',{'limit':100}))
    save('desktop-strings',c.call('list_strings',{'filter':'DefView','limit':30}))
    for name in ['FindDesktopWindowSlow','ForceLoad','OnProcessAttach','ShellGraphics_LoadIconOfFile','Shell_GetLnkPath','GetResolutionFingerprint']:
        save(name,c.call('decompile_function',{'name':name}))
        save(name+'-calls',c.call('get_function_xrefs',{'name':name,'limit':40}))
    c.close()
