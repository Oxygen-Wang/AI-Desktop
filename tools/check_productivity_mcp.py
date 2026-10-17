"""Check typed AI changes and real Everything IPC against an isolated application."""
import asyncio, json, pathlib, subprocess, uuid
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
ROOT=pathlib.Path(__file__).resolve().parents[1]

async def main():
    app=(ROOT/'dist/latest.txt').read_text(encoding='utf-8-sig').strip()
    bridge=(ROOT/'dist/latest-mcp.txt').read_text(encoding='utf-8-sig').strip()
    folder=ROOT/'dist'/('productivity-mcp-'+uuid.uuid4().hex)
    folder.mkdir()
    config={'Version':3,'McpEnabled':True,'UseEverything':True,'Groups':[{'Id':'review','Name':'测试分区','Page':0}], 'Rules':[], 'WorkFolders':[str(ROOT)]}
    (folder/'state.json').write_text(json.dumps(config),encoding='utf-8')
    pipe='QiDesk-Productivity-'+uuid.uuid4().hex
    host=subprocess.Popen([app,'--mcp-fixture',str(folder),'--pipe',pipe],creationflags=subprocess.CREATE_NO_WINDOW)
    checks=[]
    try:
        await asyncio.sleep(.8)
        async with stdio_client(StdioServerParameters(command=bridge,args=['--mcp','--pipe',pipe])) as (r,w):
            async with ClientSession(r,w) as client:
                await client.initialize()
                async def call(name,args=None,error=False):
                    result=await client.call_tool(name,args or {})
                    assert bool(result.isError)==error,result
                    return None if error else json.loads(result.content[0].text)
                current=await call('qidesk_get_settings')
                assert 'ClipboardEnabled' not in current['settings']
                checks.append('PASS: AI settings exclude deferred clipboard controls and contents')
                await call('qidesk_update_group',{'groupId':'review','settings':{'Color':'#92AAE5','TintStrength':.7,'BackgroundOpacity':0,'ViewMode':'详细信息','Sort':'大小','SortDescending':True}})
                saved=json.loads((folder/'state.json').read_text(encoding='utf-8-sig'))
                assert saved['Groups'][0]['Color']=='#92AAE5' and saved['Groups'][0]['BackgroundOpacity']==0
                checks.append('PASS: real MCP changes typed color transparency view and sort settings')
                await call('qidesk_update_settings',{'settings':{'PortalNavigate':True,'HideOriginalIcons':True,'SnapGrid':16}})
                current=await call('qidesk_get_settings')
                assert current['settings']['SnapGrid']==16
                checks.append('PASS: real MCP persists global desktop settings')
                await call('qidesk_update_group',{'groupId':'review','settings':{'Color':'#FFFFFF','IconSize':999}},error=True)
                current=await call('qidesk_get_settings')
                assert current['groups'][0]['settings']['Color']=='#92AAE5'
                checks.append('PASS: rejected AI mutation leaves previous appearance intact')
                await call('qidesk_update_settings',{'settings':{'ClipboardEnabled':True}},error=True)
                checks.append('PASS: paused clipboard feature rejects remote activation')
                found=await call('qidesk_search_files',{'query':'README.md','workspaceId':'0'})
                assert found['Provider']=='Everything' and any(pathlib.Path(x['Path'])==ROOT/'README.md' for x in found['Items']),found
                assert all(str(ROOT).lower() in x['Path'].lower() for x in found['Items'])
                checks.append('PASS: real Everything index query through MCP finds workspace README')
    finally:
        host.terminate();host.wait(timeout=5)
        (ROOT/'dist/productivity-mcp-results.txt').write_text('\n'.join(checks),encoding='utf-8')
    print('\n'.join(checks))

if __name__=='__main__':asyncio.run(main())
