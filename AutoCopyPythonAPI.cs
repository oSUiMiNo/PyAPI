# if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;


[InitializeOnLoad]
public class AutoCopyPythonAPI : AssetPostprocessor
{
    public static string PJT => Application.dataPath.Replace("/Assets", "");
    public static string SourceFile => $"{PJT}/Packages/jp.maku.maku_utillity/PythonAPI/PythonAssets/PyAPI.py";
    public static string DestFile => $"{Application.dataPath}/PythonAssets/PyAPI.py";

    //static string destDirName = "PythonAssets";
    //static string destFileName = "PyAPI.py";
    //private const string sourceFilePath = "Packages/jp.maku.maku_utillity/PythonAPI/PythonAssets/PyAPI.py";
    //private const string destinationFolderName = "PythonAssets";
    //private const string destinationFileName = "PyAPI.py";

    static AutoCopyPythonAPI()
    {
        CopyAsset();
        //Debug.Log($"{PJT}");
    }

    //private static void OnPostprocessAllAssets(
    //    string[] importedAssets,
    //    string[] deletedAssets,
    //    string[] movedAssets,
    //    string[] movedFromAssetPaths
    //)
    //{
    //    // アセットの変更に関わらず、常にコピーを試みる
    //    CopyAsset();
    //}

    static void CopyAsset()
    {
        //string projectPath = Application.dataPath.Replace("/Assets", "");
        //string destinationFolderPath = Path.Combine(Application.dataPath, destinationFolderName);
        //string destinationFilePath = Path.Combine(destinationFolderPath, destinationFileName);
        //string fullSourcePath = Path.Combine(projectPath, sourceFilePath);

        if (!File.Exists(SourceFile))
        {
            Debug.LogError("ソースファイルが見つかりません: " + SourceFile);
            return;
        }
        try
        {
            // 保存先が存在しなければ作成
            if (!Directory.Exists(Path.GetDirectoryName(DestFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DestFile));
                AssetDatabase.Refresh();
            }

            File.Copy(SourceFile, DestFile, true); // trueで上書きを許可
            AssetDatabase.Refresh();
            //Debug.Log("ファイルをコピーしました: " + destinationFilePath);
        }
        catch (IOException e)
        {
            // コピーに失敗した場合でも、エラーメッセージを表示するのみで処理を続行
            Debug.LogWarning("ファイルのコピーに失敗しました: " + e.Message);
        }
    }
}
#endif