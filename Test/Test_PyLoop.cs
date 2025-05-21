using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UniRx;

public class Test_PyLoop : MonoBehaviour
{
    PyAPI py = new PyAPI(
        $"{Application.streamingAssetsPath}/PythonAssets",
        @"C:\Users\osuim\Documents\MyPJT\VEnvs\.venv\Scripts\python.exe"
    );

    BoolReactiveProperty a = new BoolReactiveProperty( true );

    void Start()
    {
        //LogTest();
        LogTestBG();
        //Test_Idle();
        //Test_IdleBG();
    }


    async void LogTest()
    {
        PyFnc LogTest = await py.Wait("LogTest.py", 6);
        Debug.Log(await LogTest.Exe());
    }


    async void LogTestBG()
    {
        PyFnc LogTest = await py.Wait("LogTest.py", 6);
        LogTest.OnOut.Subscribe(JO =>
        {
            Debug.Log(JO);
        });
        LogTest.ExeBG();
    }


    async void Test_Idle()
    {
        JObject inJO = new JObject();
        inJO["Data0"] = "����������������������";
        inJO["Data1"] = "��������������������";
        PyFnc Test_Idle = await py.Idle("Test_Idle.py", processCount: 20);

        await Delay.Second(7);

        a.TimerWhileEqualTo(true, 0.1f)
        .Subscribe(async _ =>
        {
            Debug.Log(await Test_Idle.Exe(inJO));
        }).AddTo(this);

        // �A�C�h�����O�֐��͎蓮�ŃN���[�Y
        Test_Idle.Close(5000);

        await Delay.Second(5);
        a.Value = false;
        a.Dispose();
    }


    // �A�C�h�����O�v���Z�X�������ŌJ��Ԃ����s����ꍇ�̓o�b�N�O���E���h�ł��
    async void Test_IdleBG()
    {
        JObject inJO = new JObject();
        inJO["Data0"] = "����������������������";
        inJO["Data1"] = "��������������������";
        PyFnc Test_Idle = await py.Idle("Test_Idle.py", processCount: 20);

        Test_Idle.OnOut.Subscribe(JO =>
        {
            Debug.Log($"{JO}");
        }).AddTo(this);

        a.TimerWhileEqualTo(true, 0.1f)
        .Subscribe(_ =>
        {
            Test_Idle.ExeBG(inJO);
        }).AddTo(this);

        // �A�C�h�����O�֐��͎蓮�ŃN���[�Y
        Test_Idle.Close(5000);

        await Delay.Second(5);
        a.Value = false;
        a.Dispose();
    }
}
    