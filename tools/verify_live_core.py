"""Inspect the live app through its public MCP tools; never reads clipboard payloads."""
import asyncio,json,pathlib
from mcp import ClientSession,StdioServerParameters,types
from mcp.client.stdio import stdio_client
ROOT=pathlib.Path(__file__).resolve().parents[1]
async def main():
    bridge=(ROOT/'dist/latest-mcp.txt').read_text(encoding='utf-8-sig').strip()
    async def roots(_):return types.ListRootsResult(roots=[types.Root(uri=ROOT.as_uri(),name='当前项目')])
    async with stdio_client(StdioServerParameters(command=bridge,args=['--mcp'])) as (r,w):
        async with ClientSession(r,w,list_roots_callback=roots) as client:
            info=await client.initialize()
            async def call(name,args=None):
                result=await client.call_tool(name,args or {})
                assert not result.isError,result
                return json.loads(result.content[0].text)
            settings=await call('qidesk_get_settings')
            groups=await call('qidesk_list_groups');widgets=await call('qidesk_list_widgets')
            found=await call('qidesk_search_files',{'scope':'all','query':'path:D:\\fences README.md'})
            tools=await client.list_tools()
            current=await call('qidesk_get_current_workspace')
            status={'version':info.serverInfo.version,'tools':len(tools.tools),'groups':len(groups),'widgets':len(widgets),'hideOriginalIcons':settings['settings']['HideOriginalIcons'],'portalNavigate':settings['settings']['PortalNavigate'],'clipboardControlsPresent':'ClipboardEnabled' in settings['settings'],'currentProject':current['name'],'searchProvider':found['Provider'],'searchFound':any(pathlib.Path(x['Path'])==ROOT/'README.md' for x in found['Items'])}
            assert status['version']=='0.6.0' and status['hideOriginalIcons'] and status['portalNavigate'] and status['searchFound'] and not status['clipboardControlsPresent'],status
            (ROOT/'dist/live-core-check.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
            print(json.dumps(status,ensure_ascii=True))
if __name__=='__main__':asyncio.run(main())
