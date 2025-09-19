using Maku;
using UnityEngine;
using System;


public class EnvSetter : MonoBehaviour
{
    async void Start()
    {
        await PyEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        await VEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}