using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class GetItems : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        using (var reader = new StreamReader("data/APEM_courier_items.csv"))
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');

                try
                {
                    transform.Find(values[0]).GetComponent<ItemsList>().itemsList.Add(values[1]);
                }
                catch { }
            }
        }
    }
        
}
