using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class Timezone
{
    public int year;
    public int month;
    public int day;
    public int hour;
    public int minute;
    public int seconds;
    public int milliSeconds;
    public string dateTime;
    public string date;
    public string time;
    public string timeZone;
    public string dayOfWeek;
    public bool dstActive;
}

public class TimezoneSystem : MonoBehaviour
{
    [SerializeField] private GameObject _mainMenu;
    [SerializeField] private TMPro.TMP_InputField _input;
    [SerializeField] private TMPro.TMP_Text _errorOutput;
    [SerializeField] private Button _submit;
    [SerializeField] private Toggle _formatToggle;

    [SerializeField] private TMPro.TMP_Text _cityName;
    [SerializeField] private TMPro.TMP_Text _output;

    [SerializeField] private GameObject[] _housePrefabs;
    [SerializeField] private List<GameObject> _currentHouses = new List<GameObject>();

    private TimeSpan _currentTime = TimeSpan.MinValue;
    private string _currentCity = string.Empty;
    private int _currentCitySeed = 1;
    private bool _twelveOurFormat = true;

    static int StringToNumber(string str)
    {
        int hash = 0;
        foreach (char c in str)
        {
            hash += c;
        }
        return Math.Abs(hash);
    }

    System.Random random;
    int GetRandomNumber()
    {
        return random.Next(0, _housePrefabs.Length + 1);
    }

    private void Start()
    {
        Startup.Instance.AssignHandler(PacketType.GetTimezone, GetTimezoneMessage);

        _submit.onClick.AddListener(SendTimezoneRequest);
    }

    public void SendTimezoneRequest()
    {
        PacketWrite packetWrite = new PacketWrite(PacketType.GetTimezone);
        packetWrite.WriteString(_input.text);

        Startup.Instance.SendData(packetWrite);
    }

    private void GetTimezoneMessage(PacketRead read)
    {
        StopAllCoroutines();

        string chatMessage = read.ReadString();

        if (chatMessage != "Not found")
        {
            _currentTime = TimeSpan.Parse(chatMessage);
            _currentCity = _input.text;
            _currentCitySeed = StringToNumber(_currentCity);
            _twelveOurFormat = !_formatToggle.isOn;
            random = new System.Random(_currentCitySeed);

            _cityName.text = _currentCity;
            _mainMenu.SetActive(false);

            GenerateField();

            StartCoroutine(UpdateTime());
        }
        else
        {
            _errorOutput.text = "Город не найден!";
        }
    }

    private void GenerateField()
    {
        _currentHouses.ForEach((house) => Destroy(house));
        _currentHouses.Clear();

        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                int number = GetRandomNumber();

                if (number == _housePrefabs.Length)
                {
                    continue;
                }
                else
                {
                    _currentHouses.Add(Instantiate(_housePrefabs[number], new Vector3(x * 3, 0, y * 3), Quaternion.identity));
                }
            }
        }
    }

    private IEnumerator UpdateTime()
    {
        while (true)
        {
            if (_twelveOurFormat)
            {
                bool pm = _currentTime.Hours > 11;
                string postFormat = pm ? "pm" : "am";

                TimeSpan result = _currentTime;
                if (pm)
                {
                    result -= TimeSpan.FromHours(12);
                }

                _output.text = $"{result.ToString()} {postFormat}";
            }
            else
            {
                _output.text = _currentTime.ToString();
            }

            yield return new WaitForSeconds(1f);

            _currentTime += TimeSpan.FromSeconds(1f);
        }
    }
}
