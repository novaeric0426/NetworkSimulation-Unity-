using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Client : MonoBehaviour
{
    [SerializeField]
    private SimulatedNetwork simNet;
    private Server server;
    public float lag = 0;
    public List<Entity> entities;
    public int entity_id;
    public bool client_side_prediction = true;
    public bool server_reconciliation = true;

    private int input_sequence_number = 0;
    private List<Packet> pending_inputs;
    public bool entity_interpolation = true;

    private float last_ts = -1;
    public KeyCode rightKey;
    public KeyCode leftKey;

    public Toggle predictionToggle;
    public Toggle reconciliationToggle;
    public Toggle interpolationToggle;
    void Start()
    {
        InitToggleUI();
        pending_inputs = new List<Packet>();
    }

    private void FixedUpdate()
    {
        ProcessServerMessages();
        ProcessInput();
        if (entity_interpolation)InterpolateEntities();
        RenderWorld();
    }
    private void InitToggleUI()
    {
        predictionToggle.isOn = client_side_prediction;
        reconciliationToggle.isOn = server_reconciliation;
        interpolationToggle.isOn = entity_interpolation;
        predictionToggle.onValueChanged.AddListener(delegate
        {
            client_side_prediction = predictionToggle.isOn;
        });
        reconciliationToggle.onValueChanged.AddListener(delegate
        {
            server_reconciliation = reconciliationToggle.isOn;
        });
        interpolationToggle.onValueChanged.AddListener(delegate
        {
            entity_interpolation = interpolationToggle.isOn;
        });

    }
    public void UpdateLag(string input)
    {
        var lagValue = float.Parse(input);
        lag = lagValue;
    }
    private void ProcessInput()
    {
        //Compute delta time
        var now_ts = Time.time;
        if (last_ts < 0)last_ts = now_ts;
        var dt_sec = (now_ts - last_ts);
        last_ts = now_ts;

        Packet input = new Packet();
        if (Input.GetKey(rightKey))
        {
            input.press_time = dt_sec;
        }
        else if (Input.GetKey(leftKey))
        {
            input.press_time = -dt_sec;
        }
        else return;

        //Send the input to the server
        input.input_sequence_number = this.input_sequence_number++;
        input.entity_id = this.entity_id;
        server.GetNetwork().Send(lag, input);

        //Do client-side prediction
        if (client_side_prediction)
        {
            entities[entity_id].ApplyInput(input);
        }

        //Save this input for later reconciliation
        pending_inputs.Add(input);
    }
    private void ProcessServerMessages()
    {
        while (true)
        {
            var message = simNet.Receive();
            if (message == null)break;

            var entity = entities[message.entity_id];
            if (message.entity_id == this.entity_id)
            {
                //Reive the authoritative position of this client's entity
                entity.delta_x = message.position;
                if (server_reconciliation)
                {
                    int j = 0;
                    while (j < pending_inputs.Count)
                    {
                        var input = pending_inputs[j];
                        if (input.input_sequence_number <= message.last_processed_input)
                        {
                            //Already processed. Just drop it.
                            pending_inputs.RemoveAt(j);
                        }
                        else
                        {
                            //Not processed. Re-apply it.
                            entity.ApplyInput(input);
                            j++;
                        }
                    }
                }
                else
                {
                    //Reconciliation is disabled, so drop all saved inputs
                    pending_inputs.Clear();
                }
            }
            else
            {
                //Process other entity that is not this client
                if (!entity_interpolation)
                {
                    //if disabled, just accept the server's position
                    entity.delta_x = message.position;
                }
                else
                {
                    //Add it to the position buffer
                    var timestamp = Time.time;
                    entity.position_buffer.Add(new KeyValuePair<float, float>(timestamp, message.position));
                }
            }
        }

    }

    private void InterpolateEntities()
    {
        var now = Time.time;
        var render_timestamp = now - 0.1f; //1000 / 10 means server update late

        foreach (var entity in entities)
        {
            if (entity.entity_id == this.entity_id)
            {
                continue;
            }

            var buffer = entity.position_buffer;
            // Drop older positions
            while (buffer.Count > 2 && buffer[1].Key <= render_timestamp)
            {
                buffer.RemoveAt(0);
            }

            // Interpolate between the two positions
            if (buffer.Count >= 2 && buffer[0].Key <= render_timestamp && render_timestamp <= buffer[1].Key)
            {
                var x0 = buffer[0].Value;
                var x1 = buffer[1].Value;
                var time0 = buffer[0].Key;
                var time1 = buffer[1].Key;
                entity.delta_x = x0 + (x1 - x0) * (render_timestamp - time0) / (time1 - time0);
            }
        }
    }
    public void RenderWorld()
    {
        foreach (var entity in entities)
        {
            entity.gameObject.transform.position = new Vector3(entity.InitialPosition_x + entity.delta_x, entity.gameObject.transform.position.y, 0);
        }
    }
    public void SetServer(Server server)
    {
        this.server = server;
    }
    public SimulatedNetwork GetNetwork()
    {
        return simNet;
    }

}