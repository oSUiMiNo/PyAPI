using MyUtil;
using UnityEngine;


public class EnvSetter : MonoBehaviour
{
    async void Start()
    {
        //任意のフォルダパスを指定
        await PyEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        await VEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}