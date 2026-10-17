from pathlib import Path
import json
from mcp_client import Client
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'research/decompiled/managed'
ASSEMBLY=r'C:\Program Files (x86)\Stardock\Fences\Fences.exe'
JOBS=[('decompile_type',{'typeName':'DesktopDockUI.Simulation.DeskGroup'}),('decompile_type',{'typeName':'DesktopDockUI.Simulation.DeskGroupSettings'}),('decompile_type',{'typeName':'DesktopDockUI.Simulation.IconElementSettings'}),('decompile_type',{'typeName':'DesktopDockUI.SnapshotInfo'})]
if __name__=='__main__':
    c=Client('http://127.0.0.1:3001/mcp');c.initialize()
    for tool,args in JOBS:
        result=c.call(tool,dict(args,assemblyPath=ASSEMBLY))
        name=args.get('typeName',args.get('namespaceName','search'))+'.'+args.get('methodName',args.get('searchTerm','type'))
        (OUT/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
        (OUT/(name+'.txt')).write_text(text,encoding='utf-8',newline='\n')
        print(('ERROR ' if result.get('isError') else 'OK ')+name+' '+str(len(text)),flush=True)
    c.close()
