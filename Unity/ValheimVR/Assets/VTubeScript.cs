using UnityEngine;

public class VTubeScript : MonoBehaviour {
    public Transform parent;
    
    // Start is called before the first frame update
    void Start() {

        setBodyLayers(parent.Find("Visual"));
        
        var selfStreamCam = new GameObject();
        selfStreamCam.transform.parent = parent;
        selfStreamCam.transform.localPosition = new Vector3(-1.3f, 1.9f, 5);
        selfStreamCam.transform.Rotate(new Vector3(0, 180, 0));
        var cam = selfStreamCam.AddComponent<Camera>();
        cam.ResetProjectionMatrix();
        cam.cullingMask = 1 << 24;
        cam.clearFlags = CameraClearFlags.Nothing;
        cam.orthographicSize = 1;
        cam.depth = 10;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.orthographic = true;
    }
    
    private void setBodyLayers(Transform target) {
        
        Debug.Log("LAYER: " + target.gameObject.layer);
        target.gameObject.layer = 24;
        
        for (int i = 0; i < target.childCount; i++)
        {
            setBodyLayers(target.GetChild(i));
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
