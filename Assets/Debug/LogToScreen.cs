using UnityEngine;
using TMPro;

public class LogToScreen : MonoBehaviour
{

    public TextMeshProUGUI text;
    public Transform offsetCamera;
    public Transform plane;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        text.text = $"Offset Camera: Pos:{offsetCamera.position}, Angles:{offsetCamera.eulerAngles} \n Plane: Pos:{plane.position}, Angles:{plane.eulerAngles}";

    }
}
