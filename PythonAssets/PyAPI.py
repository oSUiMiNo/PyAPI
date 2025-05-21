import sys
import json
import os
import time
from inspect import stack
from pathlib import Path
from typing import List
from concurrent.futures import ThreadPoolExecutor


# 更新するファイルのパス
LogPath = ""
RootPah = ""
OutPath = ""
InPath = ""


def APInit():
     # UTF-8 再設定 (必要に応じて)
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding='utf-8')
        sys.stdout.reconfigure(encoding='utf-8')
        
    global LogPath, RootPah, OutPath
    # ルートパス一応保存しとく
    RootPah = Path.cwd()
    # Assets 直下にログ用txtファイルがある
    LogPath = f"{Path.cwd()}/Assets/PyLog.txt"
    # 自分が配置されているディレクトリに移動
    os.chdir(os.path.dirname(os.path.abspath(__file__)))



def APIn():
    global OutPath, InPath
    # アウトプット用にC#によって呼び出し元.pyと同じディレクトリに [自分のファイル名.json] が作成されている。
    OutPath = f"{Path(stack()[1].filename).with_suffix(".txt")}"
    APInit()
    if len(sys.argv) > 1:
            try:
                arg = sys.argv[1]
                inJO = json.loads(arg)
                if(inJO["LargeInput"] == True):
                    Log(f"ラージインプット")
                    InPath = inJO["InPath"]
                    Log(f"{InPath}")
                    try:
                        with open(InPath, 'r') as f:
                            arg = f.read()
                            Log(f"巨大引数 {arg}")
                        with open(InPath, 'w') as f:
                            pass  # ファイルを空にする
                        os.remove(InPath)
                        Log(f"巨大引数ファイル削除成功: {InPath}")
                    except FileNotFoundError:
                        Log(f"巨大引数ファイルが存在しない: {InPath}")
                    except Exception as e:
                        Log(f"エラー: {e}")
                inJO = json.loads(arg)
                return inJO
            except json.JSONDecodeError as e:
                Log(f"JSONデコードエラー: {e}")
            except Exception as e:
                Log(f"エラー: {e}")
    # else:
        # Log("外部からの引数無し")



# def APOut(outputJobj):
#      outputJson = json.dumps(outputJobj, ensure_ascii=False)
#      Log(f"JSON_OUTPUT_START{outputJson}JSON_OUTPUT_END") # プレフィックスとサフィックスで囲む
def APOut(outJO):
    if os.path.exists(OutPath):
        outJ = json.dumps(outJO, ensure_ascii=False)
        with open(OutPath, "a", encoding="utf-8") as file:
            file.write( f"___\n{outJ}\n")
    else:
        return outJO



def Log(msg):
    if os.path.exists(LogPath):
        # スタックの呼び出し元情報を取得（スタックの1つ上）
        callerFrame = stack()[1]
        # 呼び出し元ファイルの相対パス
        callerFile = Path(callerFrame.filename).relative_to(RootPah).as_posix()
        # 呼び出し元の行番号
        callerLine = callerFrame.lineno
        # ファイルに追記
        with open(LogPath, "a", encoding="utf-8") as file:
            file.write( 
                        f"___\n{msg}\n"
                        f"(at ./{callerFile}:{callerLine})")
    else:
        # ファイルが存在しない場合はコンソールに出力
        print(msg)



def Idle(fnc, inJO):
    outJO = {}
    outJO["Loaded"] = True
    APOut(outJO)
    threadCount = inJO["ThreadCount"]
    # Log(f"スレッド数 {threadCount}")
    # メインループ C#からの入力を待機
    with ThreadPoolExecutor(max_workers=threadCount) as executor:
        for arg in sys.stdin:
            if arg.strip() == "Close":
                break
            
            executor.submit(Exe, arg, fnc)
            time.sleep(0.001)
    

        
def Exe(arg, fnc):
    try:
        inJO: dict = json.loads(arg.strip())
        # Log(f"受け取ったデータ: {inJO}")
        # Log(f"受取ったデータタイプ {type(inJO)}")
        # Log(f"受取った数 {len(inJO["Data"])}")
        fnc(inJO)
    except json.JSONDecodeError:
        Log(f"JSONパースエラー {arg.strip()}")
    except Exception as e:
        exception_type, exception_object, exception_traceback = sys.exc_info()
        fileName = exception_traceback.tb_frame.f_code.co_filename
        lineNo = exception_traceback.tb_lineno
        Log(f"エラー:, {e} {fileName} _ {lineNo}")