using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class SidewalkController : MonoBehaviour
{

    public GameObject straight;
    public GameObject corner;
    public GameObject middle;

    int previowsSize = 0;
    public int size = 0;
    public int width = 1;
    bool updatingSize = false;

    void Start(){
        previowsSize = size;
    }
    
    public void setSize(){
        if(!updatingSize){
            updatingSize = true;
            if(size > 0){
                do{
                    if(size > straightPiecesCount()){
                        increaseSize(false);
                    }else if(size < straightPiecesCount()){
                        decreaseSize(false);
                    }
                }while(size != straightPiecesCount());
            }
            updateWidth(0);
            updatingSize = false;
        }
    }

    public int straightPiecesCount(){
        int count = 0;
        foreach(Transform t in transform){
            if(!t.gameObject.name.Contains("corner")){
                count++;
            }
        }
        return count;
    }

    public void updateWidth(int increment){
        width+=increment;
        foreach(Transform child in transform){
            setWidth(child.gameObject);
        }
    }

    public void increaseSize(bool updateSize){
        updatingSize = false;
        if(updateSize){
            size++;
        }
        
        createStraightPiece();
        UpdateCorner();
    }

    public void decreaseSize(bool updateSize){
        updatingSize = false;
        if(straightPiecesCount() > 0){
            if(updateSize){
                size--;
            }
            Transform last = null;
            foreach(Transform t in transform){
                if(!t.gameObject.name.Contains("corner")){
                    if(last == null){
                        last = t;
                    }else{
                        if(t.localPosition.z > last.localPosition.z){
                            last = t;
                        }
                    }
                }
            }
            if(last != null){
                DestroyImmediate(last.gameObject);
                UpdateCorner();
            }
        }
    }

    void createStraightPiece(){
        GameObject newObj = Instantiate(straight, new Vector3(0,0,0),transform.rotation, transform);
        newObj.transform.localPosition = new Vector3(-((width-1)*3.3f),0,((straightPiecesCount())*3.3f));
        setWidth(newObj);
    }

    void setWidth(GameObject obj){
        if(obj.transform.childCount != width-1){
            for(int i = obj.transform.childCount - 1; i >= 0; i--){
                DestroyImmediate(obj.transform.GetChild(i).gameObject);
            }
            if(obj.name.Contains("corner") && obj.transform.localEulerAngles.y != 0){
                obj.transform.localPosition = new Vector3(-((width-1)*3.3f)+0.1f,obj.transform.localPosition.y,obj.transform.localPosition.z);
            }else{
                obj.transform.localPosition = new Vector3(-((width-1)*3.3f),obj.transform.localPosition.y,obj.transform.localPosition.z);
            }
            
            if(width > 1){
                for(int i = 1; i < width; i++){
                    if(obj.name.Contains("corner")){
                        if(obj.transform.localEulerAngles.y != 0){
                            GameObject newObj = Instantiate(straight, new Vector3(0,0,0),transform.localRotation, obj.transform);
                            newObj.transform.localRotation = Quaternion.Euler(0,0,0);  
                            newObj.transform.localPosition = new Vector3(0,0,-(i*3.3f)+0.1f);
                        }else{
                            GameObject newObj = Instantiate(straight, new Vector3(0,0,0),transform.localRotation, obj.transform);
                            newObj.transform.localRotation = Quaternion.Euler(0,90,0);  
                            newObj.transform.localPosition = new Vector3(i*3.3f,0,0.1f);
                        }
                        
                    }else{
                        GameObject newObj = Instantiate(middle, new Vector3(0,0,0),transform.rotation, obj.transform);
                        newObj.transform.localPosition = new Vector3(i*3.3f,0,0);
                    }
                    
                }
            }
        }
    }
    void createCornerPiece(){

    }

    void UpdateCorner(){
        foreach(Transform t in transform){
            if(t.gameObject.name.Contains("corner") && t.localEulerAngles.y == 0){
                t.localPosition = new Vector3(-((width-1)*3.3f),0,(((straightPiecesCount()+1)*3.3f))-0.1f);
            }
        }
    }



    public void addCorner(){
        UnpackPrefab();
        removeCorner();
        GameObject newCorner = Instantiate(corner, new Vector3(0,0,0),transform.rotation, transform);
        newCorner.transform.localPosition = new Vector3(-((width-1)*3.3f),0,(((straightPiecesCount()+1)*3.3f))-0.1f);

        GameObject newCorner2 = Instantiate(corner, new Vector3(0,0,0),transform.rotation, transform);
        newCorner2.transform.localRotation = Quaternion.Euler(0,-90,0);  
        newCorner2.transform.localPosition = new Vector3(0.1f,0,0);
    }

    public void removeCorner(){
        UnpackPrefab();
        for(int i = transform.childCount - 1; i >= 0; i--){
            if(transform.GetChild(i).gameObject.name.Contains("corner")){
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            
        }

    }

    void UnpackPrefab(){
        if(PrefabUtility.IsPartOfAnyPrefab(transform.gameObject)){
            PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }
}
