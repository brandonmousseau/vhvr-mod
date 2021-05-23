
using System;
using UnityEngine;

    

public class shieldscript : MonoBehaviour {

    public GameObject otherObject;
    
    void Awake() {
        //initialize();
    }

    private void Update() {
        Debug.Log(Vector3.Dot(transform.forward, otherObject.transform.forward));
    }

    /**
         * create:
         * - standart layer (gray)
         * - disabled equipped layers (blue)
         * - items, without sprite yet
         */
    private void initialize() {
            
        var tex_green = new Texture2D(1, 1);
        tex_green.SetPixel(0,0, Color.green);
        tex_green.Apply();
        
        var tex_red = new Texture2D(1, 1);
        tex_red.SetPixel(0,0, Color.red);
        tex_red.Apply();
            
        GameObject greenLayer = new GameObject();
        greenLayer.transform.SetParent(transform, false);
        //greenLayer.transform.localPosition = positions[i];
        greenLayer.transform.localScale = new Vector2(1.0f, 4.0f);
        var standardRenderer = greenLayer.AddComponent<SpriteRenderer>();
        standardRenderer.sprite = Sprite.Create(tex_green, new Rect(0.0f, 0.0f, tex_green.width, tex_green.height), new Vector2(0.5f, 0.5f));
        standardRenderer.sortingOrder = 0;
                
        GameObject redLayer = new GameObject();
        redLayer.transform.SetParent(transform, false);
        //redLayer.transform.localPosition = positions[i];
        redLayer.transform.localScale = new Vector2(1.0f, 4.0f);
        var equipedRenderer = redLayer.AddComponent<SpriteRenderer>();
        equipedRenderer.sprite = Sprite.Create(tex_red, new Rect(0.0f, 0.0f, tex_red.width, tex_red.height), new Vector2(0.5f, 0.5f));
        equipedRenderer.sortingOrder = 2;
        //redLayer.SetActive(false);

    }
    
}
