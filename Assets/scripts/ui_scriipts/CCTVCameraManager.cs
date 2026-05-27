
using UnityEngine;

public class CCTVCameraManager : MonoBehaviour
{
    public Camera prisonerCamera; 
    public Camera guardCamera;   

    void Start()
    {
        SetPrisonerTurn(); 
    }

    public void SetPrisonerTurn()
    {
        prisonerCamera.gameObject.SetActive(true);
        guardCamera.gameObject.SetActive(false);
    }

    public void SetGuardTurn()
    {
        prisonerCamera.gameObject.SetActive(false);
        guardCamera.gameObject.SetActive(true);
    }
}