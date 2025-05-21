# if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class PyCreator : Editor
{
    [MenuItem("Assets/Create/Python Script", false, 1)] // 優先順位10
    public static void CreateNewPythonScript()
    {
        // Pythonスクリプトを保存するフォルダのパス (StreamingAssetsフォルダ内)
        string pythonAssetsPath = Application.dataPath + "/StreamingAssets/PythonAssets";
        string assetPath = "Assets/StreamingAssets/PythonAssets"; // AssetDatabase用のパス

        // フォルダが存在しない場合は作成
        if (!Directory.Exists(pythonAssetsPath))
        {
            Directory.CreateDirectory(pythonAssetsPath);
            AssetDatabase.Refresh(); // フォルダ作成後、AssetDatabaseを更新
        }

        // 新しいファイル名を取得 (例: NewPythonScript.py)
        string fileName = "_.py";
        string filePath = Path.Combine(pythonAssetsPath, fileName);
        string assetFilePath = Path.Combine(assetPath, fileName); // AssetDatabase用のパス

        // ファイルが既に存在する場合は連番を付与
        int count = 1;
        while (File.Exists(filePath))
        {
            fileName = $"NewPythonScript_{count}.py";
            filePath = Path.Combine(pythonAssetsPath, fileName);
            assetFilePath = Path.Combine(assetPath, fileName); // AssetDatabase用のパス
            count++;
        }

        // 空のファイルを作成
        File.Create(filePath).Close();

        // AssetDatabaseをリフレッシュ
        AssetDatabase.Refresh();

        // 作成したファイルを選択
        Object obj = AssetDatabase.LoadAssetAtPath(assetFilePath, typeof(Object));
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);

        EditorUtility.FocusProjectWindow();

        // 初期コードを書き込む (from PyAPI import APIIn, APIOut を追加)
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("from PyAPI import APIn, APOut, Log ");
            writer.WriteLine("apIn = APIn()");
            writer.WriteLine("");
            writer.WriteLine("if __name__ == \"__main__\":");
            writer.WriteLine("    ");
        }
    }
}
#endif