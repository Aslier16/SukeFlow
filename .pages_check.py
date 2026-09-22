# -*- coding: utf-8 -*-
"""线上（GitHub Pages）端到端验证：加载 → 等待就绪 → 截图 → localStorage/错误检查。"""
import asyncio
import base64
import json
import sys
import urllib.request

import websockets

URL = sys.argv[1] if len(sys.argv) > 1 else "https://aslier16.github.io/SukeFlow/"
OUT = sys.argv[2] if len(sys.argv) > 2 else r"E:\__C#Repo__\SukeFlow\.pages-live.png"
PORT = 9225


async def main():
    pages = []
    for _ in range(40):
        try:
            data = json.load(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json"))
            pages = [t for t in data if t.get("type") == "page"]
            if pages:
                break
        except Exception:
            pass
        await asyncio.sleep(1)
    if not pages:
        print("FAIL: 无法连接 CDP")
        return 1

    async with websockets.connect(pages[0]["webSocketDebuggerUrl"], max_size=64 * 1024 * 1024) as ws:
        mid = 0

        async def send(method, params=None):
            nonlocal mid
            mid += 1
            await ws.send(json.dumps({"id": mid, "method": method, "params": params or {}}))
            while True:
                msg = json.loads(await ws.recv())
                if msg.get("id") == mid:
                    return msg.get("result")

        async def evaljs(expr):
            r = await send("Runtime.evaluate", {"expression": expr, "returnByValue": True})
            return (r or {}).get("result", {}).get("value")

        await send("Page.enable")
        await send("Runtime.enable")
        await send("Log.enable")
        if "noemu" not in sys.argv:
            await send("Emulation.setDeviceMetricsOverride",
                       {"width": 467, "height": 853, "deviceScaleFactor": 2, "mobile": True})

        t0 = asyncio.get_event_loop().time()
        await send("Page.navigate", {"url": URL})

        ready = False
        for _ in range(120):
            await asyncio.sleep(1)
            ok = await evaljs(
                "!!document.querySelector('canvas.avalonia-canvas')"
                " && !!document.querySelector('.avalonia-splash')?.classList.contains('splash-close')")
            if ok:
                ready = True
                break
        elapsed = asyncio.get_event_loop().time() - t0
        print(f"就绪={ready} 用时={elapsed:.1f}s  URL={await evaljs('location.href')}")

        await asyncio.sleep(3)
        print("画布尺寸 =", await evaljs(
            "(()=>{const c=document.querySelector('canvas.avalonia-canvas');"
            "return c?`${c.width}x${c.height}`:'无'})()"))
        print("页面标题 =", await evaljs("document.title"))
        print("localStorage 键 =", await evaljs("JSON.stringify(Object.keys(localStorage))"))
        print("字体已加载 =", await evaljs(
            "(document.fonts? [...document.fonts].filter(f=>f.status==='loaded').length : 'n/a')"))
        print("未捕获错误 =", await evaljs("window.__errs ? window.__errs : 'n/a'"))

        shot = await send("Page.captureScreenshot", {"format": "png", "captureBeyondViewport": False})
        if shot and shot.get("data"):
            open(OUT, "wb").write(base64.b64decode(shot["data"]))
            print("截图 =", OUT)
        else:
            print("FAIL: 截图失败")
        return 0 if ready else 2


sys.exit(asyncio.run(main()))
