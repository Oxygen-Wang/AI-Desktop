from pathlib import Path
import json
from mcp_client import Client
OUT=Path(__file__).resolve().parents[2]/'research/decompiled/native'
def run(c,name,tool,args):
    result=c.call(tool,args)
    (OUT/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
    (OUT/(name+'.txt')).write_text(text,encoding='utf-8',newline='\n')
    if not result.get('isError'):
        try:
            body=json.loads(text)
            if 'code' in body:(OUT/(name+'.c')).write_text(body['code'].replace('\r',''),encoding='utf-8',newline='\n')
        except ValueError:pass
    print(('ERROR ' if result.get('isError') else 'OK ')+name+' '+str(len(text)),flush=True)
    return json.loads(text) if text.startswith('{') else {}
c=Client('http://127.0.0.1:8081/mcp');c.initialize()
for address,name in [('180053ca0','desktop-enumeration-callback'),('180071520','apply-desktop-integration'),('180070770','notifier-window-procedure')]:run(c,name,'decompile_function_by_address',{'address':address})
for keyword in ['hwndListView','WorkerW','SHCreateShellFolderView','FencesNotifier']:
    body=run(c,'strings-'+keyword,'list_strings',{'filter':keyword,'limit':20})
    for i,item in enumerate(body.get('items',[])[:8]):
        if item.get('address'):run(c,'strings-'+keyword+'-xref-'+str(i),'get_xrefs_to',{'address':item['address'],'limit':50})
c.close()
