using UnityEngine;

public class Tank : MonoBehaviour
{
    [SerializeField] private float Speed;
    
    private void Update()
    {
        transform.localPosition += new Vector3(0, 0, Time.deltaTime * Speed);
    }
}