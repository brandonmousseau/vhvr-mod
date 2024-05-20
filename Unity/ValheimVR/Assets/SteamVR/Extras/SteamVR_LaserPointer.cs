//======= Copyright (c) Valve Corporation, Google LLC, All rights reserved. ===============
using UnityEngine;
using System.Collections;

namespace Valve.VR.Extras
{
    public class SteamVR_LaserPointer : MonoBehaviour
    {
        public SteamVR_Behaviour_Pose pose;

        public SteamVR_Action_Boolean leftClick = SteamVR_Input.GetBooleanActionFromPath("/actions/LaserPointers/in/LeftClick");
        public SteamVR_Action_Boolean rightClick = SteamVR_Input.GetBooleanActionFromPath("/actions/LaserPointers/in/RightClick");

        public bool active = true;
        public Color color;
        public float thickness = 0.002f;
        public Color clickColor = Color.green;
        public GameObject holder;
        public GameObject pointer;
        bool isActive = false;
        public bool addRigidBody = false;
        public Transform reference;
        public event PointerEventHandler PointerIn;
        public event PointerEventHandler PointerOut;
        public event PointerEventHandler PointerClick;
        public event PointerEventHandler PointerRightClick;
        public event PointerEventHandler PointerTracking;
        public float maxRaycastDistance = Mathf.Infinity;
        public int raycastLayerMask = Physics.DefaultRaycastLayers;
        public Vector3 rayStartingPosition = Vector3.zero;
        public Quaternion rayDirection = Quaternion.identity;


        private bool __usePointer = true;
        private bool mouseButtonsLocked = false;

        Transform previousContact = null;

        public void setVisible(bool visible) {
            if (pointer != null) {
                pointer.GetComponent<Renderer>().enabled = visible;
            }
        }

        public void setUsePointer(bool usePointer) {
            __usePointer = usePointer;
        }

        public bool pointerIsActive() {
            return __usePointer;
        }

        public Transform pointerTransform { get {
            return transform;
        } }

        private void Start()
        {
            if (pose == null)
                pose = this.GetComponent<SteamVR_Behaviour_Pose>();
            if (pose == null)
                Debug.LogError("No SteamVR_Behaviour_Pose component found on this object", this);

            if (leftClick == null)
                Debug.LogError("No LeftClick action has been set on this component.", this);
            if (rightClick == null)
                Debug.LogError("No RightClick action has been set on this component.", this);

            holder = new GameObject();
            holder.transform.parent = this.transform;
            holder.transform.localPosition = new Vector3(0.04f, -0.05f, -0.01f);
            holder.transform.localRotation = Quaternion.Euler(40f, 3f, 0f);

            pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointer.layer = 23; // Hands layer
            pointer.transform.parent = holder.transform;
            pointer.transform.localScale = new Vector3(thickness, thickness, 100f);
            pointer.transform.localPosition = new Vector3(0f, 0f, 50f);
            pointer.transform.localRotation = Quaternion.identity;
            BoxCollider collider = pointer.GetComponent<BoxCollider>();
            if (addRigidBody)
            {
                if (collider)
                {
                    collider.isTrigger = true;
                }
                Rigidbody rigidBody = pointer.AddComponent<Rigidbody>();
                rigidBody.isKinematic = true;
            }
            else
            {
                if (collider)
                {
                    Object.Destroy(collider);
                }
            }
            Material newMaterial = new Material(ShaderLoader.GetShader("Custom/SteamVR_ClearAll"));
            newMaterial.SetColor("_Color", color);
            pointer.GetComponent<MeshRenderer>().material = newMaterial;
        }

        public virtual void OnPointerIn(PointerEventArgs e)
        {
            if (PointerIn != null)
                PointerIn(this, e);
        }

        public virtual void OnPointerClick(PointerEventArgs e)
        {
            if (PointerClick != null)
                PointerClick(this, e);
        }

        public virtual void OnPointerRightClick(PointerEventArgs e)
        {
            if (PointerRightClick != null)
                PointerRightClick(this, e);
        }

        public virtual void OnPointerOut(PointerEventArgs e)
        {
            if (PointerOut != null)
                PointerOut(this, e);
        }

        public virtual void OnPointerTracking(PointerEventArgs e) {
            if (PointerTracking != null)
                PointerTracking(this, e);
        }

