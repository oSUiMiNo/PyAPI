using Maku;
using UnityEngine;
using System;


public class EnvSetter : MonoBehaviour
{
    async void Start()
    {
        await PyEnvSetter.Exe($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        await VEnvSetter.Exe($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}