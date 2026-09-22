# -*- coding: utf-8 -*-
"""诊断：检查页面/画布背景色，并试试给 body 加背景后重绘。"""
import asyncio
import base64
import json
import sys
import urllib.request

import websockets

URL = "https://aslier16.github.io/SukeFlow/"
PORT = 9225


async def main():
    pages = [t for t in json.load(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json")) if t.get("type") == "page"]
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
        await send("Page.navigate", {"url": URL})
        for _ in range(120):
            await asyncio.sleep(1)
            if await evaljs("!!document.querySelector('canvas.avalonia-canvas') && document.querySelector('.avalonia-splash')?.classList.contains('splash-close')"):
                break
        await asyncio.sleep(3)

        print("body 背景 =", await evaljs("getComputedStyle(document.body).backgroundColor"))
        print("html 背景 =", await evaljs("getComputedStyle(document.documentElement).backgroundColor"))
        print("canvas 背景 =", await evaljs("getComputedStyle(document.querySelector('canvas.avalonia-canvas')).backgroundColor"))
        print("prefers-dark =", await evaljs("matchMedia('(prefers-color-scheme: dark)').matches"))

        await evaljs("document.body.style.background='#E5E7F5'; document.documentElement.style.background='#E5E7F5'")
        await asyncio.sleep(1)
        shot = await send("Page.captureScreenshot", {"format": "png"})
        open(r"E:\__C#Repo__\SukeFlow\.pages-live-bg.png", "wb").write(base64.b64decode(shot["data"]))
        print("截图(加背景后) = .pages-live-bg.png")
        return 0


sys.exit(asyncio.run(main()))
