using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class testNavmesh : MonoBehaviour
{
    private NavMeshAgent n;
    public Vector3 pos;
    // Start is called before the first frame update
    void Start()
    {
        n = GetComponent<NavMeshAgent>();
        n.SetDestination(pos);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
