using MyUtil;
using UnityEngine;
using System;


public class EnvSetter : MonoBehaviour
{
    async void Start()
    {
        //try
        //{
        //    //任意のフォルダパスを指定
        //    await PyEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        //}
        //catch(Exception e)
        //{
        //    throw;
        //}
        await PyEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        await VEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}