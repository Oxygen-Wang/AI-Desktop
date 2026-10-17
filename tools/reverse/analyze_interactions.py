import json
from pathlib import Path
from mcp_client import Client
OUT=Path(__file__).resolve().parents[2]/'research/decompiled/native/interactions'
OUT.mkdir(parents=True,exist_ok=True)
def query(c,name,tool,args):
    result=c.call(tool,args);(OUT/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    data=result.get('structuredContent')
    if data is None:
        text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
        try:data=json.loads(text)
        except ValueError:data={}
    if isinstance(data,dict) and data.get('code'):(OUT/(name+'.c')).write_text(data['code'].replace('\r',''),encoding='utf-8',newline='\n')
    print(name+': '+('error' if result.get('isError') else 'ok'),flush=True)
    return data
c=Client('http://127.0.0.1:8081/mcp');c.initialize()
info=query(c,'target','get_program_info',{})
assert info['name']=='DesktopDock64.dll'
query(c,'startup-subclass','decompile_function_by_address',{'address':'0x180071680'})
for phrase in ['StartSubclass','DoubleClick','DragDrop','IconsHidden','HideIcons','MouseDouble']:
    data=query(c,'strings-'+phrase,'list_strings',{'filter':phrase,'limit':30})
    for i,item in enumerate(data.get('items',[])[:5]):
        refs=query(c,'xref-'+phrase+'-'+str(i),'get_xrefs_to',{'address':'0x'+item['address'],'limit':20})
        functions={r['from_function']['address'] for r in refs.get('items',[]) if r.get('from_function')}
        for address in list(functions)[:2]:query(c,'fn-'+address,'decompile_function_by_address',{'address':'0x'+address})
c.close()
