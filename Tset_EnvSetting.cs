using UnityEngine;


public class Tset_EnvSetting : MonoBehaviour
{
    async void Start()
    {
        await PyEnvSetter.Exe($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        await VEnvSetter.Exe($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}