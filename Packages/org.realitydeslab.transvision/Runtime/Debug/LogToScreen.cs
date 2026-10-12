using UnityEngine;
using TMPro;

public class LogToScreen : MonoBehaviour
{

    public TextMeshProUGUI text;
    public Transform offsetCamera;
    public Transform mainCamera;
    public Transform blackCamera;
    public Transform stereoTrackedPose;
    public Transform centralEyePose;
    public Transform plane;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        text.text = $"Offset Camera: Pos:{offsetCamera.position}, Angles:{offsetCamera.eulerAngles} \n " +
            $"mainCamera: Pos:{mainCamera.position}, Angles:{mainCamera.eulerAngles}\n" +
            $"blackCamera: Pos:{blackCamera.position}, Angles:{blackCamera.eulerAngles}\n" +
            $"stereoTrackedPose: Pos:{stereoTrackedPose.position}, Angles:{stereoTrackedPose.eulerAngles}\n" +
            $"centralEyePose: Pos:{centralEyePose.position}, Angles:{centralEyePose.eulerAngles}\n" +            
            $"Plane: Pos:{plane.position}, Angles:{plane.eulerAngles}\n";

    }
}
