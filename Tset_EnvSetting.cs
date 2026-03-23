using UnityEngine;


public class Tset_EnvSetting : MonoBehaviour
{
    async void Start()
    {
        await UvSetter.Exe($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
    }
}