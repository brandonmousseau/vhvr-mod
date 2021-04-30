using System;
using UnityEngine;

namespace ValheimVRMod.Scripts
{
    public class CollisionDetection : MonoBehaviour
    {
        private void Update()
        {
            //Debug.DrawRay(transform.position, Vector3.up, Color.red);
        }

        private void OnCollisionEnter(Collision collision)
        {

            Debug.Log("Collision Detected. OWN: " + gameObject.layer + " - " + name +
                      " OTHER: " + collision.gameObject.layer + " - " + collision.gameObject.name);

            
            
            foreach (ContactPoint point in collision.contacts)
            {
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.GetComponent<MeshRenderer>().material.color = Color.red;
                obj.transform.position = point.point;
                obj.transform.localScale *= 0.05f;
                Destroy(obj.GetComponent<SphereCollider>());
            }
           
            
            //Check for a match with the specified name on any GameObject that collides with your GameObject
            if (collision.gameObject.name == "MyGameObjectName")
            {
                //If the GameObject's name matches the one you suggest, output this message in the console
                Debug.Log("Do something here");
            }

            //Check for a match with the specific tag on any GameObject that collides with your GameObject
            if (collision.gameObject.tag == "MyGameObjectTag")
            {
                //If the GameObject has the same tag as specified, output this message in the console
                Debug.Log("Do something else here");
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {

            Debug.Log("Trigger Detected. OWN: " + gameObject.layer + " - " + name +
                      " OTHER: " + other.gameObject.layer + " - " + other.gameObject.name);

            
            //Check for a match with the specified name on any GameObject that collides with your GameObject
            if (other.gameObject.name == "MyGameObjectName")
            {
                //If the GameObject's name matches the one you suggest, output this message in the console
                Debug.Log("Do something here");
            }

            //Check for a match with the specific tag on any GameObject that collides with your GameObject
            if (other.gameObject.tag == "MyGameObjectTag")
            {
                //If the GameObject has the same tag as specified, output this message in the console
                Debug.Log("Do something else here");
            }
        }
        
        
    }
}