        private void OnEnable() {
            mouseButtonsLocked = true;
        }

        private void Update()
        {
            if (!isActive)
            {
                isActive = true;
                this.transform.GetChild(0).gameObject.SetActive(true);
            }
            rayStartingPosition = holder.transform.position;
            rayDirection = holder.transform.rotation;
            if (!__usePointer) {
                return;
            }
            if (!leftClick.GetState(pose.inputSource) && !rightClick.GetState(pose.inputSource)) {
                // We lock the mouse buttons to false if they we pressed when the component
                // first becomes active and keep them locked until both are released to
                // prevent bad behavior when switching between control action sets in game.
                // This is only used for the "OnPointerTracking" event.
                mouseButtonsLocked = false;
            }

            bool leftButtonState = mouseButtonsLocked ? false : leftClick.GetState(pose.inputSource);
            bool rightButtonState = mouseButtonsLocked ? false : rightClick.GetState(pose.inputSource);

            float dist = 100f;
            Ray raycast = new Ray(rayStartingPosition, rayDirection * Vector3.forward);
            RaycastHit hit;
            bool bHit = Physics.Raycast(raycast, out hit, maxRaycastDistance, raycastLayerMask);

            if (previousContact && previousContact != hit.transform)
            {
                PointerEventArgs args = new PointerEventArgs();
                args.fromInputSource = pose.inputSource;
                args.distance = 0f;
                args.flags = 0;
                args.target = previousContact;
                args.position = hit.point;
                OnPointerOut(args);
                previousContact = null;
            }
            if (bHit && previousContact == hit.transform) {
                PointerEventArgs argsTracking = new PointerEventArgs();
                argsTracking.fromInputSource = pose.inputSource;
                argsTracking.distance = hit.distance;
                argsTracking.flags = 0;
                argsTracking.target = hit.transform;
                argsTracking.position = hit.point;
                argsTracking.buttonStateLeft = leftButtonState;
                argsTracking.buttonStateRight = rightButtonState;
                OnPointerTracking(argsTracking);
            }
            if (bHit && previousContact != hit.transform)
            {
                PointerEventArgs argsIn = new PointerEventArgs();
                argsIn.fromInputSource = pose.inputSource;
                argsIn.distance = hit.distance;
                argsIn.flags = 0;
                argsIn.target = hit.transform;
                argsIn.position = hit.point;
                OnPointerIn(argsIn);
                previousContact = hit.transform;
            }
            if (!bHit)
            {
                previousContact = null;
            }
            if (bHit && hit.distance < 100f)
            {
                dist = hit.distance;
            }

            if (bHit && leftClick.GetStateUp(pose.inputSource))
            {
                PointerEventArgs argsClick = new PointerEventArgs();
                argsClick.fromInputSource = pose.inputSource;
                argsClick.distance = hit.distance;
                argsClick.flags = 0;
                argsClick.target = hit.transform;
                argsClick.position = hit.point;
                OnPointerClick(argsClick);
            }

            if (bHit && rightClick.GetStateUp(pose.inputSource))
            {
                PointerEventArgs argsClick = new PointerEventArgs();
                argsClick.fromInputSource = pose.inputSource;
                argsClick.distance = hit.distance;
                argsClick.flags = 0;
                argsClick.target = hit.transform;
                argsClick.position = hit.point;
                OnPointerRightClick(argsClick);
            }


            if (leftClick != null && leftClick.GetState(pose.inputSource))
            {
                pointer.transform.localScale = new Vector3(thickness * 5f, thickness * 5f, dist);
                pointer.GetComponent<MeshRenderer>().material.color = clickColor;
            }
            else
            {
                pointer.transform.localScale = new Vector3(thickness, thickness, dist);
                pointer.GetComponent<MeshRenderer>().material.color = color;
            }

            pointer.transform.localPosition = new Vector3(0f, 0f, dist / 2f);
        }
    }

    public struct PointerEventArgs
    {
        public SteamVR_Input_Sources fromInputSource;
        public uint flags;
        public float distance;
        public Transform target;
        public Vector3 position;
        public bool buttonStateLeft;
        public bool buttonStateRight;
    }

    public delegate void PointerEventHandler(object sender, PointerEventArgs e);
}
