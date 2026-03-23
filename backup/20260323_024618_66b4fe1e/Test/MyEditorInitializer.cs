# if UNITY_EDITOR
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
        //    Debug.Log("MyEditorInitializerが初期化されました。");

        //    EditorApplication.delayCall += OnEditorStartup;

        //    EditorPrefs.SetBool("MyEditorInitializer_Initialized", true);
        //}
    }

    static void OnEditorStartup()
    {
        Debug.Log("Editor起動後の処理を実行");
        string savedValue = EditorPrefs.GetString("MySavedValue", "デフォルト値");
        Debug.Log("保存された値：" + savedValue);
    }
}
#endif