using MyUtil;
using UnityEngine;
using System;


public class EnvSetter : MonoBehaviour
{
    async void Start()
    {
        try
        {
            //�C�ӂ̃t�H���_�p�X���w��
            await PyEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        }
        catch(Exception e)
        {
            Debug.Log($"3 {e}");
            throw;
        }
        await VEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}