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
    //    // �A�Z�b�g�̕ύX�Ɋւ�炸�A��ɃR�s�[�����݂�
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
            Debug.LogError("�\�[�X�t�@�C����������܂���: " + SourceFile);
            return;
        }
        try
        {
            // �ۑ��悪���݂��Ȃ���΍쐬
            if (!Directory.Exists(Path.GetDirectoryName(DestFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DestFile));
                AssetDatabase.Refresh();
            }

            File.Copy(SourceFile, DestFile, true); // true�ŏ㏑��������
            AssetDatabase.Refresh();
            //Debug.Log("�t�@�C�����R�s�[���܂���: " + destinationFilePath);
        }
        catch (IOException e)
        {
            // �R�s�[�Ɏ��s�����ꍇ�ł��A�G���[���b�Z�[�W��\������݂̂ŏ����𑱍s
            Debug.LogWarning("�t�@�C���̃R�s�[�Ɏ��s���܂���: " + e.Message);
        }
    }
}
#endif