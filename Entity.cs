using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity : MonoBehaviour
{
    public List<KeyValuePair<float, float>> position_buffer;
    private float speed = 3.0f;
    public int entity_id;
    public float position;
    public float delta_x = 0;
    public float InitialPosition_x;
    // Start is called before the first frame update
    private void Awake()
    {
        InitialPosition_x = gameObject.transform.position.x;
        position_buffer = new List<KeyValuePair<float, float>>();
    }
    private void Update()
    {
        position = gameObject.transform.position.x;
    }
    public void ApplyInput(Packet input)
    {
        delta_x += input.press_time * speed;
    }
}