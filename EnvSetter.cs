using MyUtil;
using UnityEngine;


public class EnvSetter : MonoBehaviour
{
    async void Start()
    {
        //�C�ӂ̃t�H���_�p�X���w��
        await PyEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env", "3.12.5");
        await VEnvSetter.ExeFlow($"{Application.streamingAssetsPath}/PythonAssets/Env");
    }
}