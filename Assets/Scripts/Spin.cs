using UnityEngine;

public class Spin : MonoBehaviour
{
    public Vector3 speed = new Vector3(0, 60f, 0);

    void Update()
    {
        transform.Rotate(speed * Time.deltaTime, Space.World);
    }
}