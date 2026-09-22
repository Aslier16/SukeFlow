# -*- coding: utf-8 -*-
"""抓取一次加载的全部网络请求，用于判断线上部署是否为新版本。"""
import asyncio
import json
import sys
import urllib.request

import websockets

URL = sys.argv[1]
PORT = 9225
NEEDLE = sys.argv[2] if len(sys.argv) > 2 else "SukeFlow"


async def main():
    pages = [t for t in json.load(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json")) if t.get("type") == "page"]
    async with websockets.connect(pages[0]["webSocketDebuggerUrl"], max_size=64 * 1024 * 1024) as ws:
        mid = 0
        events = []

        async def send(method, params=None):
            nonlocal mid
            mid += 1
            await ws.send(json.dumps({"id": mid, "method": method, "params": params or {}}))
            while True:
                msg = json.loads(await ws.recv())
                if msg.get("id") == mid:
                    return msg.get("result")
                if "method" in msg:
                    events.append(msg)

        await send("Page.enable")
        await send("Network.enable")
        await send("Network.setCacheDisabled", {"cacheDisabled": True})
        await send("Page.navigate", {"url": URL})

        for _ in range(120):
            await asyncio.sleep(1)
            done = sum(1 for e in events if e["method"] == "Network.loadingFinished")
            if done > 5 and any("dotnet" in json.dumps(e) for e in events):
                break

        urls, status = {}, {}
        for e in events:
            if e["method"] == "Network.requestWillBeSent":
                rid = e["params"]["requestId"]
                urls[rid] = e["params"]["request"]["url"]
            elif e["method"] == "Network.responseReceived":
                status[e["params"]["requestId"]] = e["params"]["response"]["status"]

        print(f"共 {len(urls)} 个请求；非 200 的：")
        for rid, u in urls.items():
            if status.get(rid) not in (200, 304):
                print(f"  {status.get(rid)} {u}")
        print(f"\n匹配 {NEEDLE} 的请求：")
        for rid, u in urls.items():
            if NEEDLE.lower() in u.lower():
                print(f"  {status.get(rid)} {u}")
        return 0


sys.exit(asyncio.run(main()))
