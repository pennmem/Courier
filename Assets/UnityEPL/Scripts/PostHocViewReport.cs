using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PostHocViewReport : MonoBehaviour {
// NOTE: requires scene loaded logging

    // objects to omit should be listed by reporting ID
    private List<string> ObjectsToOmit = new List<string>() {
        "Player"
    };

    public Dictionary<string, GameObject> boxes = new Dictionary<string, GameObject>();
    public GameObject player;

    private string destlog;

    // NOTE: for Courier only
    private bool storesMapped = false;
    private double prevTime = -1.0f;
    public bool slideshow = false;

    public void ToggleSlideshow(bool value) {
        slideshow = value;
    }

    public void Awake() {
        DontDestroyOnLoad(this.gameObject);
    }
    
    public void StartViewCheck() {
        StartCoroutine(CheckView());
    }

    private IEnumerator CheckView() {
        // NOTE: global variable to stop game logic from modifying log file
        UnityEPL.viewCheck = true;
        SceneManager.sceneLoaded += onSceneLoaded;

        Debug.Log(System.IO.Path.Combine(UnityEPL.GetDataPath(), "session.jsonl"));
        string path = System.IO.Path.Combine(UnityEPL.GetDataPath(), "session.jsonl");
        destlog = System.IO.Path.Combine(UnityEPL.GetDataPath(), "viewlog.jsonl");

        boxes = SearchGameObjects(ObjectsToOmit); 

        if(System.IO.File.Exists(destlog) || !System.IO.File.Exists(path)) {
            yield break;
        }

        List<JObject> log = ReadLog(path);
        Vector3 pos;
        Quaternion rot;
        bool success;

        foreach(JObject line in log) {
            string evType = (string)line["type"];

            // Two different methods of logging player, directly and via worlddatareporter
            if(evType.ToLower() == "playertransform") {
                // NOTE: Courier Only
                if(!storesMapped) {
                    continue;
                }
                success = ReadLogLine(line, out pos, out rot);
                player.transform.position = pos;
                player.transform.rotation = rot;
                Debug.Log("moved player");
                Physics.SyncTransforms();

                int hits;
                foreach(string k in boxes.Keys) {
                    if(ObjectsToOmit.Contains(k)) {
                        continue;
                    }

                    hits = 0;
                    hits = Raycast(boxes[k]); // tiny optimization could be made if rays hit other logged objects
                    WriteViewLine(line, k, hits);
                }

                if(slideshow && prevTime > 0) {
                    yield return new WaitForSecondsRealtime((float)((double)line["time"] - prevTime) / 1000 );
                }
                prevTime = (double)line["time"];
            }
            else if(evType == "loadScene") {
                CleanScene((string)line["data"]["sceneName"]);
                yield return null;
            }
            else if(evType == "store mappings") {
                storesMapped = true;
                boxes = RemapStores(line["data"]);
            }
            else if(evType.Contains("Spawn")) {
                success = ReadLogLine(line, out pos, out rot);

                // looks for a prefab whose name is contained in objectName 
                CreateBox((string)line["data"]["objectName"], (string)line["data"]["reportID"], pos, rot);
            }
            else if(evType.Contains("Despawn")) {
                DestroyBox((string)line["data"]["reportID"]);
            } 
            else if(evType.Contains("Transform")) {
                // playerTransform done in first if
                success = ReadLogLine(line, out pos, out rot);
                UpdateBox( (string)line["data"]["reportID"], pos, rot);
            }
        }

        SceneManager.sceneLoaded -= onSceneLoaded;
        UnityEPL.viewCheck = false;
        SceneManager.LoadScene("MainMenu");
        yield break;
    }

    public void onSceneLoaded(Scene scene, LoadSceneMode mode) {
        Debug.Log("Scene Loaded");
        player = GameObject.Find("Player");
        if(player == null) {
            throw new Exception("no player found");
        }

        DataHandler[] handlers = FindObjectsOfType<DataHandler>() as DataHandler[];
        foreach(DataHandler dh in handlers) {
            // avoid altering original log
            dh.gameObject.SetActive(false);
        }

        boxes = SearchGameObjects(ObjectsToOmit);
    }

    public List<JObject> ReadLog(string path) {
        List<JObject> log = new List<JObject>();
        string line;

        if(System.IO.File.Exists(path)) {
            using(System.IO.StreamReader file =  new System.IO.StreamReader(path)) {
                while((line = file.ReadLine()) != null)  
                {  
                    log.Add(JObject.Parse(line));
                } 

                return log;
            }
        }
        else {
           throw new Exception("Log not found");
        }
    }

    public bool ReadLogLine(JObject prop, out Vector3 pos, out Quaternion rot) {
        float x, y, z;

        x = (float)prop["data"]["positionX"];
        y = (float)prop["data"]["positionY"];
        z = (float)prop["data"]["positionZ"];
        pos = new Vector3(x, y, z);

        x = (float)prop["data"]["rotationX"];
        y = (float)prop["data"]["rotationY"];
        z = (float)prop["data"]["rotationZ"];

        rot = Quaternion.Euler(x, y, z);

        // TODO
        return true;
    }

    public Dictionary<string, GameObject> RemapStores(JToken mappings) {
        Queue<string> toMap = new Queue<string>(boxes.Keys);
        Dictionary<string, GameObject> remapped = new Dictionary<string, GameObject>();
        string store = "";
        while(toMap.Count > 0) {
            store = toMap.Dequeue();

            GameObject shop = boxes[store];
            if(shop.transform.root.name != "NamedStores") {
                Debug.Log("not a store");
                continue;
            }

            while(shop.transform.parent.name != "NamedStores") {
                shop = shop.transform.parent.gameObject;
            }
            string storeName = shop.name;

            string newName = (string) mappings[storeName];
            newName = String.Join("", (new List<string>(newName.Split(new char[] {' ', '_'} ))).Select(x => char.ToUpper(x[0]) + x.Substring(1).ToLower()));
            string oldName = String.Join("", (new List<string>(storeName.Split(new char[] {' ', '_'} ))).Select(x => char.ToUpper(x[0]) + x.Substring(1).ToLower()));

            newName = store.Replace(oldName, newName);
            boxes[store].GetComponent<WorldDataReporter>().reportingID = newName;
            remapped.Add(newName, boxes[store]);
        }
        return remapped;
    }

    public void WriteViewLine(JObject line, string id, int hits) {
        // needs public view report destination
        string writeMe = "{";
        writeMe += "\"reportID\":" + "\"" + id + "\", ";
        writeMe += "\"time\":" + line["time"].ToString() + ", ";
        writeMe += "\"inView\":" + (hits>0).ToString() + ", ";
        writeMe += "\"hits\":" + hits.ToString() + ", ";

        GameObject obj = boxes[id];
        writeMe += "\"locationX\":" + obj.transform.position.x.ToString() + ", ";
        writeMe += "\"locationY\":" + obj.transform.position.y.ToString() + ", ";
        writeMe += "\"locationZ\":" + obj.transform.position.z.ToString() + "}";

        System.IO.File.AppendAllText(destlog, writeMe + System.Environment.NewLine);
    }

    public void CleanScene(string SceneName) {
        // load scene
        Debug.Log("Clean Scene");
        SceneManager.LoadScene(SceneName);

        // cleaning done in onSceneLoaded
        return;
    }

    public void CreateBox(string name, string id, Vector3 pos, Quaternion rot) {
        // add new boxcollider to dictionary 
        dynamic resource = Resources.Load(name);
        if(resource != null) {
            GameObject newBox = (GameObject)Instantiate(resource, pos, rot); 
            boxes.Add(id, newBox);
        }
    }

    public void UpdateBox(string id, Vector3 pos, Quaternion rot) {
        GameObject thisBox;

        if(boxes.TryGetValue(id, out thisBox)) {
            thisBox.transform.position = pos;
            thisBox.transform.rotation = rot; 
            thisBox.SetActive(true);
        }
    }

    public void DestroyBox(string id) {
        GameObject thisBox;
        if(boxes.TryGetValue(id, out thisBox)) {
            GameObject.Destroy(thisBox);

            if(!boxes.Remove(id)) { 
                throw new Exception("Could not remove collider " + id);
            }
        }
    }

    public Dictionary<string, GameObject> SearchGameObjects(List<string> names) {
        Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
        WorldDataReporter[] reporterObjects = FindObjectsOfType<WorldDataReporter>() as WorldDataReporter[];

        Debug.Log(reporterObjects.Length); 
        foreach(WorldDataReporter reporter in reporterObjects) {
            GameObject toLog = reporter.gameObject;
            string reportID = reporter.reportingID;
            if(!names.Contains(reportID)) {
                if(!toLog.GetComponent<BoxCollider>()) {
                    Debug.Log("You have selected enter/exit viewfield reporting for " + toLog.name + " but there is no box collider on the object." +
                                    "  This feature uses collision detection to compare with camera bounds and other objects.  Please add a collider or " +
                                    "unselect viewfield enter/exit reporting.");
                    continue;
                }
                objects.Add(reportID, toLog);
            }
        }

        return objects;
    }

    public int Raycast(GameObject obj) {

        Camera thisCamera = player.GetComponentInChildren<Camera>();
        Plane[] frustrumPlanes = GeometryUtility.CalculateFrustumPlanes(thisCamera);
        BoxCollider objectCollider = obj.GetComponent<BoxCollider>();
        if(objectCollider == null) {
            Debug.Log("How did this happen?");
        }

        Vector3[] vertices = GetColliderVertexPositions(objectCollider);

        RaycastHit lineOfSightHit;
        int hits = 0;
        bool lineOfSight;

        if(GeometryUtility.TestPlanesAABB(frustrumPlanes, objectCollider.bounds)) {
            foreach(Vector3 vert in vertices) {
                if(Physics.Linecast(thisCamera.transform.position, vert, out lineOfSightHit)) {
                    lineOfSight = lineOfSightHit.collider.Equals(objectCollider);
                    if(lineOfSight) {
                        hits++;
                    }
                }
            }
        }

        return hits;
    }

    private Vector3[] GetColliderVertexPositions(BoxCollider boxCollider) {
        Vector3[] vertices = new Vector3[9];

        Vector3 colliderCenter  = boxCollider.center;
        Vector3 colliderExtents = boxCollider.size/2.0f;
        Vector3 pointOffset = new Vector3(.02f, .02f, .02f);

        for (int i = 0; i < 8; i++)
        {
            Vector3 extents = colliderExtents;
            Vector3 offset = pointOffset;
            extents.Scale(new Vector3((i & 1) == 0 ? 1 : -1, (i & 2) == 0 ? 1 : -1, (i & 4) == 0 ? 1 : -1));
            offset.Scale(new Vector3((i & 1) == 0 ? 1 : -1, (i & 2) == 0 ? 1 : -1, (i & 4) == 0 ? 1 : -1));

            Vector3 vertexPosLocal = colliderCenter + extents - offset;

            Vector3 vertexPosGlobal = boxCollider.transform.TransformPoint(vertexPosLocal);

            // display vector3 to six decimal places
            vertices[i] = vertexPosGlobal;
        }
        vertices[8] = boxCollider.transform.TransformPoint(Vector3.zero); 
        
        return vertices;
    }
}