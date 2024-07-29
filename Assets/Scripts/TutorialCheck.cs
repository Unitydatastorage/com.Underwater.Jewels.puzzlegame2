using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialCheck : MonoBehaviour
{
    private int tutorial;
    public GameObject tW;
    void Start()
    {
        tutorial = PlayerPrefs.GetInt("TC", 0);
        if(tutorial == 0){
            tW.SetActive(true);
        }
    }
    public void TutorialCompleted(){
        tutorial = 1;
        PlayerPrefs.SetInt("TC", tutorial);
        PlayerPrefs.Save();
    }
}
