using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ColorFilter : MonoBehaviour
{
    [Range(0f, 100f)] public int R = 100;
    [Range(0f, 100f)] public int G = 100;
    [Range(0f, 100f)] public int B = 100;
    [Range(0f, 100f)] public int S = 100;

    

    void Start()
    {
        Color filter = new Color(R / 100f * S / 100f, G / 100f * S / 100f, B / 100f * S / 100f, 1f);

        foreach (Transform component in transform)
        {
            if (component.name != "DeliveryZone") component.GetComponent<Renderer>().material.SetColor("_BaseColor", filter);
        }

    }

    
}
