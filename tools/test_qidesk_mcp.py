"""Exercise the real stdio MCP transport against an isolated QiDesk fixture."""
import asyncio
import json
import pathlib
import subprocess
import uuid
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = pathlib.Path(__file__).resolve().parents[1]

async def main():
    app = (ROOT / "dist/latest.txt").read_text(encoding="utf-8-sig").strip()
    bridge = (ROOT / "dist/latest-mcp.txt").read_text(encoding="utf-8-sig").strip()
    test_root = ROOT / "dist" / ("mcp-fixture-" + uuid.uuid4().hex)
    workspace = test_root / "workspace"
    workspace.mkdir(parents=True)
    generated = workspace / "任意生成结果.txt"
    generated.write_text("preserve original bytes", encoding="utf-8")
    outside = test_root / "outside.txt"
    outside.write_text("outside", encoding="utf-8")
    config = {"Version": 2, "McpEnabled": True, "WorkFolders": [str(workspace)],
              "Groups": [{"Id": "fixture-a", "Name": "Fixture A"}, {"Id": "fixture-b", "Name": "Fixture B"}], "Rules": []}
    (test_root / "state.json").write_text(json.dumps(config), encoding="utf-8")
    pipe = "QiDesk-Test-" + uuid.uuid4().hex
    host = subprocess.Popen([app, "--mcp-fixture", str(test_root), "--pipe", pipe], creationflags=subprocess.CREATE_NO_WINDOW)
    results = []
    try:
        await asyncio.sleep(0.8)
        async with stdio_client(StdioServerParameters(command=bridge, args=["--mcp", "--pipe", pipe])) as (read, write):
            async with ClientSession(read, write) as session:
                initialized = await session.initialize()
                assert initialized.serverInfo.name == "QiDesk"
                results.append("PASS: real SDK initialization")
                listed = await session.list_tools()
                assert len({tool.name for tool in listed.tools}) == len(listed.tools)
                assert {"qidesk_search_files", "qidesk_update_settings", "qidesk_update_group", "qidesk_get_settings"}.issubset({tool.name for tool in listed.tools})
                results.append("PASS: real SDK discovery of unique desktop settings and search tools")
                async def call(name, arguments=None, error=False):
                    value = await session.call_tool(name, arguments or {})
                    assert bool(value.isError) == error, value
                    return value if error else json.loads(value.content[0].text)
                groups = await call("qidesk_list_groups")
                assert len(groups) == 2 and "path" not in groups[0]
                results.append("PASS: local pipe reads live groups without physical paths")
                added = await call("qidesk_add_ignore", {"pattern": "*.tmp", "groupId": "fixture-a"})
                assert len(await call("qidesk_list_ignores", {"groupId": "fixture-a"})) == 1
                assert not await call("qidesk_list_ignores", {"groupId": "fixture-b"})
                assert not await call("qidesk_list_ignores")
                await call("qidesk_remove_ignore", {"ruleId": added["ruleId"], "groupId": "fixture-a"})
                results.append("PASS: add and remove local ignores without cross-scope effects")
                registered = await call("qidesk_register_generated", {"workspaceId": "0", "paths": [str(generated)]})
                assert registered["registered"] == 1 and generated.read_text(encoding="utf-8") == "preserve original bytes"
                results.append("PASS: arbitrary generated filename registered without changing file")
                await call("qidesk_register_generated", {"workspaceId": "0", "paths": [str(outside)]}, error=True)
                assert len(await call("qidesk_list_ignores")) == 1
                results.append("PASS: outside-workspace registration rejected atomically")
                await call("qidesk_add_ignore", {"pattern": "*.log", "unexpected": "value"}, error=True)
                results.append("PASS: schema rejects unexpected tool arguments")
                portal = await call("qidesk_create_portal", {"workspaceId": "0", "name": "Generated files"})
                assert portal["groupId"] and len(await call("qidesk_list_groups")) == 3
                await call("qidesk_create_portal", {"workspaceId": "0"}, error=True)
                assert len(await call("qidesk_list_groups")) == 3
                results.append("PASS: same-folder portal creation and duplicate rejection")
                saved = json.loads((test_root / "state.json").read_text(encoding="utf-8-sig"))
                assert len(saved["Groups"]) == 3 and len(saved["IgnoreRules"]) == 1
                results.append("PASS: MCP mutations persisted by owning application")
    finally:
        host.terminate()
        host.wait(timeout=5)
        (ROOT / "dist/mcp-test-results.txt").write_text("\n".join(results), encoding="utf-8")
    print("\n".join(results))

if __name__ == "__main__":
    asyncio.run(main())
