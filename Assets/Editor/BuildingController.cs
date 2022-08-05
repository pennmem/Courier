using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BuildingController : MonoBehaviour
{

    public List<GameObject> floorPrefab;
    public GameObject topPrefab;
    public GameObject basePrefab;
    public int baseSize = 3;

    public int floors = 1;
    bool updatingSize = false;

    void Start(){
        updatingSize = false;
    }

    public void updateFloors(){
        if(!updatingSize){
            updatingSize = true;
            int n = 0;
            int x = 1000;
            do{
                n = numberOfFloors();
                if(floors > 0){
                    if(floors > n){
                        addFloor(0);
                    }else if(floors < n){
                        removeFloor(0);
                    }
                }
                x++;
            }while(floors != n && x < 1000);
            updateTopPosition();
            updatingSize = false;
        }
    }

    public void Generate(){
        UnpackPrefab();
        updatingSize = false;
        for(int i = transform.childCount - 1; i >= 0; i--){
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        int y = 0;
        if(basePrefab != null){
            GameObject newObj = Instantiate(basePrefab, new Vector3(0,y,0),transform.rotation, transform);
            newObj.name = "base";
            newObj.transform.localPosition = new Vector3(0,y,0);
            y+=baseSize;
        }
        for(int x = 0; x < floors; x++){
            addFloor(0);
            y+=3;
        }
        if(topPrefab != null){
            GameObject newObj = Instantiate(topPrefab, new Vector3(0,y,0),transform.rotation, transform);
            newObj.name = "top";
            newObj.transform.localPosition = new Vector3(0,y,0);
        }
    }

    public void addFloor(int increment){
        UnpackPrefab();
        updatingSize = false;
        floors+=increment;
        int n = numberOfFloors();
        GameObject newObj = Instantiate(floorPrefab[Random.Range(0, floorPrefab.Count-1)], new Vector3(0,baseSize+(n*3),0),transform.rotation, transform);
        newObj.name = "floor"+n;
        newObj.transform.localPosition = new Vector3(0,baseSize+(n*3),0);
    }

    public void removeFloor(int increment){
        UnpackPrefab();
        updatingSize = false;
        floors+=increment;
        int n = numberOfFloors();
        DestroyImmediate(transform.Find("floor"+(n-1)).gameObject);

    }

    void UnpackPrefab(){
        if(PrefabUtility.IsPartOfAnyPrefab(transform.gameObject)){
            PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }

    int numberOfFloors(){
        int n = 0;
        foreach(Transform child in transform){
            if(child.name.Contains("floor")){
                n++;
            }
        }
        return n;
    }

    public void updateTopPosition(){
        int n = numberOfFloors();
        foreach(Transform child in transform){
            if(child.name.Contains("top")){
                child.localPosition = new Vector3(0,baseSize+(n*3),0);
            }
        }
    }

    
}
