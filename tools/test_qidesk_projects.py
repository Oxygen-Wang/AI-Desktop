"""Real MCP roots / explicit-workspace integration using isolated data."""
import asyncio
import json
import pathlib
import subprocess
import uuid
from mcp import ClientSession, StdioServerParameters, types
from mcp.client.stdio import stdio_client

ROOT = pathlib.Path(__file__).resolve().parents[1]

async def main():
    app = (ROOT / "dist/latest.txt").read_text(encoding="utf-8-sig").strip()
    bridge = (ROOT / "dist/latest-mcp.txt").read_text(encoding="utf-8-sig").strip()
    fixture = ROOT / "dist" / ("mcp-fixture-" + uuid.uuid4().hex)
    alpha = fixture / "Project Alpha"
    beta = fixture / "Project Beta"
    alpha.mkdir(parents=True)
    beta.mkdir()
    (alpha / ".gitignore").write_text("*.tmp\n!keep.tmp\n", encoding="utf-8")
    (alpha / ".cursorignore").write_text("private/\n", encoding="utf-8")
    (beta / ".gitignore").write_text("node_modules/\n", encoding="utf-8")
    config = {"Version": 2, "McpEnabled": True, "Groups": [{"Id": "fixture-a", "Name": "A"}], "Rules": []}
    (fixture / "state.json").write_text(json.dumps(config), encoding="utf-8")
    pipe = "QiDesk-Roots-Test-" + uuid.uuid4().hex
    host = subprocess.Popen([app, "--mcp-fixture", str(fixture), "--pipe", pipe], creationflags=subprocess.CREATE_NO_WINDOW)
    results = []
    roots = [alpha]
    async def list_roots(_):
        return types.ListRootsResult(roots=[types.Root(uri=p.as_uri(), name=p.name) for p in roots])
    async def call(session, name, args=None):
        result = await session.call_tool(name, args or {})
        assert not result.isError, result
        return json.loads(result.content[0].text)
    async def wait_projects(session, expected):
        for _ in range(30):
            projects = await call(session, "qidesk_list_workspaces")
            if len(projects) == expected:
                return projects
            await asyncio.sleep(0.1)
        raise AssertionError(f"Expected {expected} projects, got {projects}")
    try:
        await asyncio.sleep(0.7)
        async with stdio_client(StdioServerParameters(command=bridge, args=["--mcp", "--pipe", pipe])) as (r, w):
            async with ClientSession(r, w, list_roots_callback=list_roots) as session:
                info = await session.initialize()
                assert info.serverInfo.version == "0.6.0"
                projects = await wait_projects(session, 1)
                assert projects[0]["name"] == "Project Alpha" and "MCP Roots" in projects[0]["source"]
                results.append("PASS: client roots automatically register current project without a tool call")
                saved = json.loads((fixture / "state.json").read_text(encoding="utf-8-sig"))
                assert len(saved["IgnoreFiles"]) == 2
                results.append("PASS: connected project automatically associates Git and Cursor ignore files")
                roots[:] = [beta]
                await session.send_roots_list_changed()
                projects = await wait_projects(session, 2)
                assert any(p["name"] == "Project Beta" for p in projects)
                results.append("PASS: roots changed notification discovers a newly opened project")
                imported = await call(session, "qidesk_import_ignore_file", {"workspaceId": "0", "fileName": ".gitignore", "groupId": "fixture-a"})
                assert imported["imported"] == 2
                results.append("PASS: MCP imports project ignore file with local scope")
                widget = await call(session, "qidesk_add_widget", {"pluginId": "calendar"})
                assert widget["widgetId"] and len(await call(session, "qidesk_list_widgets")) == 1
                results.append("PASS: MCP creates an installed calendar widget and reads its instance")
        # New fixture state checks clients without the roots feature, using the same stable wrapper option.
        fallback = fixture / "Fallback Project"
        fallback.mkdir()
        async with stdio_client(StdioServerParameters(command=bridge, args=["--mcp", "--pipe", pipe, "--workspace", str(fallback)])) as (r, w):
            async with ClientSession(r, w) as session:
                await session.initialize()
                projects = await wait_projects(session, 3)
                assert any(p["name"] == "Fallback Project" for p in projects)
                results.append("PASS: explicit launch workspace works for clients without roots")
                extra = fixture / "Explicit Project"
                extra.mkdir()
                connected = await call(session, "qidesk_connect_workspace", {"path": str(extra), "source": "Agent current project"})
                assert connected["connected"] and len(await call(session, "qidesk_list_workspaces")) == 4
                results.append("PASS: agent tool can connect current directory without editing client configuration")
                result = await session.call_tool("qidesk_import_ignore_file", {"workspaceId": "0", "fileName": "../state.json"})
                assert result.isError
                results.append("PASS: ignore import rejects paths outside known project ignore files")
    finally:
        host.terminate()
        host.wait(timeout=5)
        (ROOT / "dist/project-mcp-results.txt").write_text("\n".join(results), encoding="utf-8")
    print("\n".join(results))

if __name__ == "__main__":
    asyncio.run(main())
