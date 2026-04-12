using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
/// <summary>
/// 
/// </summary>
[ExecuteInEditMode]
public class ClickSpawn : MonoBehaviour
{
    private void OnEnable()
    {
        if(!Application.isEditor)
        {
            Destroy(this);
        }
        SceneView.duringSceneGui += OnScene;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
    }

    void OnScene(SceneView scene)
    {
        Event e = Event.current;

        if(e.type == EventType.MouseDown && e.button == 2)
        {
            Debug.Log("Middle Mouse was pressed");

            Vector3 mousePos = e.mousePosition;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            mousePos.y = scene.camera.pixelHeight - mousePos.y * ppp;
            mousePos.x *= ppp;

            Ray ray = scene.camera.ScreenPointToRay(mousePos);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit))
            {
                //Do something, ---Example---
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                
                go.transform.position = hit.point;
                Debug.Log("Instantiated Primitive " + hit.point);

                if(GameObject.FindObjectOfType<SimplePlayerController>().gameObject != null)
                {
                    FindObjectOfType<SimplePlayerController>().gameObject.transform.position = new Vector3(
                        go.transform.position.x, go.transform.position.y + 1, go.transform.position.z);
                    //Baddie[] bds = FindObjectsOfType<Baddie>();
                    //if(bds != null && bds.Length > 0)
                    //{
                    //    foreach(Baddie b in bds)
                    //    {
                    //        b.BroadcastMessage("Death", SendMessageOptions.DontRequireReceiver);
                    //    }
                    //}
                }
                go.name = "DELETE ME - TEMP";
                Destroy(go);
            }
            e.Use();
            gameObject.AddComponent<ClickSpawn>();
            
        }
    }
}
#endif