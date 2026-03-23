using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using Debug = UnityEngine.Debug;


public static class VEnvSetter
{
    ///==============================================<summary>
    /// (1) pyenv exec で .venv を作成  
    /// (2) venv 内 python で requirements.txt をインストール
    /// 前提：
    /// - dir に pyenv local が既に設定済み
    /// - dir に requirements.txt がある
    ///</summary>=============================================
    public static async UniTask Exe(string dir)
    {
        try
        {
            Debug.Log($"VEnv セットアップ開始...\n{dir}");
            //--------------------------------------
            // .venv が無ければ作成
            //--------------------------------------
            string venvDir = $"{dir}/.venv";
            if (!Directory.Exists(venvDir))
            {
                Debug.Log($".venv が無いので生成...");
                // 環境変数を気にせず pyenv 経由で python を起動してコマンドを実行
                await PowerShellAPI.Command("pyenv exec python -m venv .venv", dir);
            }
            //--------------------------------------
            // pyenv.cfg ファイル確認
            //--------------------------------------
            string cfgFile = $"{venvDir}/pyvenv.cfg";
            // pyenv.cfg ファイルが無ければ .venv 削除して再設定
            if (!File.Exists(cfgFile))
            {
                Debug.Log($"pyenv.cfg ファイルが無いので .venv リセット");
                Directory.Delete(venvDir);
                await PowerShellAPI.Command("pyenv exec python -m venv .venv", dir);
            }
            //--------------------------------------
            // requirements.txt が無ければスキップ
            //--------------------------------------
            string libList = $"{dir}/requirements.txt";
            if (!File.Exists(libList))
            {
                throw new($"{libList} が無いのでインストールをスキップ");
            }
            //--------------------------------------
            // venv 内 python のフルパスを組み立て
            //--------------------------------------
            string venvPy = $"{venvDir}/Scripts/python.exe";
            if (!File.Exists(venvPy))
            {
                throw new($"仮想環境の python.exe が見つからない: {venvPy}");
            }
            //--------------------------------------
            // 既に同一環境ならスキップ
            //--------------------------------------
            if (await IsAlreadySatisfiedAsync(venvPy, libList, dir))
            {
                Debug.Log("requirements.txt と同一環境が既に構築済み。インストールをスキップ");
                Debug.Log($"VEnv セットアップ完了！\n{dir}");
                return;
            }
            //--------------------------------------
            // requirements.txt があれば全ライブラリインストール
            //--------------------------------------
            Debug.Log("リストをもとに全ライブラリをインストール...");
            await PowerShellAPI.Command($"{venvPy} -m pip install -r requirements.txt", dir);
            Debug.Log($"VEnv セットアップ完了！\n{dir}");
        }
        catch { throw; }
    }


    ///==============================================<summary>
    // requirements.txt と venv 内 pip freeze を比較
    ///</summary>=============================================
    static async UniTask<bool> IsAlreadySatisfiedAsync(string venvPy, string reqPath, string dir)
    {
        // 「Package==Version」まで合わせて比較したいので正規化
        string NormalizePkg(string line) =>
            line.Replace(" ", "").Replace("\r", "").ToLowerInvariant();

        //--------------------------------------
        // requirements.txt を整形
        //--------------------------------------
        var required = File.ReadLines(reqPath)
                           .Select(l => l.Split('#')[0].Trim())       // コメント除去
                           .Where(l => !string.IsNullOrWhiteSpace(l)) // 空行除去
                           .Select(NormalizePkg)
                           .ToHashSet(StringComparer.OrdinalIgnoreCase);
        //--------------------------------------
        // venv の現状を取得
        //--------------------------------------
        // C:/Users/[ユーザ名] フォルダ
        string usersDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await UniTask.WaitUntil(() => File.Exists($"{usersDir}/.pyenv/pyenv-win/versions/3.12.5/python.exe"));
        string freeze = await PowerShellAPI.Command($"{venvPy} -m pip freeze", dir);
        var installed = freeze.Split('\n')
                              .Select(l => l.Trim())
                              .Where(l => !string.IsNullOrWhiteSpace(l))
                              .Select(NormalizePkg)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
        //--------------------------------------
        // 差分チェック
        //--------------------------------------
        bool ok = required.IsSubsetOf(installed);
        if (!ok)
        {
            var missing = required.Except(installed);
            Debug.Log($"未インストールのパッケージ：{string.Join(",\n", missing)}");
        }
        return ok;
    }


    ///==============================================<summary>
    /// cfg ファイルの home の ユーザフォルダパス部分を各々の環境に置き換え
    ///</summary>=============================================
    static void ReplaceCfg(string cfgPath)
    {
        Debug.Log($"cfg ファイルのホームパス置き換え開始...");
        string usersDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // すべての行を読み込む
        string[] lines = File.ReadAllLines(cfgPath);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("home ="))
            {
                lines[i] = @$"home = {usersDir}\.pyenv\pyenv-win\versions\3.12.5\python.exe";
            }
        }
        // 上書き保存
        File.WriteAllLines(cfgPath, lines);
        Debug.Log($"cfg ファイルのホームパス置き換え完了！");
    }
}
