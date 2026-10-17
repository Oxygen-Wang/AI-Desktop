"""Read-only window diagnostics; never captures the desktop or reads file contents."""
import argparse
import ctypes
import json
from ctypes import wintypes as w
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--pid', required=True, type=int)
options = parser.parse_args()
u = ctypes.WinDLL('user32', use_last_error=True)
dwm = ctypes.WinDLL('dwmapi')
u.SetProcessDPIAware()
callback = ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
u.EnumWindows.argtypes = [callback, w.LPARAM]
u.GetWindowLongPtrW.restype = ctypes.c_ssize_t
u.GetWindowLongPtrW.argtypes = [w.HWND, ctypes.c_int]
u.GetWindow.restype = w.HWND
u.GetWindow.argtypes = [w.HWND, w.UINT]
u.GetWindowRect.argtypes = [w.HWND, ctypes.POINTER(w.RECT)]
u.IsWindowVisible.argtypes = [w.HWND]
u.GetWindowThreadProcessId.argtypes = [w.HWND, ctypes.POINTER(w.DWORD)]
u.GetClassNameW.argtypes = [w.HWND, w.LPWSTR, ctypes.c_int]
u.FindWindowExW.restype = w.HWND
u.FindWindowExW.argtypes = [w.HWND, w.HWND, w.LPCWSTR, w.LPCWSTR]
u.GetShellWindow.restype = w.HWND
dwm.DwmGetWindowAttribute.argtypes = [w.HWND, w.DWORD, ctypes.c_void_p, w.DWORD]
shell_process = w.DWORD()
u.GetWindowThreadProcessId(u.GetShellWindow(), ctypes.byref(shell_process))
virtual = [u.GetSystemMetrics(i) for i in (76, 77, 78, 79)]
records = []

def class_name(hwnd):
    value = ctypes.create_unicode_buffer(256)
    u.GetClassNameW(hwnd, value, len(value))
    return value.value

@callback
def inspect(hwnd, unused):
    pid = w.DWORD()
    u.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    cls = class_name(hwnd)
    desktop = cls in ('Progman', 'WorkerW') and pid.value == shell_process.value
    if pid.value != options.pid and not desktop:
        return True
    rect = w.RECT()
    u.GetWindowRect(hwnd, ctypes.byref(rect))
    owner = u.GetWindow(hwnd, 4)
    cloak = w.DWORD()
    cloak_ok = dwm.DwmGetWindowAttribute(hwnd, 14, ctypes.byref(cloak), ctypes.sizeof(cloak)) == 0
    view = u.FindWindowExW(hwnd, None, 'SHELLDLL_DefView', None)
    list_window = u.FindWindowExW(view, None, 'SysListView32', None) if view else None
    desktop_view = bool(list_window)
    intersects = (rect.right > virtual[0] and rect.left < virtual[0] + virtual[2]
                  and rect.bottom > virtual[1] and rect.top < virtual[1] + virtual[3])
    records.append({
        'hwnd': hex(hwnd), 'class': cls, 'pid': pid.value,
        'owner': hex(owner or 0), 'owner_class': class_name(owner) if owner else '',
        'visible': bool(u.IsWindowVisible(hwnd)), 'cloaked': cloak.value if cloak_ok else None,
        'style': hex(u.GetWindowLongPtrW(hwnd, -16) & 0xffffffff),
        'rect': [rect.left, rect.top, rect.right, rect.bottom],
        'intersects_virtual_screen': intersects, 'desktop_view': desktop_view,
        'desktop_list': hex(list_window or 0), 'desktop_list_visible': bool(list_window and u.IsWindowVisible(list_window)),
    })
    return True

u.EnumWindows(inspect, 0)
result = {'pid': options.pid, 'shell_pid': shell_process.value, 'virtual_screen': virtual, 'windows': records}
output = Path(__file__).resolve().parents[1] / 'dist/desktop-diagnostics.json'
output.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
