using UnityEditor;
using UnityEngine;


[InitializeOnLoad]
public class MyEditorInitializer
{
    static bool initialized;

    static MyEditorInitializer()
    {
        //if (!EditorPrefs.GetBool("MyEditorInitializer_Initialized", false))
        //{
        //    Debug.Log("MyEditorInitializer������������܂����B");

        //    EditorApplication.delayCall += OnEditorStartup;

        //    EditorPrefs.SetBool("MyEditorInitializer_Initialized", true);
        //}
    }

    static void OnEditorStartup()
    {
        Debug.Log("Editor�N����̏��������s");
        string savedValue = EditorPrefs.GetString("MySavedValue", "�f�t�H���g�l");
        Debug.Log("�ۑ����ꂽ�l�F" + savedValue);
    }
}