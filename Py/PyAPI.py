import sys
import json
import os
import socket
import struct
import threading
import time
from inspect import stack
from pathlib import Path
from typing import List
from concurrent.futures import ThreadPoolExecutor


RootPah = ""
Conn = None
SendLock = threading.Lock()


def recv_exact(conn, n):
    buf = b""
    while len(buf) < n:
        chunk = conn.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("接続が切断された")
        buf += chunk
    return buf


def send_message(conn, data):
    body = json.dumps(data, ensure_ascii=False).encode("utf-8")
    header = struct.pack(">I", len(body))
    with SendLock:
        conn.sendall(header + body)


def recv_message(conn):
    header = recv_exact(conn, 4)
    length = struct.unpack(">I", header)[0]
    body = recv_exact(conn, length)
    return json.loads(body.decode("utf-8"))


def APInit():
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8")
        sys.stdout.reconfigure(encoding="utf-8")

    global RootPah, Conn
    RootPah = Path.cwd()
    os.chdir(os.path.dirname(os.path.abspath(__file__)))

    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("127.0.0.1", 0))
    port = server.getsockname()[1]
    server.listen(1)
    print(f"PORT:{port}", flush=True)
    Conn, _ = server.accept()
    server.close()


def APIn():
    APInit()
    if len(sys.argv) > 1:
        try:
            arg = sys.argv[1]
            inJO = json.loads(arg)
            if inJO.get("LargeInput") == True:
                Log("ラージインプット")
                inPath = inJO["InPath"]
                Log(f"{inPath}")
                try:
                    with open(inPath, "r") as f:
                        arg = f.read()
                        Log(f"巨大引数 {arg}")
                    with open(inPath, "w") as f:
                        pass
                    os.remove(inPath)
                    Log(f"巨大引数ファイル削除成功: {inPath}")
                except FileNotFoundError:
                    Log(f"巨大引数ファイルが存在しない: {inPath}")
                except Exception as e:
                    Log(f"エラー: {e}")
            inJO = json.loads(arg)
            return inJO
        except json.JSONDecodeError as e:
            Log(f"JSONデコードエラー: {e}")
        except Exception as e:
            Log(f"エラー: {e}")


def APOut(outJO):
    msg = {"_type": "out"}
    msg.update(outJO)
    send_message(Conn, msg)


def Log(msg):
    if Conn is None:
        print(msg)
        return
    try:
        callerFrame = stack()[1]
        callerFile = Path(callerFrame.filename).relative_to(RootPah).as_posix()
        callerLine = callerFrame.lineno
        send_message(Conn, {
            "_type": "log",
            "_msg": str(msg),
            "_src": f"{callerFile}:{callerLine}"
        })
    except Exception:
        print(msg)


def Idle(fnc, inJO):
    send_message(Conn, {"_type": "loaded"})
    threadCount = inJO["ThreadCount"]
    with ThreadPoolExecutor(max_workers=threadCount) as executor:
        while True:
            try:
                msg = recv_message(Conn)
            except ConnectionError:
                break
            if msg.get("_type") == "close":
                break
            executor.submit(Exe, msg, fnc)
            time.sleep(0.001)


def Exe(msg, fnc):
    try:
        inJO = {k: v for k, v in msg.items() if k != "_type"}
        fnc(inJO)
    except Exception as e:
        exception_type, exception_object, exception_traceback = sys.exc_info()
        fileName = exception_traceback.tb_frame.f_code.co_filename
        lineNo = exception_traceback.tb_lineno
        Log(f"エラー:, {e} {fileName} _ {lineNo}")
