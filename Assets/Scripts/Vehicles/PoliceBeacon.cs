 using UnityEngine;

public class PoliceBeacon : MonoBehaviour
{
    public Light redLight;
    public Light blueLight;
    public float blinkSpeed = 8f;

    void Update()
    {
        bool redOn = Mathf.PingPong(Time.time * blinkSpeed, 2f) < 1f;
        redLight.enabled = redOn;
        blueLight.enabled = !redOn;
    }
}