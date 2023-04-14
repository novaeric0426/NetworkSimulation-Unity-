using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Server : MonoBehaviour
{
    [SerializeField]
    private List<Client> clients;
    [SerializeField]
    private List<Entity> entities;
    public SimulatedNetwork network;
    [SerializeField]
    private List<int> last_processed_input;
    public float serverUpdateRate;

    void Start()
    {
        var updatePeriod = 1 / serverUpdateRate;
        Debug.Log(updatePeriod);
        Connect(GameObject.Find("ClientA").GetComponent<Client>());
        Connect(GameObject.Find("ClientB").GetComponent<Client>());
        StartCoroutine(ServerRoutine(updatePeriod));
    }
    private IEnumerator ServerRoutine(float period)
    {
        yield return new WaitForSeconds(period);
        ProcessInput();
        SendWorldState();
        RenderWorld();
        StartCoroutine(ServerRoutine(period));
    }
    public SimulatedNetwork GetNetwork()
    {
        return network;
    }
    public void Connect(Client client)
    {
        client.SetServer(this);
        client.entity_id = clients.Count;
        clients.Add(client);
    }
    private void ProcessInput()
    {
        while (true)
        {
            var message = network.Receive();
            if (message == null)break;

            //Update the state of the entity, based on its input
            var id = message.entity_id;
            entities[id].ApplyInput(message);
            last_processed_input[id] = message.input_sequence_number;
        }
    }

    private void SendWorldState()
    {
        var num_clients = clients.Count;
        List<Packet> stateList = new List<Packet>();
        for (int i = 0; i < num_clients; i++)
        {
            var entity = entities[i];
            var packet = new Packet();
            packet.entity_id = entity.entity_id;
            packet.position = entity.delta_x;
            packet.last_processed_input = last_processed_input[i];
            stateList.Add(packet);
        }
        for (int i = 0; i < num_clients; i++)
        {
            var client = clients[i];
            foreach (var pkt in stateList)
            {
                client.GetNetwork().Send(client.lag, pkt);
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
}