# if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class PyCreator : Editor
{
    [MenuItem("Assets/Create/Python Script", false, 1)] // �D�揇��10
    public static void CreateNewPythonScript()
    {
        // Python�X�N���v�g��ۑ�����t�H���_�̃p�X (StreamingAssets�t�H���_��)
        string pythonAssetsPath = Application.dataPath + "/StreamingAssets/PythonAssets";
        string assetPath = "Assets/StreamingAssets/PythonAssets"; // AssetDatabase�p�̃p�X

        // �t�H���_�����݂��Ȃ��ꍇ�͍쐬
        if (!Directory.Exists(pythonAssetsPath))
        {
            Directory.CreateDirectory(pythonAssetsPath);
            AssetDatabase.Refresh(); // �t�H���_�쐬��AAssetDatabase���X�V
        }

        // �V�����t�@�C�������擾 (��: NewPythonScript.py)
        string fileName = "_.py";
        string filePath = Path.Combine(pythonAssetsPath, fileName);
        string assetFilePath = Path.Combine(assetPath, fileName); // AssetDatabase�p�̃p�X

        // �t�@�C�������ɑ��݂���ꍇ�͘A�Ԃ�t�^
        int count = 1;
        while (File.Exists(filePath))
        {
            fileName = $"NewPythonScript_{count}.py";
            filePath = Path.Combine(pythonAssetsPath, fileName);
            assetFilePath = Path.Combine(assetPath, fileName); // AssetDatabase�p�̃p�X
            count++;
        }

        // ��̃t�@�C�����쐬
        File.Create(filePath).Close();

        // AssetDatabase�����t���b�V��
        AssetDatabase.Refresh();

        // �쐬�����t�@�C����I��
        Object obj = AssetDatabase.LoadAssetAtPath(assetFilePath, typeof(Object));
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);

        EditorUtility.FocusProjectWindow();

        // �����R�[�h���������� (from PyAPI import APIIn, APIOut ��ǉ�)
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