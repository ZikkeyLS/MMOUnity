using UnityEngine;

public class House : MonoBehaviour
{
    private void OnMouseDown()
    {
        HouseTime.Instance.SetCurrentHouse(gameObject);
    }
}
