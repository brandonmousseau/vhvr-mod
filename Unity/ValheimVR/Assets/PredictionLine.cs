using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PredictionLine : MonoBehaviour
{
    // Start is called before the first frame update

    private LineRenderer lr;

    private void Awake() {
        lr = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update() {

        Vector3 pos = transform.position;
        Vector3 vel = -transform.forward * 20;
        List<Vector3> pointList = new List<Vector3>();
        
        for (int i = 0; i < 20; i++) {
            pointList.Add(pos);
            vel += Vector3.down * 9.81f *0.1f;
            pos += vel * 0.1f;
        }

        lr.positionCount = 20;
        lr.SetPositions(pointList.ToArray());

    }
}
