# if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;


public class PyCreator : Editor
{
    // Python�X�N���v�g��ۑ�����t�H���_�̃p�X (StreamingAssets�t�H���_��)
    static string Dir = Application.dataPath + "/StreamingAssets/Py";
    static string PyAssetsDir = "Assets/StreamingAssets/Py"; // AssetDatabase�p�̃p�X


    [MenuItem("Assets/Create/Python Script", false, 10)] // �D�揇��10
    public static void CreateNewPythonScript()
    {
        // �t�H���_�����݂��Ȃ��ꍇ�͍쐬
        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh(); // �t�H���_�쐬��AAssetDatabase���X�V
        }

        // �V�����t�@�C�������擾 (��: NewPythonScript.py)
        string fileName = "_.py";
        string filePath = Path.Combine(Dir, fileName);
        string assetFilePath = Path.Combine(PyAssetsDir, fileName); // AssetDatabase�p�̃p�X

        // �t�@�C�������ɑ��݂���ꍇ�͘A�Ԃ�t�^
        int count = 1;
        while (File.Exists(filePath))
        {
            fileName = $"_{count}.py";
            filePath = Path.Combine(Dir, fileName);
            assetFilePath = Path.Combine(PyAssetsDir, fileName); // AssetDatabase�p�̃p�X
            count++;
        }

        // ��̃t�@�C�����쐬
        File.Create(filePath).Close();

        // �����R�[�h���������� (from PyAPI import APIIn, APIOut ��ǉ�)
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("from PyAPI import APIn, APOut, Log ");
            writer.WriteLine("inJO = APIn()");
            writer.WriteLine("");
            writer.WriteLine("if __name__ == \"__main__\":");
            writer.WriteLine("    ");
        }

        // AssetDatabase�����t���b�V��
        AssetDatabase.Refresh();

        // �쐬�����t�@�C����I��
        Object obj = AssetDatabase.LoadAssetAtPath(assetFilePath, typeof(Object));
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);

        EditorUtility.FocusProjectWindow();
    }


    //[MenuItem("Assets/Create/Python Script", false, 1)] // �D�揇��10
    //public static void CreateNewPythonScript()
    //{
    //    //--------------------------------------
    //    // �t�H���_�����݂��Ȃ��ꍇ�͍쐬
    //    //--------------------------------------
    //    if (!AssetDatabase.IsValidFolder(Dir))
    //    {
    //        var parent = "Assets/StreamingAssets";
    //        if (!AssetDatabase.IsValidFolder(parent))
    //            AssetDatabase.CreateFolder("Assets", "StreamingAssets");
    //        AssetDatabase.CreateFolder(parent, "Py");
    //    }

    //    //--------------------------------------
    //    // �V�����t�@�C�������擾 (��: NewPythonScript.py)
    //    // �� GenerateUniqueAssetPath�ŘA�ԏ�����C����
    //    //--------------------------------------
    //    var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(Dir, "_.py"));

    //    //--------------------------------------
    //    // ��̃t�@�C�����쐬 �� ���g���ꔭ�ŏ�������
    //    // (File.Create�Ɠ�x����������Ė������t���b�V����h�~)
    //    //--------------------------------------
    //    var fullPath = Path.GetFullPath(assetPath);
    //    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

    //    //--------------------------------------
    //    // �����R�[�h���������� (from PyAPI import APIIn, APIOut ��ǉ�)
    //    //--------------------------------------
    //    using (StreamWriter writer = new StreamWriter(fullPath))
    //    {
    //        writer.WriteLine("from PyAPI import APIn, APOut, Log, Idle ");
    //        writer.WriteLine("inJO = APIn()");
    //        writer.WriteLine("");
    //        writer.WriteLine("if __name__ == \"__main__\":");
    //        writer.WriteLine("    ");
    //    }

    //    //--------------------------------------
    //    // AssetDatabase���X�V
    //    // �� StartAssetEditing�ŃC���|�[�g���ꎞ��~���Ă���Import
    //    //--------------------------------------
    //    AssetDatabase.StartAssetEditing();
    //    try
    //    {
    //        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    //    }
    //    finally
    //    {
    //        AssetDatabase.StopAssetEditing();
    //    }

    //    //--------------------------------------
    //    // �쐬�����t�@�C����I������Project�E�B���h�E�ŕ\��
    //    //--------------------------------------
    //    var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
    //    Selection.activeObject = obj;
    //    EditorGUIUtility.PingObject(obj);
    //    EditorUtility.FocusProjectWindow();
    //}
}
#endif
