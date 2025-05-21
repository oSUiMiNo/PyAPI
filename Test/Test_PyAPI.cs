using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class Test_PyAPI : MonoBehaviour
{
    PyAPI py;

    async void Start()
    {
        py = new PyAPI(
            @"C:\Users\osuim\AppData\Local\Programs\Python\Python312\python.exe",
            @"C:\Users\osuim\Documents\Unity\Maku\Maku\Packages\Maku\PythonAPI\PythonAssets"
        );

        await Exe();
        Exe().Forget();
        Count();
    }

    private async void Count()
    {
        for (int i = 0; i <= 10; i++)
        {
            Debug.Log(i);
            await Delay.Second(1);
        }
    }

    async UniTask Exe()
    {
        JObject inputJObj = new JObject();

        // —v‘f’Ç‰Á
        inputJObj["Power"] = true;
        inputJObj["Battery"] = 88;
        inputJObj["CPU"] = "Intel";
        inputJObj["Drives"] = new JArray("HDD", "SSD");
        Debug.Log(inputJObj);

        // List‚É—v‘f’Ç‰Á
        JArray drivesArray = (JArray)inputJObj["Drives"];
        drivesArray.Add("USB");
        Debug.Log(inputJObj);

        //JObject outputJObj = await py.Exe("Test_PyAPI.py", inputJObj, 10);
        //Debug.Log($"Œ‹‰ÊF{outputJObj}");
    }
}


