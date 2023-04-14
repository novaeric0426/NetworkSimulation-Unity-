using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulatedNetwork : MonoBehaviour
{

    public List<KeyValuePair<float, Packet>> messages;
    void Start()
    {
        messages = new List<KeyValuePair<float, Packet>>();
    }
    // Start is called before the first frame update
    public void Send(float lag_ms, Packet message)
    {
        messages.Add(new KeyValuePair<float, Packet>(Time.time + lag_ms, message));
    }

    public Packet Receive()
    {
        var now = Time.time;
        for (int i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (message.Key <= now)
            {
                messages.RemoveAt(i);
                return message.Value;
            }
        }
        return null;
    }
}