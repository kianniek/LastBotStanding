using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    private GameObject player;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if(player == null)
        {
            Instantiate(playerPrefab, transform.position, Quaternion.identity, null);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
