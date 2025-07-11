using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;


public static class VEnvSetter
{
    //================================================
    // (1) pyenv exec で .venv を作成  
    // (2) venv 内 python で requirements.txt をインストール
    // 前提：
    // - dir に pyenv local が既に設定済み
    // - dir に requirements.txt がある
    //================================================
    public static async UniTask ExeFlow(string dir)
    {
        Debug.Log($"VEnv セットアップ開始...\n{dir}");
        //-------------------------------
        // .venv が無ければ作成
        //-------------------------------
        string venvDir = $"{dir}/.venv";
        if (!Directory.Exists(venvDir))
        {
            Debug.Log($".venv を生成...");
            // 環境変数を気にせず pyenv 経由で python を起動してコマンドを実行
            await CommandUtil.ExeToolCommand("pyenv exec python -m venv .venv", dir);
        }
        else
            Debug.Log(".venv は既に存在する");
        //-------------------------------
        // requirements.txt が無ければスキップ
        //-------------------------------
        string libList = $"{dir}/requirements.txt";
        if (!File.Exists(libList))
        {
            Debug.LogWarning($"{libList} が無いのでインストールをスキップ");
            return;
        }
        //-------------------------------
        // venv 内 python のフルパスを組み立て
        //-------------------------------
        string venvPy = $"{venvDir}/Scripts/python.exe";
        if (!File.Exists(venvPy))
            throw new FileNotFoundException($"仮想環境の python.exe が見つからない: {venvPy}");
        //-------------------------------
        // 既に同一環境ならスキップ
        //-------------------------------
        if (await IsAlreadySatisfiedAsync(venvPy, libList, dir))
        {
            Debug.Log("requirements.txt と同一環境が既に構築済み。インストールをスキップ");
            return;
        }
        //-------------------------------
        // requirements.txt があれば全ライブラリインストール
        //-------------------------------
        Debug.Log("リストをもとに全ライブラリをインストール...");
        await CommandUtil.ExeToolCommand(
            $"{venvPy} -m pip install -r requirements.txt",
            dir);
        Debug.Log($"VEnv セットアップ完了\n{dir}");
    }


    //================================================
    // requirements.txt と venv 内 pip freeze を比較
    //================================================
    static async UniTask<bool> IsAlreadySatisfiedAsync(string venvPy, string reqPath, string dir)
    {
        // A) requirements.txt を整形
        var required = File.ReadLines(reqPath)
                           .Select(l => l.Split('#')[0].Trim())       // コメント除去
                           .Where(l => !string.IsNullOrWhiteSpace(l)) // 空行除去
                           .Select(NormalizePkg)
                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // B) venv の現状を取得
        string freeze = await CommandUtil.ExeToolCommand($"{venvPy} -m pip freeze", dir);
        var installed = freeze.Split('\n')
                              .Select(l => l.Trim())
                              .Where(l => !string.IsNullOrWhiteSpace(l))
                              .Select(NormalizePkg)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // C) 差分チェック
        bool ok = required.IsSubsetOf(installed);
        if (!ok)
        {
            var missing = required.Except(installed);
            Debug.Log($"未インストールのパッケージ: {string.Join(", ", missing)}");
        }
        return ok;
    }

    // 「Package==Version」まで合わせて比較したいので正規化        ★
    private static string NormalizePkg(string line) =>
        line.Replace(" ", "").Replace("\r", "").ToLowerInvariant();
}
