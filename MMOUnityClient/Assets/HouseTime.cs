using System;
using UnityEngine;

public class HouseTime : MonoBehaviour
{
    public static HouseTime Instance;

    [SerializeField] private GameObject _housePanelRoot;
    [SerializeField] private TMPro.TMP_Text _text;
    [SerializeField] private int _houseLength = 10;
    [SerializeField] private int _virtualDistance = 3;

    private GameObject _currentHouse;

    private void Awake()
    {
        if (Instance)
        {
            Debug.LogWarning("Instance of HouseTime already exists!");
            Destroy(this);
        }
        Instance = this;
    }

    private void Update()
    {
        if (_currentHouse == null)
        {
            _housePanelRoot.SetActive(false);
            return;
        }

        if (TimezoneSystem.Instance != null)
        {
            string time = (TimezoneSystem.Instance.CurrentTime
                - TimeSpan.FromMinutes(_currentHouse.transform.position.x - _houseLength * _virtualDistance)).ToString();
            _text.text = $"Время в этом доме: {time}";
        }
    }

    public void SetCurrentHouse(GameObject gameObject)
    {
        _currentHouse = gameObject;
        _housePanelRoot.SetActive(true);
    }

    public void ClearHouse()
    {
        _currentHouse = null;
    }
}
